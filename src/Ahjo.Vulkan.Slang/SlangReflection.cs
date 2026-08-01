using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Slang.Internal;
using Ahjo.Vulkan.Slang.Native;

namespace Ahjo.Vulkan.Slang;

/// <summary>
/// The binding surface of a linked Slang program, expressed in the wrapper's
/// own description types — <c>DescriptorBinding</c>, <c>PushConstantRange</c>
/// and <c>VertexAttributeDescription</c>.
/// </summary>
/// <remarks>
/// <para>There is deliberately no parallel <c>Slang*</c> description type set.
/// The value of reflecting is that its output is the type
/// <c>Device.CreateDescriptorSetLayout</c> and
/// <c>Device.CreatePipelineLayout</c> already take.</para>
/// <para><b>This is always the layout of a linked, fully specialized
/// program.</b> A <see cref="SlangReflection"/> can only be obtained from a
/// <see cref="SlangProgram"/>, and a <see cref="SlangProgram"/> can only be
/// obtained from a successful <c>link</c>, because both of the alternatives
/// are silently wrong rather than loudly wrong: a module reflected on its own
/// reports different sets and binding indices than the same module inside a
/// composite, and an unspecialized generic parameter block reports a
/// descriptor set with <em>zero</em> bindings where the compiled shader has
/// five.</para>
/// <para><b>Set indices are Vulkan set numbers, not positions.</b> A program's
/// descriptor spaces need not start at 0 and need not be contiguous — see
/// <see cref="SetLayoutSlotCount"/>.</para>
/// <para>Everything is computed once, eagerly, in the constructor; the spans
/// this type hands out are views over those arrays and stay valid for its
/// lifetime. Reflection is setup-time — the wrapper's zero-per-frame-allocation
/// invariant does not apply here and no benchmark covers it.</para>
/// </remarks>
public sealed unsafe class SlangReflection
{
    private readonly uint[] _setIndices;
    private readonly int[] _setStarts;
    private readonly DescriptorBinding[] _bindings;
    private readonly PushConstantRange[] _pushConstantRanges;
    private readonly SlangEntryPointInfo[] _entryPoints;
    private readonly VertexAttributeDescription[][] _vertexAttributes;

    internal SlangReflection(SlangProgram program, SlangStageAttribution attribution)
    {
        IComponentType* linked = program.LinkedComponent;
        SlangProgramLayout* layout = GetLayout(linked);

        // Entry points first: their stages are the program union, which every
        // other step needs, and PerEntryPointUsage indexes metadata by the
        // same entry-point index.
        int entryPointCount = (int)SlangApi.spReflection_getEntryPointCount(layout);

        _entryPoints = new SlangEntryPointInfo[entryPointCount];
        _vertexAttributes = new VertexAttributeDescription[entryPointCount][];

        ShaderStages programStages = ShaderStages.None;

        for (int i = 0; i < entryPointCount; i++)
        {
            SlangEntryPointLayout* entryPoint = SlangApi.spReflection_getEntryPointByIndex(layout, (ulong)i);
            string name = SlangUtf8.ToString(SlangApi.spReflectionEntryPoint_getName(entryPoint)) ?? string.Empty;
            ShaderStages stage = SlangStages.ToShaderStages(SlangApi.spReflectionEntryPoint_getStage(entryPoint));

            _entryPoints[i] = new SlangEntryPointInfo(name, stage);
            _vertexAttributes[i] = BuildVertexAttributes(entryPoint, stage);
            programStages |= stage;
        }

        var pending = new List<PendingBinding>();
        int pushConstantRanges = 0;

        // setOf(global scope) = 0. Not "the global scope owns set 0" — a
        // program whose global scope declares only ParameterBlocks has no
        // descriptors of its own, and its first block lands in set 0.
        Walk(
            SlangApi.spReflection_getGlobalParamsTypeLayout(layout),
            absoluteSet: 0,
            isParameterBlockElement: false,
            pending,
            ref pushConstantRanges);

        _pushConstantRanges = BuildPushConstantRanges(layout, pushConstantRanges, programStages);

        ApplyStages(linked, pending, _entryPoints, programStages, attribution);
        Group(pending, out _setIndices, out _setStarts, out _bindings);

        SetLayoutSlotCount = _setIndices.Length == 0 ? 0 : _setIndices[^1] + 1;
    }

    /// <summary>
    /// Number of <b>populated</b> descriptor sets. Not a set index bound — see
    /// <see cref="SetLayoutSlotCount"/>.
    /// </summary>
    public int DescriptorSetCount => _setIndices.Length;

    /// <summary>
    /// The length a <c>PipelineLayoutDescription.SetLayouts</c> span must have
    /// for this program: the highest declared set index plus one, or <c>0</c>
    /// when the program declares no descriptors.
    /// </summary>
    /// <remarks>
    /// <para><b>A Slang program's descriptor set indices need not be
    /// contiguous.</b> <c>PipelineLayoutDescription.SetLayouts</c> is
    /// positional, so a caller allocates <see cref="SetLayoutSlotCount"/>
    /// entries and asks <see cref="TryGetSet"/> for each index in turn:</para>
    /// <code>
    /// var layouts = new DescriptorSetLayout[(int)reflection.SetLayoutSlotCount];
    ///
    /// for (uint set = 0; set &lt; reflection.SetLayoutSlotCount; set++)
    /// {
    ///     if (reflection.TryGetSet(set, out ReadOnlySpan&lt;DescriptorBinding&gt; bindings))
    ///         layouts[set] = device.CreateDescriptorSetLayout(
    ///             new DescriptorSetLayoutDescription { Bindings = bindings });
    /// }
    /// </code>
    /// <para><b>A <see langword="false"/> from <see cref="TryGetSet"/> has no
    /// answer in the wrapper today.</b> Vulkan fills such a hole with a
    /// descriptor set layout that has zero bindings, but
    /// <c>Device.CreateDescriptorSetLayout</c> rejects an empty
    /// <c>Bindings</c> span outright, so there is no way to obtain one through
    /// this API — a reflected program that leaves a set index unused cannot
    /// currently be turned into a complete <c>PipelineLayout</c>. Closing that
    /// needs a decision in <c>Ahjo.Vulkan</c> itself and is deliberately not
    /// worked around here; producing a stand-in layout with an invented binding
    /// in it would be worse than the gap.</para>
    /// <para>The reflected set numbers are baked into the emitted SPIR-V.
    /// Renumbering them to be dense produces a pipeline layout that builds and
    /// then binds to the wrong slots at draw time.</para>
    /// </remarks>
    public uint SetLayoutSlotCount { get; }

    /// <summary>
    /// Push-constant ranges, or an empty span when the program declares none.
    /// </summary>
    /// <remarks>
    /// <c>Stages</c> is the union of the program's entry-point stages in
    /// <em>both</em> <see cref="SlangStageAttribution"/> modes:
    /// <c>IMetadata::isParameterLocationUsed</c> reports a push constant as
    /// unused even for a stage whose SPIR-V demonstrably contains a
    /// <c>PushConstant</c> variable reading it, under every parameter category
    /// and space swept, so there is no narrowing to be had.
    /// </remarks>
    public ReadOnlySpan<PushConstantRange> PushConstantRanges => _pushConstantRanges;

    /// <summary>Number of entry points in the linked program.</summary>
    public int EntryPointCount => _entryPoints.Length;

    /// <summary>
    /// The Vulkan set number of the <paramref name="index"/>-th populated set.
    /// Sets are ordered ascending by set number.
    /// </summary>
    public uint SetIndex(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _setIndices.Length);

        return _setIndices[index];
    }

    /// <summary>
    /// Bindings of the <paramref name="index"/>-th populated set, ascending by
    /// <c>Slot</c>. Pair it with <see cref="SetIndex"/> for the set number.
    /// </summary>
    public ReadOnlySpan<DescriptorBinding> Bindings(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _setIndices.Length);

        return _bindings.AsSpan(_setStarts[index], _setStarts[index + 1] - _setStarts[index]);
    }

    /// <summary>
    /// Bindings of Vulkan descriptor set <paramref name="setIndex"/>, or
    /// <see langword="false"/> when the program declares nothing in that set.
    /// </summary>
    /// <remarks>
    /// A <see langword="false"/> here is a gap the caller fills with an empty
    /// descriptor set layout; see <see cref="SetLayoutSlotCount"/>.
    /// </remarks>
    public bool TryGetSet(uint setIndex, out ReadOnlySpan<DescriptorBinding> bindings)
    {
        // Linear: setup-time, and the set count is single digits.
        for (int i = 0; i < _setIndices.Length; i++)
        {
            if (_setIndices[i] == setIndex)
            {
                bindings = Bindings(i);

                return true;
            }
        }

        bindings = default;

        return false;
    }

    /// <summary>
    /// The <paramref name="index"/>-th entry point's name and stage. Same
    /// index as <c>SlangProgram.Spirv</c> and
    /// <see cref="VertexAttributes"/>.
    /// </summary>
    public SlangEntryPointInfo EntryPoint(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _entryPoints.Length);

        return _entryPoints[index];
    }

    /// <summary>
    /// Vertex attributes declared by entry point
    /// <paramref name="entryPointIndex"/>, ascending by <c>Location</c>. Empty
    /// for any entry point whose stage is not <c>ShaderStages.Vertex</c>.
    /// </summary>
    /// <remarks>
    /// <para><b><c>Binding</c> and <c>Offset</c> are left at their defaults and
    /// the caller must fill them.</b> A shader states its input locations and
    /// formats but not how the application packs its vertex buffers, so
    /// <c>VertexAttributeDescription.Binding</c> / <c>.Offset</c> and every
    /// field of <c>VertexBindingDescription</c> are information reflection
    /// simply does not have. There is deliberately no
    /// <c>VertexInputDescription</c> factory here; composition does not change
    /// this, because nothing in a composite says anything about the
    /// application's buffer layout either.</para>
    /// <para>System-value inputs (<c>SV_VertexID</c>, <c>SV_InstanceID</c>,
    /// <c>SV_IsFrontFace</c>, <c>SV_Position</c>) are excluded: they report
    /// parameter category <c>NONE</c>, and emitting them would produce a
    /// phantom attribute at location 0 colliding with the real
    /// <c>POSITION</c>.</para>
    /// </remarks>
    /// <exception cref="NotSupportedException">
    /// The entry point takes a matrix-typed vertex input, whose per-location
    /// component count is not derivable — see the exception text.
    /// </exception>
    public ReadOnlySpan<VertexAttributeDescription> VertexAttributes(int entryPointIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(entryPointIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(entryPointIndex, _vertexAttributes.Length);

        return _vertexAttributes[entryPointIndex];
    }

    /// <summary>
    /// Maps a Slang descriptor-range binding type to the Vulkan descriptor
    /// type. Total: there is no default case, because a wrong
    /// <c>VkDescriptorType</c> is a validation error the caller cannot trace
    /// back to here.
    /// </summary>
    internal static VkDescriptorType MapBindingType(SlangBindingType type)
    {
        bool mutable = (type & SlangBindingType.SLANG_BINDING_TYPE_MUTABLE_FLAG) != 0;

        return (type & SlangBindingType.SLANG_BINDING_TYPE_BASE_MASK) switch
        {
            SlangBindingType.SLANG_BINDING_TYPE_SAMPLER
                => VkDescriptorType.VK_DESCRIPTOR_TYPE_SAMPLER,
            SlangBindingType.SLANG_BINDING_TYPE_TEXTURE
                => mutable
                    ? VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_IMAGE
                    : VkDescriptorType.VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE,
            SlangBindingType.SLANG_BINDING_TYPE_CONSTANT_BUFFER
                => VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER,
            SlangBindingType.SLANG_BINDING_TYPE_TYPED_BUFFER
                => mutable
                    ? VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_TEXEL_BUFFER
                    : VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_TEXEL_BUFFER,
            SlangBindingType.SLANG_BINDING_TYPE_RAW_BUFFER
                => VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER,
            SlangBindingType.SLANG_BINDING_TYPE_COMBINED_TEXTURE_SAMPLER
                => VkDescriptorType.VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER,
            SlangBindingType.SLANG_BINDING_TYPE_INPUT_RENDER_TARGET
                => VkDescriptorType.VK_DESCRIPTOR_TYPE_INPUT_ATTACHMENT,
            SlangBindingType.SLANG_BINDING_TYPE_INLINE_UNIFORM_DATA
                => VkDescriptorType.VK_DESCRIPTOR_TYPE_INLINE_UNIFORM_BLOCK,
            SlangBindingType.SLANG_BINDING_TYPE_RAY_TRACING_ACCELERATION_STRUCTURE
                => VkDescriptorType.VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR,

            // A ParameterBlock is a descriptor *space*, not a descriptor. The
            // walk filters it out and recurses into its element; reaching here
            // means the walk did not, and mapping it to a uniform buffer would
            // put a phantom binding in the parent set on top of the real
            // implicit one synthesized in the child.
            SlangBindingType.SLANG_BINDING_TYPE_PARAMETER_BLOCK
                => throw new NotSupportedException(
                    "A SLANG_BINDING_TYPE_PARAMETER_BLOCK range reached the descriptor-type mapping. "
                    + "Parameter blocks are recursed into, never mapped — this is a bug in the reflection walk, not in the shader."),

            _ => throw new NotSupportedException(
                $"Slang binding type {type} has no VkDescriptorType mapping."),
        };
    }

    /// <summary>
    /// Walks one struct-shaped scope — the global scope, or a
    /// <c>ParameterBlock</c>'s element type — collecting its descriptor ranges
    /// and recursing into the parameter blocks it contains.
    /// </summary>
    /// <param name="structTypeLayout">The scope's type layout.</param>
    /// <param name="absoluteSet">
    /// The Vulkan set number this scope's own descriptors live in.
    /// </param>
    /// <param name="isParameterBlockElement">
    /// <see langword="false"/> for the global scope only. Gates the synthesized
    /// binding 0 in step 2 — the asymmetry is real and applying it to both
    /// double-counts.
    /// </param>
    private static void Walk(
        SlangReflectionTypeLayout* structTypeLayout,
        uint absoluteSet,
        bool isParameterBlockElement,
        List<PendingBinding> pending,
        ref int pushConstantRanges)
    {
        // ---- Step 1: this scope's own descriptor sets. ----
        long setCount = SlangApi.spReflectionTypeLayout_getDescriptorSetCount(structTypeLayout);

        for (long s = 0; s < setCount; s++)
        {
            // The loop index is NOT the Vulkan set number. A program with
            // [[vk::binding(3, 0)]] and [[vk::binding(7, 2)]] reports two
            // descriptor sets whose space offsets are 0 and 2, matching the
            // emitted SPIR-V; the loop index for the second is 1.
            long spaceOffset = SlangApi.spReflectionTypeLayout_getDescriptorSetSpaceOffset(structTypeLayout, s);

            if (spaceOffset < 0)
            {
                throw new NotSupportedException(
                    $"Slang reported descriptor set {s} of this scope at space offset {spaceOffset}, which is not a "
                    + "Vulkan descriptor set number. This is the sentinel Slang returns when a layout depends on "
                    + "unresolved generic parameters or link-time constants; reflect a fully specialized program.");
            }

            uint vkSet = absoluteSet + (uint)spaceOffset;
            long rangeCount = SlangApi.spReflectionTypeLayout_getDescriptorSetDescriptorRangeCount(structTypeLayout, s);

            for (long r = 0; r < rangeCount; r++)
            {
                SlangParameterCategory category =
                    SlangApi.spReflectionTypeLayout_getDescriptorSetDescriptorRangeCategory(structTypeLayout, s, r);

                if (category == SlangParameterCategory.SLANG_PARAMETER_CATEGORY_PUSH_CONSTANT_BUFFER)
                {
                    // Push constants arrive as a descriptor range. They are not
                    // descriptors; step 4 turns them into PushConstantRanges.
                    pushConstantRanges++;

                    continue;
                }

                long slot = SlangApi.spReflectionTypeLayout_getDescriptorSetDescriptorRangeIndexOffset(structTypeLayout, s, r);
                long count = SlangApi.spReflectionTypeLayout_getDescriptorSetDescriptorRangeDescriptorCount(structTypeLayout, s, r);

                // SLANG_UNBOUNDED_SIZE (~(size_t)0) and SLANG_UNKNOWN_SIZE
                // (that minus one) are documented returns of both calls, and
                // reach here as -1 / -2. Casting either to uint yields
                // 4294967295 or 4294967294 — a driver crash at
                // vkCreateDescriptorSetLayout, not a validation message.
                if (slot < 0 || slot > uint.MaxValue)
                {
                    throw new NotSupportedException(
                        $"Descriptor range {r} of descriptor set {vkSet} reports index offset {slot}. Slang returns "
                        + "this sentinel when the offset depends on unresolved generic parameters or link-time "
                        + "constants; there is no binding number to emit. Reflect a fully specialized program.");
                }

                if (count < 0 || count > uint.MaxValue)
                {
                    throw new NotSupportedException(
                        $"Descriptor range {r} of descriptor set {vkSet} reports descriptor count {count}. That is "
                        + "Slang's sentinel for an unbounded array, or for a count that depends on unresolved "
                        + "generic parameters or link-time constants. Bindless arrays need an explicit Count plus "
                        + "DescriptorBindingFlags.VariableDescriptorCount, which reflection cannot choose for you.");
                }

                SlangBindingType bindingType =
                    SlangApi.spReflectionTypeLayout_getDescriptorSetDescriptorRangeType(structTypeLayout, s, r);

                pending.Add(new PendingBinding(
                    vkSet,
                    new DescriptorBinding
                    {
                        Slot = (uint)slot,
                        Count = (uint)count,
                        Type = MapBindingType(bindingType),
                    }));
            }
        }

        // ---- Step 2: the block's implicit uniform buffer. ----
        //
        // When a ParameterBlock's element type has ordinary (uniform) data,
        // Slang allocates an implicit uniform buffer at binding 0 of the
        // block's space and shifts every listed range up by one — but emits no
        // descriptor range for that buffer. A layout built only from the listed
        // ranges is missing a binding and the pipeline is invalid at bind time.
        //
        // The global scope does NOT share this asymmetry: its implicit constant
        // buffer *is* listed, as a CONSTANT_BUFFER range at index 0, so
        // applying this there would double-count binding 0.
        if (isParameterBlockElement
            && SlangApi.spReflectionTypeLayout_GetSize(
                structTypeLayout, SlangParameterCategory.SLANG_PARAMETER_CATEGORY_UNIFORM) > 0)
        {
            pending.Add(new PendingBinding(
                absoluteSet,
                new DescriptorBinding
                {
                    Slot = 0,
                    Count = 1,

                    // By construction, not through MapBindingType — there is no
                    // Slang binding type for a range Slang does not report.
                    Type = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER,
                }));
        }

        // ---- Step 3: recurse into ParameterBlocks. ----
        long subObjectRangeCount = SlangApi.spReflectionTypeLayout_getSubObjectRangeCount(structTypeLayout);

        for (long i = 0; i < subObjectRangeCount; i++)
        {
            long bindingRange = SlangApi.spReflectionTypeLayout_getSubObjectRangeBindingRangeIndex(structTypeLayout, i);

            // The sub-object range list also contains constant buffers, raw
            // buffers and push-constant buffers, all of which step 1 already
            // handled as descriptor ranges of this scope.
            if (SlangApi.spReflectionTypeLayout_getBindingRangeType(structTypeLayout, bindingRange)
                != SlangBindingType.SLANG_BINDING_TYPE_PARAMETER_BLOCK)
            {
                continue;
            }

            SlangReflectionTypeLayout* blockTypeLayout =
                SlangApi.spReflectionTypeLayout_getBindingRangeLeafTypeLayout(structTypeLayout, bindingRange);
            SlangReflectionVariableLayout* offsetVariable =
                SlangApi.spReflectionTypeLayout_getSubObjectRangeOffset(structTypeLayout, i);

            // NEVER spReflectionTypeLayout_getSubObjectRangeSpaceOffset here.
            // It is the wrong function and returns 0 for every sub-object
            // range, including blocks the emitted SPIR-V demonstrably places in
            // spaces 1 and 2. The space offset is on the range's variable
            // layout, under SUB_ELEMENT_REGISTER_SPACE, and it is an offset
            // relative to the enclosing scope — which is why it accumulates
            // rather than being read absolutely.
            nuint spaceOffset = SlangApi.spReflectionVariableLayout_GetOffset(
                offsetVariable, SlangParameterCategory.SLANG_PARAMETER_CATEGORY_SUB_ELEMENT_REGISTER_SPACE);

            if (spaceOffset > uint.MaxValue)
            {
                throw new NotSupportedException(
                    $"Sub-object range {i} reports a descriptor space offset of {spaceOffset}, which is Slang's "
                    + "sentinel for an offset that depends on unresolved generic parameters or link-time constants. "
                    + "Reflect a fully specialized program.");
            }

            Walk(
                SlangApi.spReflectionTypeLayout_GetElementTypeLayout(blockTypeLayout),
                absoluteSet + (uint)spaceOffset,
                isParameterBlockElement: true,
                pending,
                ref pushConstantRanges);
        }
    }

    /// <summary>
    /// Step 4 — turns the push-constant ranges step 1 counted into
    /// <c>PushConstantRange</c> values, whose byte size comes from the
    /// declaring parameter rather than from the range.
    /// </summary>
    private static PushConstantRange[] BuildPushConstantRanges(
        SlangProgramLayout* layout,
        int pushConstantRangeCount,
        ShaderStages programStages)
    {
        if (pushConstantRangeCount == 0)
        {
            return [];
        }

        uint parameterCount = SlangApi.spReflection_GetParameterCount(layout);
        SlangReflectionTypeLayout* found = null;
        string firstName = string.Empty;
        string secondName = string.Empty;
        int matches = 0;

        for (uint i = 0; i < parameterCount; i++)
        {
            SlangReflectionVariableLayout* parameter = SlangApi.spReflection_GetParameterByIndex(layout, i);
            SlangReflectionTypeLayout* typeLayout = SlangApi.spReflectionVariableLayout_GetTypeLayout(parameter);

            if (SlangApi.spReflectionTypeLayout_GetParameterCategory(typeLayout)
                != SlangParameterCategory.SLANG_PARAMETER_CATEGORY_PUSH_CONSTANT_BUFFER)
            {
                continue;
            }

            matches++;

            if (matches == 1)
            {
                found = typeLayout;
                firstName = NameOf(parameter);
            }
            else if (matches == 2)
            {
                secondName = NameOf(parameter);
            }
        }

        if (matches > 1)
        {
            // OPEN-5. Two [[vk::push_constant]] blocks compose and link, and
            // reflection reports two PUSH_CONSTANT ranges — but the only offset
            // it exposes for them is a push-constant *buffer index* (0, 1, …),
            // while VkPushConstantRange.offset is a byte offset into a single
            // shared push-constant block. Nothing probed yields those bytes.
            // Emitting 0 for both would produce two overlapping ranges; guessing
            // a packing order would produce a layout that mismatches the SPIR-V
            // as soon as Slang changed its own.
            throw new NotSupportedException(
                $"This Slang program declares {matches} push-constant blocks ('{firstName}', '{secondName}'…). "
                + "Slang reflection exposes only a push-constant buffer index for each of them, not the byte offset "
                + "VkPushConstantRange.Offset needs, so a correct set of ranges is not derivable here and a guessed "
                + "one would mismatch the emitted SPIR-V (issue #166, OPEN-5). Declare a single [[vk::push_constant]] "
                + "block across the composed program, or build the PushConstantRange values by hand.");
        }

        if (matches == 0 || found == null)
        {
            throw new NotSupportedException(
                $"Slang reported {pushConstantRangeCount} push-constant descriptor range(s) but no global parameter "
                + "of category PUSH_CONSTANT_BUFFER to take the block's byte size from. Without a size there is no "
                + "VkPushConstantRange to build.");
        }

        nuint size = SlangApi.spReflectionTypeLayout_GetSize(
            SlangApi.spReflectionTypeLayout_GetElementTypeLayout(found),
            SlangParameterCategory.SLANG_PARAMETER_CATEGORY_UNIFORM);

        if (size == 0 || size > uint.MaxValue)
        {
            throw new NotSupportedException(
                $"Push-constant block '{firstName}' reports a uniform size of {size}, which is not a byte count "
                + "VkPushConstantRange.Size can carry. Reflect a fully specialized program.");
        }

        // Offset 0: with exactly one block there is nothing to offset past, and
        // Stages is the program union in both attribution modes — see the
        // PushConstantRanges remarks.
        return [new PushConstantRange { Stages = programStages, Offset = 0, Size = (uint)size }];
    }

    /// <summary>
    /// Step 5 — fills <c>DescriptorBinding.Stages</c> on everything the walk
    /// collected.
    /// </summary>
    private static void ApplyStages(
        IComponentType* linked,
        List<PendingBinding> pending,
        SlangEntryPointInfo[] entryPoints,
        ShaderStages programStages,
        SlangStageAttribution attribution)
    {
        if (attribution == SlangStageAttribution.ProgramStageUnion)
        {
            for (int i = 0; i < pending.Count; i++)
            {
                PendingBinding binding = pending[i];

                pending[i] = binding with { Binding = binding.Binding with { Stages = programStages } };
            }

            return;
        }

        var used = new ShaderStages[pending.Count];

        for (int e = 0; e < entryPoints.Length; e++)
        {
            IMetadata* metadata = null;
            ISlangBlob* diagnostics = null;

            // Carries the same preconditions as getEntryPointCode: this is a
            // code generation, which is why precise stages are opt-in and why
            // this mode can throw where ProgramStageUnion cannot.
            int rc = linked->getEntryPointMetadata(e, 0, &metadata, &diagnostics);
            string text = SlangUtf8.TakeDiagnostics(&diagnostics);

            if (rc < 0 || metadata == null)
            {
                throw new SlangCompilationException(
                    $"getEntryPointMetadata({e}) for entry point '{entryPoints[e].Name}' (0x{rc:X8})",
                    text);
            }

            try
            {
                ShaderStages stage = entryPoints[e].Stage;

                for (int i = 0; i < pending.Count; i++)
                {
                    bool isUsed = false;
                    int queryRc = metadata->isParameterLocationUsed(
                        SlangParameterCategory.SLANG_PARAMETER_CATEGORY_DESCRIPTOR_TABLE_SLOT,
                        pending[i].Set,
                        pending[i].Binding.Slot,
                        &isUsed);

                    // A failed query is treated as "no information", not as
                    // "unused": the fallback below turns a binding no entry
                    // point claimed into the program union, which is always a
                    // legal stageFlags value.
                    if (queryRc >= 0 && isUsed)
                    {
                        used[i] |= stage;
                    }
                }
            }
            finally
            {
                metadata->release();
            }
        }

        for (int i = 0; i < pending.Count; i++)
        {
            // ShaderStages.None is not a usable
            // VkDescriptorSetLayoutBinding.stageFlags — no stage could ever
            // access the descriptor. Usage is reported post-optimization, so a
            // binding nothing touched lands here legitimately.
            ShaderStages stages = used[i] == ShaderStages.None ? programStages : used[i];
            PendingBinding binding = pending[i];

            pending[i] = binding with { Binding = binding.Binding with { Stages = stages } };
        }
    }

    /// <summary>
    /// Step 6 — sorts by set then slot and slices the flat binding array per
    /// populated set.
    /// </summary>
    private static void Group(
        List<PendingBinding> pending,
        out uint[] setIndices,
        out int[] setStarts,
        out DescriptorBinding[] bindings)
    {
        if (pending.Count == 0)
        {
            setIndices = [];
            setStarts = [0];
            bindings = [];

            return;
        }

        PendingBinding[] sorted = [.. pending];

        Array.Sort(sorted, static (a, b) => a.Set != b.Set
            ? a.Set.CompareTo(b.Set)
            : a.Binding.Slot.CompareTo(b.Binding.Slot));

        int distinctSets = 1;

        for (int i = 1; i < sorted.Length; i++)
        {
            if (sorted[i].Set != sorted[i - 1].Set)
            {
                distinctSets++;
            }
        }

        setIndices = new uint[distinctSets];
        setStarts = new int[distinctSets + 1];
        bindings = new DescriptorBinding[sorted.Length];

        int set = -1;

        for (int i = 0; i < sorted.Length; i++)
        {
            if (set < 0 || sorted[i].Set != setIndices[set])
            {
                set++;
                setIndices[set] = sorted[i].Set;
                setStarts[set] = i;
            }

            bindings[i] = sorted[i].Binding;
        }

        setStarts[distinctSets] = sorted.Length;
    }

    private static VertexAttributeDescription[] BuildVertexAttributes(SlangEntryPointLayout* entryPoint, ShaderStages stage)
    {
        // A fragment stage's struct input is VARYING_INPUT too; only a vertex
        // stage's inputs are VkVertexInputAttributeDescriptions.
        if (stage != ShaderStages.Vertex)
        {
            return [];
        }

        uint parameterCount = SlangApi.spReflectionEntryPoint_getParameterCount(entryPoint);
        List<VertexAttributeDescription>? attributes = null;

        for (uint i = 0; i < parameterCount; i++)
        {
            SlangReflectionVariableLayout* parameter = SlangApi.spReflectionEntryPoint_getParameterByIndex(entryPoint, i);
            SlangReflectionTypeLayout* typeLayout = SlangApi.spReflectionVariableLayout_GetTypeLayout(parameter);

            // The filter that keeps SV_InstanceID, SV_VertexID, SV_IsFrontFace
            // and SV_Position out: they all report category NONE, and without
            // this an SV_InstanceID emits a phantom attribute at location 0
            // that collides with the real POSITION.
            if (SlangApi.spReflectionTypeLayout_GetParameterCategory(typeLayout)
                != SlangParameterCategory.SLANG_PARAMETER_CATEGORY_VARYING_INPUT)
            {
                continue;
            }

            uint baseLocation = (uint)SlangApi.spReflectionVariableLayout_GetOffset(
                parameter, SlangParameterCategory.SLANG_PARAMETER_CATEGORY_VARYING_INPUT);

            if (SlangApi.spReflectionTypeLayout_getKind(typeLayout) == SlangTypeKind.SLANG_TYPE_KIND_STRUCT)
            {
                // One level of recursion, and locations accumulate: a field's
                // own VARYING_INPUT offset is relative to the struct.
                uint fieldCount = SlangApi.spReflectionTypeLayout_GetFieldCount(typeLayout);

                for (uint f = 0; f < fieldCount; f++)
                {
                    SlangReflectionVariableLayout* field = SlangApi.spReflectionTypeLayout_GetFieldByIndex(typeLayout, f);
                    SlangReflectionTypeLayout* fieldTypeLayout = SlangApi.spReflectionVariableLayout_GetTypeLayout(field);

                    if (SlangApi.spReflectionTypeLayout_GetParameterCategory(fieldTypeLayout)
                        != SlangParameterCategory.SLANG_PARAMETER_CATEGORY_VARYING_INPUT)
                    {
                        continue;
                    }

                    uint location = baseLocation + (uint)SlangApi.spReflectionVariableLayout_GetOffset(
                        field, SlangParameterCategory.SLANG_PARAMETER_CATEGORY_VARYING_INPUT);

                    (attributes ??= []).Add(new VertexAttributeDescription
                    {
                        Location = location,
                        Format = MapVertexFormat(fieldTypeLayout, NameOf(field)),
                    });
                }
            }
            else
            {
                (attributes ??= []).Add(new VertexAttributeDescription
                {
                    Location = baseLocation,
                    Format = MapVertexFormat(typeLayout, NameOf(parameter)),
                });
            }
        }

        if (attributes is null)
        {
            return [];
        }

        VertexAttributeDescription[] result = [.. attributes];

        Array.Sort(result, static (a, b) => a.Location.CompareTo(b.Location));

        return result;
    }

    private static VkFormat MapVertexFormat(SlangReflectionTypeLayout* typeLayout, string name)
    {
        SlangTypeKind kind = SlangApi.spReflectionTypeLayout_getKind(typeLayout);
        SlangReflectionType* type = SlangApi.spReflectionTypeLayout_GetType(typeLayout);

        uint components;
        SlangScalarType scalar;

        switch (kind)
        {
            case SlangTypeKind.SLANG_TYPE_KIND_VECTOR:
                components = (uint)SlangApi.spReflectionType_GetElementCount(type);
                scalar = SlangApi.spReflectionType_GetScalarType(SlangApi.spReflectionType_GetElementType(type));

                break;

            case SlangTypeKind.SLANG_TYPE_KIND_SCALAR:
                components = 1;
                scalar = SlangApi.spReflectionType_GetScalarType(type);

                break;

            case SlangTypeKind.SLANG_TYPE_KIND_MATRIX:
                // OPEN-6. A matrix input occupies GetSize(typeLayout,
                // VARYING_INPUT) consecutive locations and SPIR-V decorates it
                // at the base location — but which scalar count each of those
                // locations carries depends on the session's
                // defaultMatrixLayoutMode, and only column-major has been
                // measured against the emitted SPIR-V. A VkFormat emitted here
                // would be a guess that silently mis-describes the other mode,
                // which is a wrong vertex fetch rather than a validation error.
                throw new NotSupportedException(
                    $"Vertex input '{name}' is a matrix ({SlangApi.spReflectionType_GetRowCount(type)}x"
                    + $"{SlangApi.spReflectionType_GetColumnCount(type)}). It occupies "
                    + $"{SlangApi.spReflectionTypeLayout_GetSize(typeLayout, SlangParameterCategory.SLANG_PARAMETER_CATEGORY_VARYING_INPUT)} "
                    + "consecutive vertex-input locations, but the per-location component count depends on the "
                    + "session's default matrix layout mode and only column-major has been verified against the "
                    + "emitted SPIR-V, so the VkFormat for each location is not derivable here (issue #166, OPEN-6). "
                    + "Declare the input as separate vector-typed fields, or fill VertexAttributeDescription by hand "
                    + "for this entry point.");

            default:
                throw new NotSupportedException(
                    $"Vertex input '{name}' has type kind {kind}, which has no VkFormat mapping. Vertex attributes "
                    + "are scalars and vectors.");
        }

        VkFormat format = MapScalarFormat(scalar, components);

        return format != VkFormat.VK_FORMAT_UNDEFINED
            ? format
            : throw new NotSupportedException(
                $"Vertex input '{name}' is {components} x {scalar}, which has no VkFormat mapping.");
    }

    /// <summary>
    /// <c>(scalar type, component count)</c> to <c>VkFormat</c>. Returns
    /// <c>VK_FORMAT_UNDEFINED</c> for a combination with no Vulkan format, so
    /// the caller can throw naming the field.
    /// </summary>
    private static VkFormat MapScalarFormat(SlangScalarType scalar, uint components) => (scalar, components) switch
    {
        (SlangScalarType.SLANG_SCALAR_TYPE_FLOAT32, 1) => VkFormat.VK_FORMAT_R32_SFLOAT,
        (SlangScalarType.SLANG_SCALAR_TYPE_FLOAT32, 2) => VkFormat.VK_FORMAT_R32G32_SFLOAT,
        (SlangScalarType.SLANG_SCALAR_TYPE_FLOAT32, 3) => VkFormat.VK_FORMAT_R32G32B32_SFLOAT,
        (SlangScalarType.SLANG_SCALAR_TYPE_FLOAT32, 4) => VkFormat.VK_FORMAT_R32G32B32A32_SFLOAT,

        (SlangScalarType.SLANG_SCALAR_TYPE_INT32, 1) => VkFormat.VK_FORMAT_R32_SINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_INT32, 2) => VkFormat.VK_FORMAT_R32G32_SINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_INT32, 3) => VkFormat.VK_FORMAT_R32G32B32_SINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_INT32, 4) => VkFormat.VK_FORMAT_R32G32B32A32_SINT,

        (SlangScalarType.SLANG_SCALAR_TYPE_UINT32, 1) => VkFormat.VK_FORMAT_R32_UINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_UINT32, 2) => VkFormat.VK_FORMAT_R32G32_UINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_UINT32, 3) => VkFormat.VK_FORMAT_R32G32B32_UINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_UINT32, 4) => VkFormat.VK_FORMAT_R32G32B32A32_UINT,

        (SlangScalarType.SLANG_SCALAR_TYPE_FLOAT16, 1) => VkFormat.VK_FORMAT_R16_SFLOAT,
        (SlangScalarType.SLANG_SCALAR_TYPE_FLOAT16, 2) => VkFormat.VK_FORMAT_R16G16_SFLOAT,
        (SlangScalarType.SLANG_SCALAR_TYPE_FLOAT16, 3) => VkFormat.VK_FORMAT_R16G16B16_SFLOAT,
        (SlangScalarType.SLANG_SCALAR_TYPE_FLOAT16, 4) => VkFormat.VK_FORMAT_R16G16B16A16_SFLOAT,

        (SlangScalarType.SLANG_SCALAR_TYPE_INT16, 1) => VkFormat.VK_FORMAT_R16_SINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_INT16, 2) => VkFormat.VK_FORMAT_R16G16_SINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_INT16, 3) => VkFormat.VK_FORMAT_R16G16B16_SINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_INT16, 4) => VkFormat.VK_FORMAT_R16G16B16A16_SINT,

        (SlangScalarType.SLANG_SCALAR_TYPE_UINT16, 1) => VkFormat.VK_FORMAT_R16_UINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_UINT16, 2) => VkFormat.VK_FORMAT_R16G16_UINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_UINT16, 3) => VkFormat.VK_FORMAT_R16G16B16_UINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_UINT16, 4) => VkFormat.VK_FORMAT_R16G16B16A16_UINT,

        (SlangScalarType.SLANG_SCALAR_TYPE_INT8, 1) => VkFormat.VK_FORMAT_R8_SINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_INT8, 2) => VkFormat.VK_FORMAT_R8G8_SINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_INT8, 3) => VkFormat.VK_FORMAT_R8G8B8_SINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_INT8, 4) => VkFormat.VK_FORMAT_R8G8B8A8_SINT,

        (SlangScalarType.SLANG_SCALAR_TYPE_UINT8, 1) => VkFormat.VK_FORMAT_R8_UINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_UINT8, 2) => VkFormat.VK_FORMAT_R8G8_UINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_UINT8, 3) => VkFormat.VK_FORMAT_R8G8B8_UINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_UINT8, 4) => VkFormat.VK_FORMAT_R8G8B8A8_UINT,

        (SlangScalarType.SLANG_SCALAR_TYPE_FLOAT64, 1) => VkFormat.VK_FORMAT_R64_SFLOAT,
        (SlangScalarType.SLANG_SCALAR_TYPE_FLOAT64, 2) => VkFormat.VK_FORMAT_R64G64_SFLOAT,
        (SlangScalarType.SLANG_SCALAR_TYPE_FLOAT64, 3) => VkFormat.VK_FORMAT_R64G64B64_SFLOAT,
        (SlangScalarType.SLANG_SCALAR_TYPE_FLOAT64, 4) => VkFormat.VK_FORMAT_R64G64B64A64_SFLOAT,

        (SlangScalarType.SLANG_SCALAR_TYPE_INT64, 1) => VkFormat.VK_FORMAT_R64_SINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_INT64, 2) => VkFormat.VK_FORMAT_R64G64_SINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_INT64, 3) => VkFormat.VK_FORMAT_R64G64B64_SINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_INT64, 4) => VkFormat.VK_FORMAT_R64G64B64A64_SINT,

        (SlangScalarType.SLANG_SCALAR_TYPE_UINT64, 1) => VkFormat.VK_FORMAT_R64_UINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_UINT64, 2) => VkFormat.VK_FORMAT_R64G64_UINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_UINT64, 3) => VkFormat.VK_FORMAT_R64G64B64_UINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_UINT64, 4) => VkFormat.VK_FORMAT_R64G64B64A64_UINT,

        _ => VkFormat.VK_FORMAT_UNDEFINED,
    };

    private static SlangProgramLayout* GetLayout(IComponentType* linked)
    {
        ISlangBlob* diagnostics = null;
        var layout = (SlangProgramLayout*)linked->getLayout(0, &diagnostics);
        string text = SlangUtf8.TakeDiagnostics(&diagnostics);

        return layout != null
            ? layout
            : throw new SlangCompilationException("IComponentType::getLayout", text);
    }

    private static string NameOf(SlangReflectionVariableLayout* variableLayout)
        => SlangUtf8.ToString(
            SlangApi.spReflectionVariable_GetName(
                SlangApi.spReflectionVariableLayout_GetVariable(variableLayout)))
            ?? string.Empty;

    /// <summary>
    /// A binding plus the Vulkan set it landed in, before
    /// <c>Stages</c> is resolved and before grouping.
    /// </summary>
    private readonly record struct PendingBinding(uint Set, DescriptorBinding Binding);
}
