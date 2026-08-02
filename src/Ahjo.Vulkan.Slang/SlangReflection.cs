using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Slang.Internal;
using Ahjo.Vulkan.Slang.Native;

namespace Ahjo.Vulkan.Slang;

/// <summary>
/// The binding surface of a linked Slang program, expressed in the wrapper's
/// own description types — <c>SlangDescriptorBinding</c>, <c>SlangPushConstantRange</c>
/// and <c>SlangVertexAttributeDescription</c>.
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
/// <para><b>An unbounded (bindless) array is reported, not refused.</b> A
/// descriptor range whose count is one of Slang's sentinels lands here as a
/// <see cref="SlangDescriptorCount"/> whose <c>Kind</c> is
/// <c>Unbounded</c> or <c>Unknown</c>, and the rest of the program — the other
/// sets, the push-constant ranges, the vertex attributes — is reported as
/// usual. The capacity decision lives in <c>SlangVulkanMapping</c>, which
/// refuses the binding or takes the capacity as a parameter.</para>
/// <para>Everything is computed once, eagerly, in the constructor; the spans
/// this type hands out are views over those arrays and stay valid for its
/// lifetime. Reflection is setup-time — the wrapper's zero-per-frame-allocation
/// invariant does not apply here and no benchmark covers it.</para>
/// </remarks>
public sealed unsafe class SlangReflection
{
    private readonly uint[] _setIndices;
    private readonly int[] _setStarts;
    private readonly SlangDescriptorBinding[] _bindings;
    private readonly SlangPushConstantRange[] _pushConstantRanges;
    private readonly SlangEntryPointInfo[] _entryPoints;
    private readonly SlangVertexAttributeDescription[][] _vertexAttributes;

    internal SlangReflection(SlangProgram program, SlangStageAttribution attribution)
    {
        IComponentType* linked = program.LinkedComponent;
        SlangProgramLayout* layout = GetLayout(linked);

        // Entry points first: their stages are the program union, which every
        // other step needs, and PerEntryPointUsage indexes metadata by the
        // same entry-point index.
        int entryPointCount = (int)SlangApi.spReflection_getEntryPointCount(layout);

        _entryPoints = new SlangEntryPointInfo[entryPointCount];
        _vertexAttributes = new SlangVertexAttributeDescription[entryPointCount][];

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
    ///     if (reflection.TryGetSet(set, out ReadOnlySpan&lt;SlangDescriptorBinding&gt; bindings))
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
    public ReadOnlySpan<SlangPushConstantRange> PushConstantRanges => _pushConstantRanges;

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
    public ReadOnlySpan<SlangDescriptorBinding> Bindings(int index)
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
    public bool TryGetSet(uint setIndex, out ReadOnlySpan<SlangDescriptorBinding> bindings)
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
    /// <para>This reports the input's <c>Location</c> and its declared Slang
    /// type. Use <c>SlangVulkanMapping.MapVertexAttribute</c> to resolve that
    /// into a <c>VertexAttributeDescription</c> with a <c>VkFormat</c>; that is
    /// also where <c>binding</c> and <c>offset</c> are supplied, because a
    /// shader states its input locations and formats but not how the
    /// application packs its vertex buffers. Those two and every field of
    /// <c>VertexBindingDescription</c> are information reflection simply does
    /// not have. There is deliberately no <c>VertexInputDescription</c> factory
    /// here; composition does not change this, because nothing in a composite
    /// says anything about the application's buffer layout either.</para>
    /// <para>System-value inputs (<c>SV_VertexID</c>, <c>SV_InstanceID</c>,
    /// <c>SV_IsFrontFace</c>, <c>SV_Position</c>) are excluded: they report
    /// parameter category <c>NONE</c>, and emitting them would produce a
    /// phantom attribute at location 0 colliding with the real
    /// <c>POSITION</c>.</para>
    /// <para>A matrix-typed vertex input is reported here rather than refused;
    /// it is <c>MapVertexAttribute</c> that throws
    /// <c>NotSupportedException</c> for it, since only the mapping to a
    /// <c>VkFormat</c> needs the per-location component count that is not
    /// derivable.</para>
    /// </remarks>
    public ReadOnlySpan<SlangVertexAttributeDescription> VertexAttributes(int entryPointIndex)
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
                //
                // The *count* sentinels are classified rather than refused
                // (issue #176): an unsized count still leaves a perfectly
                // usable (set, slot, type), and the capacity decision belongs
                // to SlangVulkanMapping. The *index offset* sentinel below —
                // and the sub-object space offset sentinel in step 3 — stay
                // all-or-nothing, because a binding with no binding number and
                // a scope with no set number have no layout to report at all.
                if (slot < 0 || slot > uint.MaxValue)
                {
                    throw new NotSupportedException(
                        $"Descriptor range {r} of descriptor set {vkSet} reports index offset {slot}. Slang returns "
                        + "this sentinel when the offset depends on unresolved generic parameters or link-time "
                        + "constants; there is no binding number to emit. Reflect a fully specialized program.");
                }

                // Measured on v2026.14.1 / win-x64: three unbounded arrays in
                // one space report -1 here, which is SLANG_UNBOUNDED_SIZE
                // through the long-returning binding
                // (Reflection_UnboundedArray_ReportsBindingInsteadOfThrowing).
                SlangDescriptorCount descriptorCount = count switch
                {
                    -1 => SlangDescriptorCount.Unbounded,
                    -2 => SlangDescriptorCount.Unknown,
                    >= 0 and <= uint.MaxValue => SlangDescriptorCount.Fixed((uint)count),
                    _ => throw new NotSupportedException(
                        $"Descriptor range {r} of descriptor set {vkSet} reports descriptor count {count}, which is "
                        + "neither a descriptor count nor one of Slang's documented sentinels "
                        + "(`SLANG_UNBOUNDED_SIZE` = -1, `SLANG_UNKNOWN_SIZE` = -2). Casting it to a `uint` would "
                        + "hand `vkCreateDescriptorSetLayout` a nonsense `descriptorCount`."),
                };

                SlangBindingType bindingType =
                    SlangApi.spReflectionTypeLayout_getDescriptorSetDescriptorRangeType(structTypeLayout, s, r);

                pending.Add(new PendingBinding(
                    vkSet,
                    new SlangDescriptorBinding
                    {
                        Slot = (uint)slot,
                        Count = descriptorCount,
                        Type = bindingType,
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
                new SlangDescriptorBinding
                {
                    Slot = 0,
                    Count = SlangDescriptorCount.Fixed(1),

                    // By construction, not through MapBindingType — there is no
                    // Slang binding type for a range Slang does not report.
                    Type = SlangBindingType.SLANG_BINDING_TYPE_CONSTANT_BUFFER,
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
    /// <c>SlangPushConstantRange</c> values, whose byte size comes from the
    /// declaring parameter rather than from the range.
    /// </summary>
    private static SlangPushConstantRange[] BuildPushConstantRanges(
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
                + "block across the composed program, or build the SlangPushConstantRange values by hand.");
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
        return [new SlangPushConstantRange { Stages = programStages, Offset = 0, Size = (uint)size }];
    }

    /// <summary>
    /// Step 5 — fills <c>SlangDescriptorBinding.Stages</c> on everything the walk
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
        out SlangDescriptorBinding[] bindings)
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
        bindings = new SlangDescriptorBinding[sorted.Length];

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

    private static SlangVertexAttributeDescription[] BuildVertexAttributes(SlangEntryPointLayout* entryPoint, ShaderStages stage)
    {
        // A fragment stage's struct input is VARYING_INPUT too; only a vertex
        // stage's inputs are VkVertexInputAttributeDescriptions.
        if (stage != ShaderStages.Vertex)
        {
            return [];
        }

        uint parameterCount = SlangApi.spReflectionEntryPoint_getParameterCount(entryPoint);
        List<SlangVertexAttributeDescription>? attributes = null;

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

(attributes ??= []).Add(BuildVertexAttribute(fieldTypeLayout, location, NameOf(field)));
                }
            }
            else
            {
(attributes ??= []).Add(BuildVertexAttribute(typeLayout, baseLocation, NameOf(parameter)));
            }
        }

        if (attributes is null)
        {
            return [];
        }

        SlangVertexAttributeDescription[] result = [.. attributes];

        Array.Sort(result, static (a, b) => a.Location.CompareTo(b.Location));

        return result;
    }

    
    private static SlangVertexAttributeDescription BuildVertexAttribute(SlangReflectionTypeLayout* typeLayout, uint location, string name)
    {
        SlangTypeKind kind = SlangApi.spReflectionTypeLayout_getKind(typeLayout);
        SlangReflectionType* type = SlangApi.spReflectionTypeLayout_GetType(typeLayout);
        
        uint components = 0;
        uint rows = 0;
        uint cols = 0;
        SlangScalarType scalar = SlangScalarType.SLANG_SCALAR_TYPE_NONE;

        if (kind == SlangTypeKind.SLANG_TYPE_KIND_VECTOR)
        {
            components = (uint)SlangApi.spReflectionType_GetElementCount(type);
            scalar = SlangApi.spReflectionType_GetScalarType(SlangApi.spReflectionType_GetElementType(type));
        }
        else if (kind == SlangTypeKind.SLANG_TYPE_KIND_SCALAR)
        {
            components = 1;
            scalar = SlangApi.spReflectionType_GetScalarType(type);
        }
        else if (kind == SlangTypeKind.SLANG_TYPE_KIND_MATRIX)
        {
            rows = SlangApi.spReflectionType_GetRowCount(type);
            cols = SlangApi.spReflectionType_GetColumnCount(type);
        }

        return new SlangVertexAttributeDescription
        {
            Location = location,
            Name = name,
            Kind = kind,
            ScalarType = scalar,
            ComponentCount = components,
            RowCount = rows,
            ColumnCount = cols,
            SizeInLocations = (long)SlangApi.spReflectionTypeLayout_GetSize(typeLayout, SlangParameterCategory.SLANG_PARAMETER_CATEGORY_VARYING_INPUT)
        };
    }

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
    private readonly record struct PendingBinding(uint Set, SlangDescriptorBinding Binding);
}
