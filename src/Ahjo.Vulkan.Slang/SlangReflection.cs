using System.Diagnostics.CodeAnalysis;

using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Slang.Internal;
using Ahjo.Vulkan.Slang.Native;

namespace Ahjo.Vulkan.Slang;

/// <summary>
/// The binding surface of a linked Slang program, expressed in the wrapper's
/// own description types — <c>SlangDescriptorBinding</c>, <c>SlangPushConstantRange</c>
/// and <c>SlangVertexAttributeDescription</c> — plus the contents of the
/// buffers those bindings point at (<see cref="TryGetBufferLayout"/>).
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
/// <para>Everything except <see cref="ToJson"/> is computed once, eagerly, in
/// the constructor; the spans this type hands out are views over those arrays
/// and stay valid for its lifetime. Reflection is setup-time — the wrapper's
/// zero-per-frame-allocation invariant does not apply here and no benchmark
/// covers it.</para>
/// </remarks>
public sealed unsafe class SlangReflection
{
    /// <summary>
    /// How deep <see cref="AppendMembers"/> will follow nested structs before
    /// refusing. A struct that contains itself is not expressible in Slang, but
    /// a pathological generated layout must not be able to overflow the stack
    /// inside a constructor.
    /// </summary>
    private const int MaxMemberDepth = 16;

    private readonly uint[] _setIndices;
    private readonly int[] _setStarts;
    private readonly SlangDescriptorBinding[] _bindings;
    private readonly SlangPushConstantRange[] _pushConstantRanges;
    private readonly SlangEntryPointInfo[] _entryPoints;
    private readonly SlangVertexAttributeDescription[][] _vertexAttributes;
    private readonly (uint Set, uint Slot)[] _bufferLayoutKeys;
    private readonly SlangBufferLayout[] _bufferLayouts;
    private readonly SlangBufferLayout? _pushConstantLayout;

    /// <summary>
    /// The program this reflection was read from, kept only so
    /// <see cref="ToJson"/> can re-obtain the layout pointer on demand.
    /// <b>Holding it changes no ownership or disposal semantics</b> — the
    /// reflection does not own the program and must never dispose it; a
    /// <see cref="ToJson"/> call after the program is gone throws
    /// <see cref="ObjectDisposedException"/> from the existing
    /// <c>LinkedComponent</c> guard, which is the intended behaviour.
    /// </summary>
    private readonly SlangProgram _program;

    private string? _json;

    internal SlangReflection(SlangProgram program, SlangStageAttribution attribution)
    {
        _program = program;

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
            SlangEntryPointInfo info = SlangEntryPoints.Read(entryPoint);

            _entryPoints[i] = info;
            _vertexAttributes[i] = BuildVertexAttributes(entryPoint, info.Stage);
            programStages |= info.Stage;
        }

        var state = new WalkState();

        // setOf(global scope) = 0. Not "the global scope owns set 0" — a
        // program whose global scope declares only ParameterBlocks has no
        // descriptors of its own, and its first block lands in set 0.
        Walk(
            SlangApi.spReflection_getGlobalParamsTypeLayout(layout),
            absoluteSet: 0,
            isParameterBlockElement: false,
            scopeName: string.Empty,
            scopeIsSpecializable: false,
            state);

        _pushConstantRanges = BuildPushConstantRanges(
            layout, state.PushConstantRangeCount, programStages, out _pushConstantLayout);

        AddBufferLayoutsFromBindingRangeFacts(state);

        ApplyStages(linked, state.Pending, _entryPoints, programStages, attribution);
        Group(state.Pending, state.Facts, out _setIndices, out _setStarts, out _bindings);

        _bufferLayoutKeys = new (uint, uint)[state.BufferLayouts.Count];
        _bufferLayouts = new SlangBufferLayout[state.BufferLayouts.Count];

        for (int i = 0; i < state.BufferLayouts.Count; i++)
        {
            (uint set, uint slot, SlangBufferLayout bufferLayout) = state.BufferLayouts[i];

            _bufferLayoutKeys[i] = (set, slot);
            _bufferLayouts[i] = bufferLayout;
        }

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
    /// The layout of the uniform or structured buffer bound at
    /// <paramref name="set"/> / <paramref name="slot"/>, or
    /// <see langword="false"/> when that binding is not a buffer with ordinary
    /// data in it.
    /// </summary>
    /// <remarks>
    /// <para>Every buffer-shaped binding the program declares has one: a
    /// standalone <c>ConstantBuffer&lt;T&gt;</c>, the implicit uniform buffer a
    /// <c>ParameterBlock</c> owns at binding 0 of its space, and the
    /// <em>element</em> of a structured buffer. A texture, a sampler and a
    /// buffer whose element carries no ordinary data report
    /// <see langword="false"/>.</para>
    /// <para>Push-constant blocks are not descriptor bindings and are reached
    /// through <see cref="TryGetPushConstantLayout"/> instead.</para>
    /// </remarks>
    public bool TryGetBufferLayout(uint set, uint slot, [NotNullWhen(true)] out SlangBufferLayout? layout)
    {
        // Linear, like TryGetSet: setup-time, and a program's buffer count is
        // single to low double digits.
        for (int i = 0; i < _bufferLayoutKeys.Length; i++)
        {
            if (_bufferLayoutKeys[i].Set == set && _bufferLayoutKeys[i].Slot == slot)
            {
                layout = _bufferLayouts[i];

                return true;
            }
        }

        layout = null;

        return false;
    }

    /// <summary>
    /// The layout of the program's <c>[[vk::push_constant]]</c> block, or
    /// <see langword="false"/> when it declares none.
    /// </summary>
    /// <remarks>
    /// <see cref="SlangBufferLayout.Size"/> is the same number
    /// <see cref="PushConstantRanges"/> reports as <c>Size</c>, and the members
    /// are the bytes a caller writes into <c>vkCmdPushConstants</c>.
    /// </remarks>
    public bool TryGetPushConstantLayout([NotNullWhen(true)] out SlangBufferLayout? layout)
    {
        layout = _pushConstantLayout;

        return layout is not null;
    }

    /// <summary>
    /// Slang's own JSON dump of this program's layout. <b>For diagnostics.</b>
    /// </summary>
    /// <remarks>
    /// <para>The schema is Slang's, is not stable across versions, and is not
    /// something this package parses or promises. Use the typed surface for
    /// anything a program depends on; this is the escape hatch for what the
    /// typed surface does not cover yet — for example the members of an
    /// array-of-struct element.</para>
    /// <para>Computed on first call and cached, so a program nobody asks about
    /// never pays for serializing its whole layout into a string.</para>
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The program has been disposed.</exception>
    /// <exception cref="SlangCompilationException">Slang refused to produce the dump.</exception>
    public string ToJson()
    {
        if (_json is not null)
        {
            return _json;
        }

        SlangProgramLayout* layout = GetLayout(_program.LinkedComponent);
        ISlangBlob* blob = null;
        int rc = SlangApi.spReflection_ToJson(layout, null, &blob);

        if (rc < 0 || blob == null)
        {
            throw new SlangCompilationException($"spReflection_ToJson (0x{rc:X8})", string.Empty);
        }

        try
        {
            _json = SlangUtf8.ReadBlob(blob);
        }
        finally
        {
            blob->release();
        }

        return _json;
    }

    /// <summary>
    /// The <paramref name="index"/>-th entry point's name, stage and thread
    /// group size. Same index as <c>SlangProgram.Spirv</c> and
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
    /// <para>This reports the input's <c>Location</c>, its declared Slang
    /// type and its HLSL semantic. Use
    /// <c>SlangVulkanMapping.MapVertexAttribute</c> to resolve that into a
    /// <c>VertexAttributeDescription</c> with a <c>VkFormat</c>; that is
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
    /// Walks one scope — the global scope, or one <c>ParameterBlock</c>'s
    /// element — collecting its descriptor bindings, the facts the binding-range
    /// pass supplies for them, its buffer layouts and its push-constant range
    /// count, and recursing into the parameter blocks it contains.
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
    /// <param name="scopeName">
    /// The declaring <c>ParameterBlock</c> parameter's name, or empty for the
    /// global scope. The implicit uniform buffer step 2 synthesizes has no name
    /// of its own and takes this one, for both its
    /// <c>SlangDescriptorBinding.Name</c> and its <c>SlangBufferLayout.Name</c>.
    /// </param>
    /// <param name="scopeIsSpecializable">
    /// Whether the declaring block's binding range reported
    /// <c>isBindingRangeSpecializable</c>. Threaded the same way and for the
    /// same reason as <paramref name="scopeName"/>: the flag is Slang's answer
    /// about the block, and the block's only binding is the one step 2 makes.
    /// </param>
    /// <param name="state">Everything the walk accumulates.</param>
    private static void Walk(
        SlangReflectionTypeLayout* structTypeLayout,
        uint absoluteSet,
        bool isParameterBlockElement,
        string scopeName,
        bool scopeIsSpecializable,
        WalkState state)
    {
        // ---- Step 0: the additive binding-range pass. ----
        CollectBindingRangeFacts(structTypeLayout, absoluteSet, state.Facts);

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
                    state.PushConstantRangeCount++;

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

                state.Pending.Add(new PendingBinding(
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
            // Name and IsSpecializable come from the declaring block, not from
            // the binding-range pass: there is no binding range for a buffer
            // Slang does not report. Group's stamping step leaves these alone
            // for exactly that reason — the facts dictionary has no entry here.
            state.Pending.Add(new PendingBinding(
                absoluteSet,
                new SlangDescriptorBinding
                {
                    Slot = 0,
                    Name = scopeName,
                    Count = SlangDescriptorCount.Fixed(1),
                    IsSpecializable = scopeIsSpecializable,

                    // By construction, not through MapBindingType — there is no
                    // Slang binding type for a range Slang does not report.
                    Type = SlangBindingType.SLANG_BINDING_TYPE_CONSTANT_BUFFER,
                }));

            state.BufferLayouts.Add((absoluteSet, 0, BuildBufferLayout(structTypeLayout, scopeName)));
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
                NameOfBindingRange(structTypeLayout, bindingRange),
                SlangApi.spReflectionTypeLayout_isBindingRangeSpecializable(structTypeLayout, bindingRange) != 0,
                state);
        }
    }

    /// <summary>
    /// Step 0 — the binding-range pass: everything Slang knows about a binding
    /// that its descriptor-range list does not carry.
    /// </summary>
    /// <remarks>
    /// <para><b>Route 1 of spec E8's ladder, measured before it was written.</b>
    /// The concern was that <c>getBindingRangeDescriptorSetIndex</c> /
    /// <c>getBindingRangeFirstDescriptorRangeIndex</c> would turn out to be the
    /// same kind of call as <c>getSubObjectRangeSpaceOffset</c>, which looks
    /// right in the header and returns <c>0</c> for everything. They are not.
    /// Measured on <c>v2026.14.1</c> / win-x64, the keys this pass computes
    /// match the SPIR-V-verified walk exactly:</para>
    /// <list type="bullet">
    /// <item><description><c>ReflectionGlobals</c> — br 0..3 join to
    /// <c>(0,0) gXform</c>, <c>(0,1) gAlbedo</c>, <c>(0,2) gSampler</c>,
    /// <c>(0,3) gOut</c>, which are the four <c>OpName</c>s the emitted module
    /// decorates at those very <c>(set, binding)</c> pairs.</description></item>
    /// <item><description><c>ReflectionSparseSets</c> — br 1 reports
    /// <c>s = 1</c> and a space offset of <b>2</b>, joining to
    /// <c>(2,7) gSamp</c> rather than to the loop index's <c>(1,7)</c>. So this
    /// pass reads the space offset the same way step 1 does, and a sparse
    /// program does not silently key one set low.</description></item>
    /// <item><description><c>ReflectionBlockOrdinaryData</c> — inside
    /// <c>gWith</c>'s scope, br 0/1 join to <c>(0,1)</c> and <c>(0,2)</c>: the
    /// shift the implicit uniform buffer causes is already in the numbers.</description></item>
    /// </list>
    /// <para><b>All three skips are load-bearing, not tidiness.</b> A
    /// <c>PARAMETER_BLOCK</c> range reports <c>s = -1</c> and step 3 recurses
    /// into it. A <c>PUSH_CONSTANT</c> range reports a real
    /// <c>(s, r)</c> that resolves to <b>slot 0 of the enclosing set</b> —
    /// measured: <c>gPush</c> in <c>ReflectionGlobals</c> joins to
    /// <c>(0, 0)</c>, which is <c>gXform</c>'s key. Keeping it would rename a
    /// caller's constant buffer after the push-constant block. An
    /// <c>EXISTENTIAL_VALUE</c> range is what an interface-typed block's
    /// element scope reports for the value buffer step 2 already synthesizes —
    /// it joins to that same <c>(set, 0)</c>, its leaf variable is
    /// <see langword="null"/>, and stamping it would replace the block's name
    /// with an empty one.</para>
    /// <para>This pass is deliberately <b>additive</b>: it never modifies the
    /// descriptor walk and a key it fails to produce costs a name, not a
    /// binding.</para>
    /// </remarks>
    private static void CollectBindingRangeFacts(
        SlangReflectionTypeLayout* structTypeLayout,
        uint absoluteSet,
        Dictionary<(uint Set, uint Slot), BindingFacts> into)
    {
        long bindingRangeCount = SlangApi.spReflectionTypeLayout_getBindingRangeCount(structTypeLayout);

        for (long br = 0; br < bindingRangeCount; br++)
        {
            SlangBindingType type = SlangApi.spReflectionTypeLayout_getBindingRangeType(structTypeLayout, br);

            if (type is SlangBindingType.SLANG_BINDING_TYPE_PARAMETER_BLOCK
                     or SlangBindingType.SLANG_BINDING_TYPE_PUSH_CONSTANT
                     or SlangBindingType.SLANG_BINDING_TYPE_EXISTENTIAL_VALUE)
            {
                continue;
            }

            long s = SlangApi.spReflectionTypeLayout_getBindingRangeDescriptorSetIndex(structTypeLayout, br);
            long r = SlangApi.spReflectionTypeLayout_getBindingRangeFirstDescriptorRangeIndex(structTypeLayout, br);

            if (s < 0 || r < 0)
            {
                continue;
            }

            long spaceOffset = SlangApi.spReflectionTypeLayout_getDescriptorSetSpaceOffset(structTypeLayout, s);
            long slot = SlangApi.spReflectionTypeLayout_getDescriptorSetDescriptorRangeIndexOffset(structTypeLayout, s, r);

            // Step 1 is what refuses a sentinel here, with a message naming the
            // range. This pass only declines to key one.
            if (spaceOffset < 0 || slot < 0 || slot > uint.MaxValue)
            {
                continue;
            }

            // getBindingRangeImageFormat is NOT total — see ImageFormatOf.
            into[(absoluteSet + (uint)spaceOffset, (uint)slot)] = new BindingFacts(
                NameOfBindingRange(structTypeLayout, br),
                ImageFormatOf(structTypeLayout, br, type),
                SlangApi.spReflectionTypeLayout_isBindingRangeSpecializable(structTypeLayout, br) != 0,
                (nint)SlangApi.spReflectionTypeLayout_getBindingRangeLeafTypeLayout(structTypeLayout, br));
        }
    }

    /// <summary>
    /// The storage-image format of a binding range, asked only of the binding
    /// types that can have one.
    /// </summary>
    /// <remarks>
    /// <b><c>spReflectionTypeLayout_getBindingRangeImageFormat</c> is not a
    /// total function.</b> Measured on <c>v2026.14.1</c> / win-x64: calling it
    /// on the single <c>SLANG_BINDING_TYPE_EXISTENTIAL_VALUE</c> range that a
    /// <c>ParameterBlock&lt;ISurface&gt;</c>'s element scope reports kills the
    /// process with <c>0xC0000005</c> — not a bad value, an access violation,
    /// with no result code to check. Every other call on that range
    /// (<c>getBindingRangeType</c>, the set/range indices,
    /// <c>getBindingRangeLeafVariable</c>, <c>getBindingRangeLeafTypeLayout</c>,
    /// <c>isBindingRangeSpecializable</c>) returns normally, so the crash is
    /// specific to this one entry point. Asking only textures and typed buffers
    /// — the only declarations <c>[[vk::image_format]]</c> applies to — loses
    /// nothing: every other binding type reported
    /// <c>SLANG_IMAGE_FORMAT_unknown</c>, which is what this returns for them.
    /// </remarks>
    private static SlangImageFormat ImageFormatOf(
        SlangReflectionTypeLayout* structTypeLayout,
        long bindingRange,
        SlangBindingType type)
        => (type & SlangBindingType.SLANG_BINDING_TYPE_BASE_MASK) is
            SlangBindingType.SLANG_BINDING_TYPE_TEXTURE or SlangBindingType.SLANG_BINDING_TYPE_TYPED_BUFFER
            ? SlangApi.spReflectionTypeLayout_getBindingRangeImageFormat(structTypeLayout, bindingRange)
            : SlangImageFormat.SLANG_IMAGE_FORMAT_unknown;

    /// <summary>
    /// Spec D4(c) — a buffer layout for every binding whose leaf type has an
    /// element with ordinary data: a standalone <c>ConstantBuffer&lt;T&gt;</c>,
    /// the global scope's implicit constant buffer, a structured buffer's
    /// element.
    /// </summary>
    /// <remarks>
    /// A key a <c>ParameterBlock</c> already claimed in step 2 wins: that one is
    /// built from the block's own element type layout, which is the same struct,
    /// and re-adding it would leave two entries the linear lookup could return
    /// either of.
    /// </remarks>
    private static void AddBufferLayoutsFromBindingRangeFacts(WalkState state)
    {
        foreach (((uint set, uint slot), BindingFacts facts) in state.Facts)
        {
            var leaf = (SlangReflectionTypeLayout*)facts.LeafTypeLayout;

            if (leaf == null)
            {
                continue;
            }

            SlangReflectionTypeLayout* element = SlangApi.spReflectionTypeLayout_GetElementTypeLayout(leaf);

            if (element == null
                || SlangApi.spReflectionTypeLayout_GetSize(
                    element, SlangParameterCategory.SLANG_PARAMETER_CATEGORY_UNIFORM) == 0)
            {
                continue;
            }

            bool alreadyKeyed = false;

            for (int i = 0; i < state.BufferLayouts.Count; i++)
            {
                alreadyKeyed |= state.BufferLayouts[i].Set == set && state.BufferLayouts[i].Slot == slot;
            }

            if (!alreadyKeyed)
            {
                state.BufferLayouts.Add((set, slot, BuildBufferLayout(element, facts.Name)));
            }
        }
    }

    /// <summary>
    /// Step 4 — turns the push-constant ranges step 1 counted into
    /// <c>SlangPushConstantRange</c> values, whose byte size comes from the
    /// declaring parameter rather than from the range, and builds the block's
    /// member layout from the same type layout.
    /// </summary>
    private static SlangPushConstantRange[] BuildPushConstantRanges(
        SlangProgramLayout* layout,
        int pushConstantRangeCount,
        ShaderStages programStages,
        out SlangBufferLayout? blockLayout)
    {
        blockLayout = null;

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

        SlangReflectionTypeLayout* element = SlangApi.spReflectionTypeLayout_GetElementTypeLayout(found);
        nuint size = SlangApi.spReflectionTypeLayout_GetSize(
            element, SlangParameterCategory.SLANG_PARAMETER_CATEGORY_UNIFORM);

        if (size == 0 || size > uint.MaxValue)
        {
            throw new NotSupportedException(
                $"Push-constant block '{firstName}' reports a uniform size of {size}, which is not a byte count "
                + "VkPushConstantRange.Size can carry. Reflect a fully specialized program.");
        }

        blockLayout = BuildBufferLayout(element, firstName);

        // Offset 0: with exactly one block there is nothing to offset past, and
        // Stages is the program union in both attribution modes — see the
        // PushConstantRanges remarks.
        return [new SlangPushConstantRange
        {
            Name = firstName,
            Stages = programStages,
            Offset = 0,
            Size = (uint)size,
        }];
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
    /// Step 6 — stamps each binding with the binding-range pass's facts, then
    /// sorts by set then slot and slices the flat binding array per populated
    /// set.
    /// </summary>
    private static void Group(
        List<PendingBinding> pending,
        Dictionary<(uint Set, uint Slot), BindingFacts> facts,
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

            SlangDescriptorBinding binding = sorted[i].Binding;

            // A binding with no fact is the ParameterBlock's synthesized
            // uniform buffer, which carries the block's own name and
            // specializability already.
            bindings[i] = facts.TryGetValue((sorted[i].Set, binding.Slot), out BindingFacts fact)
                ? binding with
                {
                    Name = fact.Name,
                    ImageFormat = fact.ImageFormat,
                    IsSpecializable = fact.IsSpecializable,
                }
                : binding;
        }

        setStarts[distinctSets] = sorted.Length;
    }

    /// <summary>
    /// Walks a buffer's element type into a flat, pre-order member list.
    /// </summary>
    /// <remarks>
    /// A buffer element that is not a struct — the existential value buffer
    /// behind a <c>ParameterBlock&lt;ISurface&gt;</c> (kind
    /// <c>SLANG_TYPE_KIND_INTERFACE</c>), a
    /// <c>StructuredBuffer&lt;float4&gt;</c>'s vector element — has a size and
    /// no describable members, and reports exactly that. The kind is checked
    /// rather than relying on <c>GetFieldCount</c> returning <c>0</c>: this
    /// family of calls is not uniformly total, and
    /// <see cref="ImageFormatOf"/> documents the one in it that takes the
    /// process down.
    /// </remarks>
    private static SlangBufferLayout BuildBufferLayout(SlangReflectionTypeLayout* elementTypeLayout, string name)
    {
        nuint rawSize = SlangApi.spReflectionTypeLayout_GetSize(
            elementTypeLayout, SlangParameterCategory.SLANG_PARAMETER_CATEGORY_UNIFORM);

        if (!TryClassifySize(rawSize, name, out uint size))
        {
            // A buffer ending in a runtime-sized array has no total size. Its
            // members are still exact, and the trailing one carries IsUnsized.
            size = 0;
        }

        if (SlangApi.spReflectionTypeLayout_getKind(elementTypeLayout) != SlangTypeKind.SLANG_TYPE_KIND_STRUCT)
        {
            return new SlangBufferLayout(name, size, []);
        }

        var members = new List<SlangBufferMember>();

        AppendMembers(elementTypeLayout, string.Empty, parentIndex: -1, baseOffset: 0, depth: 0, members);

        return new SlangBufferLayout(name, size, [.. members]);
    }

    /// <summary>
    /// Appends one struct's fields, then recurses into the struct-typed ones.
    /// </summary>
    /// <remarks>
    /// Offsets accumulate exactly as vertex-input locations do: a field's
    /// <c>UNIFORM</c> offset is relative to its enclosing struct, so the
    /// enclosing struct's own offset is the base. Every byte quantity here is
    /// read under <c>SLANG_PARAMETER_CATEGORY_UNIFORM</c> — a different
    /// category produces offsets that look plausible and are silently wrong,
    /// which is the failure issue #175 exists to prevent, and
    /// <c>BufferLayout_MaterialBlock_OffsetsMatchTheEmittedSpirv</c> is what
    /// pins it.
    /// </remarks>
    private static void AppendMembers(
        SlangReflectionTypeLayout* structTypeLayout,
        string pathPrefix,
        int parentIndex,
        uint baseOffset,
        int depth,
        List<SlangBufferMember> into)
    {
        if (depth > MaxMemberDepth)
        {
            throw new NotSupportedException(
                $"Buffer member '{pathPrefix}' nests more than {MaxMemberDepth} structs deep. Reflecting it would "
                + "risk overflowing the stack inside SlangReflection's constructor, so it is refused instead.");
        }

        uint fieldCount = SlangApi.spReflectionTypeLayout_GetFieldCount(structTypeLayout);

        for (uint f = 0; f < fieldCount; f++)
        {
            SlangReflectionVariableLayout* field = SlangApi.spReflectionTypeLayout_GetFieldByIndex(structTypeLayout, f);
            SlangReflectionTypeLayout* fieldTypeLayout = SlangApi.spReflectionVariableLayout_GetTypeLayout(field);
            string fieldName = NameOf(field);
            string path = pathPrefix.Length == 0 ? fieldName : pathPrefix + "." + fieldName;

            nuint rawSize = SlangApi.spReflectionTypeLayout_GetSize(
                fieldTypeLayout, SlangParameterCategory.SLANG_PARAMETER_CATEGORY_UNIFORM);
            bool sized = TryClassifySize(rawSize, path, out uint size);

            // A Texture2D, a SamplerState or a struct of those occupies no
            // bytes in the buffer. Listing it at offset 0 with size 0 would
            // invite a caller to write there.
            if (sized && size == 0)
            {
                continue;
            }

            // Guarded like every sibling quantity in this loop, and like the
            // same call in step 3. An unguarded cast would turn a sentinel into
            // offset 0xFFFFFFFF — and this is the one number a caller writes
            // bytes against, so a plausible-looking lie here is the whole of
            // issue #175.
            nuint rawOffset = SlangApi.spReflectionVariableLayout_GetOffset(
                field, SlangParameterCategory.SLANG_PARAMETER_CATEGORY_UNIFORM);

            if (!TryClassifySize(rawOffset, path, out uint fieldOffset))
            {
                throw new NotSupportedException(
                    $"Buffer member '{path}' reports a uniform offset that is one of Slang's sentinels "
                    + "(SLANG_UNBOUNDED_SIZE, SLANG_UNKNOWN_SIZE) rather than a byte offset. There is no address to "
                    + "report for it, and guessing one would have a caller write to the wrong bytes. Reflect a fully "
                    + "specialized program.");
            }

            uint offset = baseOffset + fieldOffset;

            SlangTypeKind kind = SlangApi.spReflectionTypeLayout_getKind(fieldTypeLayout);
            SlangReflectionType* type = SlangApi.spReflectionTypeLayout_GetType(fieldTypeLayout);

            DescribeType(
                fieldTypeLayout,
                out SlangScalarType scalar,
                out uint components,
                out uint rows,
                out uint columns);

            uint matrixStride = 0;
            SlangMatrixLayoutMode matrixLayout = default;

            if (kind == SlangTypeKind.SLANG_TYPE_KIND_MATRIX)
            {
                matrixLayout = SlangApi.spReflectionTypeLayout_GetMatrixLayoutMode(fieldTypeLayout);

                // Measured on v2026.14.1 / win-x64: GetElementTypeLayout on a
                // float4x4's type layout yields the row vector's layout, whose
                // UNIFORM stride is 16. GetElementStride on the matrix itself
                // returns 0, so this is the derivation that works.
                SlangReflectionTypeLayout* rowLayout =
                    SlangApi.spReflectionTypeLayout_GetElementTypeLayout(fieldTypeLayout);

                if (rowLayout != null)
                {
                    nuint stride = SlangApi.spReflectionTypeLayout_GetStride(
                        rowLayout, SlangParameterCategory.SLANG_PARAMETER_CATEGORY_UNIFORM);

                    matrixStride = stride <= uint.MaxValue ? (uint)stride : 0;
                }
            }

            uint elementCount = 0;
            uint elementStride = 0;
            bool unsizedArray = false;

            if (kind == SlangTypeKind.SLANG_TYPE_KIND_ARRAY)
            {
                nuint rawCount = SlangApi.spReflectionType_GetElementCount(type);

                unsizedArray = !TryClassifySize(rawCount, path, out elementCount);

                nuint stride = SlangApi.spReflectionTypeLayout_GetElementStride(
                    fieldTypeLayout, SlangParameterCategory.SLANG_PARAMETER_CATEGORY_UNIFORM);

                elementStride = stride <= uint.MaxValue ? (uint)stride : 0;
            }

            nuint rawStride = SlangApi.spReflectionTypeLayout_GetStride(
                fieldTypeLayout, SlangParameterCategory.SLANG_PARAMETER_CATEGORY_UNIFORM);
            int alignment = SlangApi.spReflectionTypeLayout_getAlignment(
                fieldTypeLayout, SlangParameterCategory.SLANG_PARAMETER_CATEGORY_UNIFORM);

            int index = into.Count;

            into.Add(new SlangBufferMember
            {
                Name = path,
                ParentIndex = parentIndex,
                Offset = offset,
                Size = sized ? size : 0,
                Stride = rawStride <= uint.MaxValue ? (uint)rawStride : 0,
                Alignment = alignment > 0 ? (uint)alignment : 0,
                IsUnsized = !sized || unsizedArray,
                TypeName = SlangUtf8.ToString(SlangApi.spReflectionType_GetName(type)) ?? string.Empty,
                Kind = kind,
                ScalarType = scalar,
                ComponentCount = components,
                RowCount = rows,
                ColumnCount = columns,
                MatrixLayout = matrixLayout,
                MatrixStride = matrixStride,
                ElementCount = elementCount,
                ElementStride = elementStride,
            });

            // Arrays are leaves on purpose: recursing into an array's element
            // struct would introduce members whose Offset is element-relative
            // while every other member's is buffer-relative.
            if (kind == SlangTypeKind.SLANG_TYPE_KIND_STRUCT)
            {
                AppendMembers(fieldTypeLayout, path, index, offset, depth + 1, into);
            }
        }
    }

    /// <summary>
    /// Classifies a <c>nuint</c> Slang returned for a size, a stride or an
    /// element count.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when the value is one of Slang's documented
    /// sentinels (<c>SLANG_UNBOUNDED_SIZE</c>, <c>SLANG_UNKNOWN_SIZE</c> —
    /// <c>slang.h:2361-2362</c>), in which case
    /// <paramref name="value"/> is <c>0</c>.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// The value is neither a sentinel nor representable as a
    /// <see cref="uint"/> byte count.
    /// </exception>
    private static bool TryClassifySize(nuint raw, string what, out uint value)
    {
        if (raw == nuint.MaxValue || raw == nuint.MaxValue - 1)
        {
            value = 0;

            return false;
        }

        if (raw > uint.MaxValue)
        {
            throw new NotSupportedException(
                $"Slang reports {raw} for '{what}', which is neither a byte count nor one of its documented "
                + "sentinels (SLANG_UNBOUNDED_SIZE, SLANG_UNKNOWN_SIZE). Reflect a fully specialized program.");
        }

        value = (uint)raw;

        return true;
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

                    (attributes ??= []).Add(BuildVertexAttribute(field, fieldTypeLayout, location));
                }
            }
            else
            {
                (attributes ??= []).Add(BuildVertexAttribute(parameter, typeLayout, baseLocation));
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

    /// <summary>
    /// Builds one vertex attribute. Takes the <em>variable</em> layout as well
    /// as the type layout because the HLSL semantic lives on the variable — the
    /// type layout knows nothing about <c>POSITION</c>.
    /// </summary>
    private static SlangVertexAttributeDescription BuildVertexAttribute(
        SlangReflectionVariableLayout* variableLayout,
        SlangReflectionTypeLayout* typeLayout,
        uint location)
    {
        DescribeType(typeLayout, out SlangScalarType scalar, out uint components, out uint rows, out uint columns);

        nuint semanticIndex = SlangApi.spReflectionVariableLayout_GetSemanticIndex(variableLayout);

        return new SlangVertexAttributeDescription
        {
            Location = location,
            Name = NameOf(variableLayout),
            SemanticName = SlangUtf8.ToString(SlangApi.spReflectionVariableLayout_GetSemanticName(variableLayout))
                ?? string.Empty,
            SemanticIndex = semanticIndex <= uint.MaxValue ? (uint)semanticIndex : 0,
            Kind = SlangApi.spReflectionTypeLayout_getKind(typeLayout),
            ScalarType = scalar,
            ComponentCount = components,
            RowCount = rows,
            ColumnCount = columns,
            SizeInLocations = (long)SlangApi.spReflectionTypeLayout_GetSize(
                typeLayout, SlangParameterCategory.SLANG_PARAMETER_CATEGORY_VARYING_INPUT),
        };
    }

    /// <summary>
    /// The one derivation of scalar type, component count and matrix extents
    /// from a type layout — shared by vertex attributes and buffer members so
    /// the package has one way of describing a type, not two.
    /// </summary>
    private static void DescribeType(
        SlangReflectionTypeLayout* typeLayout,
        out SlangScalarType scalar,
        out uint components,
        out uint rows,
        out uint columns)
    {
        SlangTypeKind kind = SlangApi.spReflectionTypeLayout_getKind(typeLayout);
        SlangReflectionType* type = SlangApi.spReflectionTypeLayout_GetType(typeLayout);

        scalar = SlangScalarType.SLANG_SCALAR_TYPE_NONE;
        components = 0;
        rows = 0;
        columns = 0;

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
            columns = SlangApi.spReflectionType_GetColumnCount(type);
            scalar = SlangApi.spReflectionType_GetScalarType(SlangApi.spReflectionType_GetElementType(type));
        }
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

    private static string NameOfBindingRange(SlangReflectionTypeLayout* structTypeLayout, long bindingRange)
    {
        SlangReflectionVariable* variable =
            SlangApi.spReflectionTypeLayout_getBindingRangeLeafVariable(structTypeLayout, bindingRange);

        return variable == null
            ? string.Empty
            : SlangUtf8.ToString(SlangApi.spReflectionVariable_GetName(variable)) ?? string.Empty;
    }

    /// <summary>
    /// A binding plus the Vulkan set it landed in, before
    /// <c>Stages</c> is resolved and before grouping.
    /// </summary>
    private readonly record struct PendingBinding(uint Set, SlangDescriptorBinding Binding);

    /// <summary>
    /// What the binding-range pass knows about a <c>(set, slot)</c> that the
    /// descriptor-range list does not carry.
    /// </summary>
    private readonly record struct BindingFacts(
        string Name,
        SlangImageFormat ImageFormat,
        bool IsSpecializable,
        nint LeafTypeLayout);

    /// <summary>
    /// Everything <see cref="Walk"/> accumulates across its recursion. One
    /// object rather than five by-ref parameters — the walk threads a scope
    /// name and a specializability flag down as well now, and nine parameters
    /// is not a signature anyone reads.
    /// </summary>
    private sealed class WalkState
    {
        public List<PendingBinding> Pending { get; } = [];

        public Dictionary<(uint Set, uint Slot), BindingFacts> Facts { get; } = [];

        public List<(uint Set, uint Slot, SlangBufferLayout Layout)> BufferLayouts { get; } = [];

        public int PushConstantRangeCount { get; set; }
    }
}
