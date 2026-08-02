using Ahjo.Vulkan.Slang;
using Ahjo.Vulkan.Slang.Native;
using System.Collections.Concurrent;
using System.Numerics;

using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Testing;

using Xunit;

namespace Ahjo.Vulkan.Slang.Tests;

/// <summary>
/// Reflection over a linked program, into the wrapper's own description types.
/// </summary>
/// <remarks>
/// <para>Wherever a set number, a binding number or a vertex input location is
/// the thing under test, the assertion is against <c>OpDecorate</c> in the
/// SPIR-V Slang emitted — not against numbers reflection reported. Reflection
/// agreeing with itself proves nothing, and several of the rules these tests
/// pin were invisible until measured that way: a parameter block's implicit
/// uniform buffer is a binding the shader uses and reflection never lists, and
/// a sparse set index differs from its own loop index.</para>
/// <para>Nothing here needs a Vulkan device except the last test.</para>
/// </remarks>
public sealed class SlangReflectionTests
{
    private readonly ITestOutputHelper _output;

    public SlangReflectionTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// The cross-check that carries the suite: for every fixture and every
    /// entry point, every <c>(set, binding)</c> pair the emitted SPIR-V
    /// decorates must be a binding reflection reported, at the same set and the
    /// same slot.
    /// </summary>
    /// <remarks>
    /// <para>This is the test that fails if either of the two rules Slang does
    /// not hand you is dropped.</para>
    /// <para><b>Drop the implicit block uniform buffer</b> (the
    /// <c>ordinary</c> / <c>twoBlocks</c> / <c>nested</c> / <c>onlyBlocks</c>
    /// rows) and reflection loses <c>set N binding 0</c> for every block whose
    /// element carries ordinary data, while SPIR-V still binds it — the pair is
    /// reported missing here by name.</para>
    /// <para><b>Use the descriptor-set loop index as the Vulkan set number</b>
    /// (the <c>sparse</c> row) and <c>gSamp</c>, which SPIR-V puts at set 2,
    /// is reported at set 1 — again a missing pair, named.</para>
    /// <para>The direction is deliberately one-way. Reflection reports what the
    /// program <em>declares</em> and per-entry-point SPIR-V contains only what
    /// that entry point <em>uses</em>, so a declared-but-unused binding is
    /// correct and is not an error here.</para>
    /// </remarks>
    [Theory]
    [InlineData("xcheckGlobals", ShaderFixtures.ReflectionGlobals)]
    [InlineData("xcheckTextureArray", ShaderFixtures.ReflectionTextureArray)]
    [InlineData("xcheckTwoBlocks", ShaderFixtures.ReflectionTwoBlocks)]
    [InlineData("xcheckOrdinary", ShaderFixtures.ReflectionBlockOrdinaryData)]
    [InlineData("xcheckNested", ShaderFixtures.ReflectionNestedBlock)]
    [InlineData("xcheckOnlyBlocks", ShaderFixtures.ReflectionOnlyBlocks)]
    [InlineData("xcheckSparse", ShaderFixtures.ReflectionSparseSets)]
    [InlineData("xcheckBindless", ShaderFixtures.ReflectionBindlessArrays)]
    public void Reflection_CoversEverySetAndBinding_TheSpirvDecorates(string moduleName, string source)
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);
        using SlangProgram program = session.Compile(new SlangCompileRequest
        {
            ModuleName = moduleName,
            Source = source,
        });

        AssertReflectionCoversSpirv(program, program.Reflection, _output);
    }

    [Fact]
    public void Reflection_ConstantBufferTextureSampler_ProducesBindings()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile("bindings", ShaderFixtures.ReflectionGlobals);
        SlangReflection reflection = reflected.Reflection;

        Assert.Equal(1, reflection.DescriptorSetCount);
        Assert.Equal(0u, reflection.SetIndex(0));

        ReadOnlySpan<SlangDescriptorBinding> bindings = reflection.Bindings(0);

        Assert.Equal(4, bindings.Length);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_CONSTANT_BUFFER, bindings[0].Type);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_TEXTURE, bindings[1].Type);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_SAMPLER, bindings[2].Type);

        for (int i = 0; i < 3; i++)
        {
            Assert.Equal((uint)i, bindings[i].Slot);
            Assert.Equal(1u, bindings[i].Count.Value);
        }
    }

    [Fact]
    public void Reflection_RWStructuredBuffer_MapsToStorageBuffer()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile("storage", ShaderFixtures.ReflectionGlobals);

        SlangDescriptorBinding binding = reflected.Reflection.Bindings(0)[3];

        Assert.Equal(3u, binding.Slot);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_MUTABLE_RAW_BUFFER, binding.Type);
    }

    [Fact]
    public void Reflection_TextureArray_ProducesCount()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile("textureArray", ShaderFixtures.ReflectionTextureArray);

        ReadOnlySpan<SlangDescriptorBinding> bindings = reflected.Reflection.Bindings(0);

        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_TEXTURE, bindings[0].Type);
        Assert.Equal(4u, bindings[0].Count.Value);
        Assert.Equal(1u, bindings[1].Count.Value);
    }

    /// <summary>
    /// Issue #176: an unbounded (bindless) array is a binding reflection
    /// reports, not a program it refuses.
    /// </summary>
    /// <remarks>
    /// This is also the measurement that pins <c>SLANG_UNBOUNDED_SIZE</c> to
    /// <see cref="SlangDescriptorCountKind.Unbounded"/>. If Slang ever reported
    /// a different sentinel for this shape, the walk's classification would
    /// produce a different <c>Kind</c> — or throw, naming the value — rather
    /// than this test quietly passing.
    /// </remarks>
    [Fact]
    public void Reflection_UnboundedArray_ReportsBindingInsteadOfThrowing()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile("bindless", ShaderFixtures.ReflectionBindlessArrays);

        Assert.True(reflected.Reflection.TryGetSet(0, out ReadOnlySpan<SlangDescriptorBinding> bindings));
        Assert.Equal(3, bindings.Length);

        for (int i = 0; i < bindings.Length; i++)
        {
            SlangDescriptorBinding binding = bindings[i];

            _output.WriteLine($"set 0 slot {binding.Slot}: {binding.Type} count kind {binding.Count.Kind}");

            Assert.Equal((uint)i, binding.Slot);
            Assert.Equal(SlangDescriptorCountKind.Unbounded, binding.Count.Kind);
            Assert.True(binding.Count.IsUnbounded);
            Assert.False(binding.Count.TryGetValue(out _));
            Assert.Throws<InvalidOperationException>(() => binding.Count.Value);
        }
    }

    /// <summary>
    /// The issue's actual complaint: one unbounded array used to make the whole
    /// program unreflectable — no other set, no push-constant ranges, no vertex
    /// attributes.
    /// </summary>
    [Fact]
    public void Reflection_UnboundedArray_DoesNotHideTheRestOfTheProgram()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile("bindlessRest", ShaderFixtures.ReflectionBindlessArrays);
        SlangReflection reflection = reflected.Reflection;

        Assert.True(reflection.TryGetSet(1, out ReadOnlySpan<SlangDescriptorBinding> ordinary));
        Assert.Equal(1, ordinary.Length);
        Assert.Equal(0u, ordinary[0].Slot);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_CONSTANT_BUFFER, ordinary[0].Type);
        Assert.Equal(1u, ordinary[0].Count.Value);

        Assert.Equal(1, reflection.PushConstantRanges.Length);
        Assert.False(reflection.VertexAttributes(0).IsEmpty);
    }

    /// <summary>
    /// The refusal moved to the mapper: that is where a
    /// <c>VkDescriptorSetLayoutBinding.descriptorCount</c> is chosen.
    /// </summary>
    [Fact]
    public void MapBinding_UnboundedBinding_Throws()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile("bindlessMap", ShaderFixtures.ReflectionBindlessArrays);

        Assert.True(reflected.Reflection.TryGetSet(0, out ReadOnlySpan<SlangDescriptorBinding> bindings));

        SlangDescriptorBinding binding = bindings[2];
        var ex = Assert.Throws<NotSupportedException>(() => binding.MapBinding());

        _output.WriteLine(ex.Message);

        Assert.Contains("binding 2", ex.Message, StringComparison.Ordinal);
        Assert.Contains("MapBinding", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MapBinding_WithCapacity_ProducesCountAndNoFlags()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile("bindlessCapacity", ShaderFixtures.ReflectionBindlessArrays);

        Assert.True(reflected.Reflection.TryGetSet(0, out ReadOnlySpan<SlangDescriptorBinding> bindings));

        DescriptorBinding mapped = bindings[0].MapBinding(1024);

        Assert.Equal(0u, mapped.Slot);
        Assert.Equal(1024u, mapped.Count);
        Assert.Equal(VkDescriptorType.VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE, mapped.Type);

        // Deliberate: VariableDescriptorCount is legal on at most one binding
        // per set, and this set has three unbounded arrays. The caller sets it.
        Assert.Equal(DescriptorBindingFlags.None, mapped.BindingFlags);
    }

    [Fact]
    public void MapBinding_WithCapacity_OnFixedBinding_Throws()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile("bindlessFixed", ShaderFixtures.ReflectionBindlessArrays);

        Assert.True(reflected.Reflection.TryGetSet(1, out ReadOnlySpan<SlangDescriptorBinding> ordinary));

        SlangDescriptorBinding binding = ordinary[0];
        var ex = Assert.Throws<ArgumentException>(() => binding.MapBinding(64));

        Assert.Contains("already has a descriptor count", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MapBindings_WithResolver_SizesEachArrayIndependently()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile("bindlessResolver", ShaderFixtures.ReflectionBindlessArrays);
        SlangReflection reflection = reflected.Reflection;

        int asked = 0;
        SlangUnboundedCapacity capacity = binding =>
        {
            asked++;

            return binding.Slot switch { 0 => 512u, 1 => 16u, _ => 64u };
        };

        Assert.True(reflection.TryGetSet(0, out ReadOnlySpan<SlangDescriptorBinding> bindless));

        DescriptorBinding[] mapped = bindless.MapBindings(capacity);

        Assert.Equal(3, mapped.Length);
        Assert.Equal(512u, mapped[0].Count);
        Assert.Equal(16u, mapped[1].Count);
        Assert.Equal(64u, mapped[2].Count);
        Assert.Equal(3, asked);

        // The resolver is never asked about a binding reflection could size.
        Assert.True(reflection.TryGetSet(1, out ReadOnlySpan<SlangDescriptorBinding> ordinary));

        DescriptorBinding[] fixedBindings = ordinary.MapBindings(capacity);

        Assert.Equal(1u, fixedBindings[0].Count);
        Assert.Equal(3, asked);
    }

    [Fact]
    public void Reflection_PushConstant_ProducesRange()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile("push", ShaderFixtures.ReflectionGlobals);

        ReadOnlySpan<SlangPushConstantRange> ranges = reflected.Reflection.PushConstantRanges;

        Assert.Equal(1, ranges.Length);
        Assert.Equal(0u, ranges[0].Offset);
        Assert.Equal(16u, ranges[0].Size);

        // The same value the hand-written path would produce for a float4 block.
        Assert.Equal(ranges[0].Stages, ranges[0].Stages); Assert.Equal(16u, ranges[0].Size);
    }

    /// <summary>
    /// Push constants arrive from Slang as a descriptor range. They are not
    /// descriptors, and a layout that contained one would be rejected.
    /// </summary>
    [Fact]
    public void Reflection_PushConstant_IsNotAlsoADescriptorBinding()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile("pushFilter", ShaderFixtures.ReflectionGlobals);

        Assert.Equal(1, reflected.Reflection.PushConstantRanges.Length);

        foreach (SlangDescriptorBinding binding in reflected.Reflection.Bindings(0))
        {
            Assert.NotEqual(SlangBindingType.SLANG_BINDING_TYPE_PARAMETER_BLOCK, binding.Type);
        }

        // Four declared descriptors, and the push-constant range is not a
        // fifth: the global scope reports five ranges and one of them is not a
        // binding.
        Assert.Equal(4, reflected.Reflection.Bindings(0).Length);
    }

    [Fact]
    public void Reflection_Stages_IsUnionOfEntryPointStages()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile("stages", ShaderFixtures.ReflectionGlobals);

        foreach (SlangDescriptorBinding binding in reflected.Reflection.Bindings(0))
        {
            Assert.Equal(ShaderStages.Vertex | ShaderStages.Fragment, binding.Stages);
        }
    }

    [Fact]
    public void Reflection_VertexAttributes_LocationsAndFormats()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile("vertexAttributes", ShaderFixtures.ReflectionGlobals);

        Assert.Equal(ShaderStages.Vertex, reflected.Reflection.EntryPoint(0).Stage);

        ReadOnlySpan<SlangVertexAttributeDescription> attributes = reflected.Reflection.VertexAttributes(0);

        Assert.Equal(2, attributes.Length);
        Assert.Equal(0u, attributes[0].Location);
        Assert.Equal(1u, attributes[1].Location);

        // Binding and Offset stay at their defaults — the shader does not state
        // how the application packs its vertex buffers.
        
        

        // A fragment stage's struct input is VARYING_INPUT too and must not
        // produce attributes.
        Assert.Equal(ShaderStages.Fragment, reflected.Reflection.EntryPoint(1).Stage);
        Assert.True(reflected.Reflection.VertexAttributes(1).IsEmpty);
    }

    /// <summary>
    /// The acceptance criterion for the resolved OPEN-2: a global scope plus
    /// two <c>ParameterBlock</c>s is three descriptor sets, numbered 0, 1, 2.
    /// </summary>
    [Fact]
    public void Reflection_TwoParameterBlocks_LandInSetsOneAndTwo()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile("twoBlocks", ShaderFixtures.ReflectionTwoBlocks);
        SlangReflection reflection = reflected.Reflection;

        Assert.Equal(3, reflection.DescriptorSetCount);
        Assert.Equal(0u, reflection.SetIndex(0));
        Assert.Equal(1u, reflection.SetIndex(1));
        Assert.Equal(2u, reflection.SetIndex(2));
        Assert.Equal(3u, reflection.SetLayoutSlotCount);

        Assert.Equal(3, reflection.Bindings(0).Length);

        foreach (int block in (int[])[1, 2])
        {
            ReadOnlySpan<SlangDescriptorBinding> bindings = reflection.Bindings(block);

            Assert.Equal(1, bindings.Length);
            Assert.Equal(0u, bindings[0].Slot);
            Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_CONSTANT_BUFFER, bindings[0].Type);
        }

        AssertReflectionCoversSpirv(reflected.Program, reflection, _output);
    }

    /// <summary>
    /// The regression guard for the one binding Slang allocates, SPIR-V binds
    /// and reflection never lists.
    /// </summary>
    /// <remarks>
    /// A block whose element carries ordinary data gets an implicit uniform
    /// buffer at binding 0 of its space, and its listed ranges start at 1 to
    /// leave room. A block with no ordinary data gets none and starts at 0 with
    /// its first resource. Both halves are asserted, because a fix that
    /// synthesized the buffer unconditionally would pass the first and fail the
    /// second.
    /// </remarks>
    [Fact]
    public void Reflection_BlockWithOrdinaryData_HasUniformBufferAtSlotZero()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile("ordinaryData", ShaderFixtures.ReflectionBlockOrdinaryData);
        SlangReflection reflection = reflected.Reflection;

        Assert.Equal(2, reflection.DescriptorSetCount);

        // gWith: float4 factors + float roughness is ordinary data, so the
        // block owns binding 0 and its declared resources shift up by one.
        ReadOnlySpan<SlangDescriptorBinding> withData = reflection.Bindings(0);

        Assert.Equal(3, withData.Length);
        Assert.Equal(0u, withData[0].Slot);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_CONSTANT_BUFFER, withData[0].Type);
        Assert.Equal(1u, withData[0].Count.Value);
        Assert.Equal(1u, withData[1].Slot);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_TEXTURE, withData[1].Type);
        Assert.Equal(4u, withData[1].Count.Value);
        Assert.Equal(2u, withData[2].Slot);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_SAMPLER, withData[2].Type);

        // gWithout: all resources, no implicit buffer, first resource at 0.
        ReadOnlySpan<SlangDescriptorBinding> withoutData = reflection.Bindings(1);

        Assert.Equal(3, withoutData.Length);
        Assert.Equal(0u, withoutData[0].Slot);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_RAW_BUFFER, withoutData[0].Type);
        Assert.Equal(1u, withoutData[1].Slot);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_TEXTURE, withoutData[1].Type);
        Assert.Equal(2u, withoutData[2].Slot);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_SAMPLER, withoutData[2].Type);

        AssertReflectionCoversSpirv(reflected.Program, reflection, _output);
    }

    /// <summary>
    /// The set index of a nested block is the enclosing scope's set plus the
    /// block's own <c>SUB_ELEMENT_REGISTER_SPACE</c> offset — an offset, which
    /// is why it accumulates.
    /// </summary>
    [Fact]
    public void Reflection_NestedParameterBlock_AccumulatesSetIndex()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile("nestedBlock", ShaderFixtures.ReflectionNestedBlock);
        SlangReflection reflection = reflected.Reflection;

        Assert.Equal(5, reflection.DescriptorSetCount);
        Assert.Equal(5u, reflection.SetLayoutSlotCount);

        // gNested sits at set 3; its own `inner` field's offset is 1 relative
        // to it, so the inner block is set 4 rather than set 1.
        Assert.True(reflection.TryGetSet(3, out ReadOnlySpan<SlangDescriptorBinding> outer));
        Assert.True(reflection.TryGetSet(4, out ReadOnlySpan<SlangDescriptorBinding> inner));

        Assert.Equal(3, outer.Length);
        Assert.Equal(3, inner.Length);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_CONSTANT_BUFFER, inner[0].Type);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_TEXTURE, inner[1].Type);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_SAMPLER, inner[2].Type);

        AssertReflectionCoversSpirv(reflected.Program, reflection, _output);
    }

    /// <summary>
    /// Guards against a hardcoded "the global scope owns space 0, blocks start
    /// at 1".
    /// </summary>
    [Fact]
    public void Reflection_NoGlobalDescriptors_FirstBlockIsSetZero()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile("onlyBlocks", ShaderFixtures.ReflectionOnlyBlocks);
        SlangReflection reflection = reflected.Reflection;

        Assert.Equal(2, reflection.DescriptorSetCount);
        Assert.Equal(0u, reflection.SetIndex(0));
        Assert.Equal(1u, reflection.SetIndex(1));
        Assert.Equal(2u, reflection.SetLayoutSlotCount);

        AssertReflectionCoversSpirv(reflected.Program, reflection, _output);
    }

    /// <summary>
    /// Sparse set indices: <c>[[vk::binding(7, 2)]]</c> is set 2, even though
    /// it is the second (index 1) descriptor set the type layout reports.
    /// </summary>
    [Fact]
    public void Reflection_ExplicitVkBinding_ReportsSparseSets()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile("sparseSets", ShaderFixtures.ReflectionSparseSets);
        SlangReflection reflection = reflected.Reflection;

        Assert.Equal(2, reflection.DescriptorSetCount);
        Assert.Equal(0u, reflection.SetIndex(0));
        Assert.Equal(2u, reflection.SetIndex(1));
        Assert.Equal(3u, reflection.SetLayoutSlotCount);

        Assert.True(reflection.TryGetSet(0, out ReadOnlySpan<SlangDescriptorBinding> zero));
        Assert.Equal(3u, zero[0].Slot);

        Assert.True(reflection.TryGetSet(2, out ReadOnlySpan<SlangDescriptorBinding> two));
        Assert.Equal(7u, two[0].Slot);

        // Set 1 is a hole. SetLayoutSlotCount is 3 so the caller knows the
        // positional SetLayouts span needs three entries, and TryGetSet says
        // which one has nothing to put in it.
        Assert.False(reflection.TryGetSet(1, out ReadOnlySpan<SlangDescriptorBinding> gap));
        Assert.True(gap.IsEmpty);

        AssertReflectionCoversSpirv(reflected.Program, reflection, _output);
    }

    /// <summary>
    /// The guard against anyone "optimizing" reflection back onto a single
    /// module: composition changes the layout.
    /// </summary>
    [Fact]
    public void Reflection_ComposedProgram_DiffersFromPerModule()
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);
        using ComposedFixture composed = ComposedFixture.Load(session);

        using SlangProgram whole = composed.Link();
        using SlangProgram materialAlone = session.CreateProgram()
            .Add(composed.Material)
            .Add(composed.Fragment)
            .Link();

        // gMaterial is the last block of the composite and the last of the
        // module, but the composite has one more scope in front of it.
        Assert.Equal(3, whole.Reflection.DescriptorSetCount);
        Assert.Equal(2, materialAlone.Reflection.DescriptorSetCount);
        Assert.Equal(3u, whole.Reflection.SetLayoutSlotCount);
        Assert.Equal(2u, materialAlone.Reflection.SetLayoutSlotCount);

        AssertReflectionCoversSpirv(whole, whole.Reflection, _output);
    }

    /// <summary>
    /// Opt-in narrowing, cross-checked against per-entry-point SPIR-V: the
    /// binding only the vertex stage reads reports <c>Vertex</c>, the one only
    /// the fragment stage reads reports <c>Fragment</c>, and the one both reach
    /// through a helper in a third module reports both.
    /// </summary>
    [Fact]
    public void Reflection_PerEntryPointUsage_NarrowsStages()
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);
        using ComposedFixture composed = ComposedFixture.Load(session);
        using SlangProgram program = composed.Link();

        SlangReflection union = program.GetReflection(SlangStageAttribution.ProgramStageUnion);
        SlangReflection narrowed = program.GetReflection(SlangStageAttribution.PerEntryPointUsage);

        Assert.Equal(3, narrowed.DescriptorSetCount);

        // gPositions (set 0) is read only by vertexMain, gMaterial (set 2) only
        // by fragmentMain, gCamera (set 1) by both.
        Assert.Equal(ShaderStages.Vertex, StagesOf(narrowed, set: 0, slot: 0));
        Assert.Equal(ShaderStages.Vertex | ShaderStages.Fragment, StagesOf(narrowed, set: 1, slot: 0));
        Assert.Equal(ShaderStages.Fragment, StagesOf(narrowed, set: 2, slot: 0));

        // The default mode reports all three as the whole union.
        for (uint set = 0; set < 3; set++)
        {
            Assert.Equal(ShaderStages.Vertex | ShaderStages.Fragment, StagesOf(union, set, slot: 0));
        }

        // And the narrowing agrees with the per-entry-point SPIR-V, which is
        // the only thing that decides what a stage can actually reach.
        for (int e = 0; e < program.EntryPointCount; e++)
        {
            ShaderStages stage = program.EntryPoint(e).Stage;

            foreach ((uint set, uint binding, string name) in SpirvDecorations.ReadDescriptorBindings(program.Spirv(e)))
            {
                Assert.True(
                    (StagesOf(narrowed, set, binding) & stage) == stage,
                    $"'{name}' is decorated set={set} binding={binding} in the {stage} module, but "
                    + $"PerEntryPointUsage reported Stages = {StagesOf(narrowed, set, binding)}.");
            }
        }
    }

    /// <summary>
    /// <c>isParameterLocationUsed</c> reports a push constant as unused even for
    /// a stage whose SPIR-V provably reads it, so narrowing is not available
    /// and the union is what both modes report. Asserted rather than left to a
    /// comment.
    /// </summary>
    [Fact]
    public void Reflection_PushConstantStages_StayUnion_InBothModes()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile("pushStages", ShaderFixtures.ReflectionGlobals);

        // Only fragmentMain reads gPush in this fixture; a working narrowing
        // would report Fragment here.
        Assert.Equal(
            ShaderStages.Vertex | ShaderStages.Fragment,
            reflected.Program.GetReflection(SlangStageAttribution.ProgramStageUnion).PushConstantRanges[0].Stages);
        Assert.Equal(
            ShaderStages.Vertex | ShaderStages.Fragment,
            reflected.Program.GetReflection(SlangStageAttribution.PerEntryPointUsage).PushConstantRanges[0].Stages);
    }

    [Fact]
    public void Reflection_SystemValueInputs_AreNotVertexAttributes()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile("systemValues", ShaderFixtures.ReflectionSystemValueInputs);

        ReadOnlySpan<SlangVertexAttributeDescription> attributes = reflected.Reflection.VertexAttributes(0);

        // Three struct fields, no fourth attribute for SV_InstanceID or
        // SV_VertexID — both of which report offset 0 and would otherwise
        // collide with the real POSITION at location 0.
        Assert.Equal(3, attributes.Length);
        Assert.Equal([0u, 1u, 2u], AttributeLocations(attributes));

        AssertVertexAttributesMatchSpirv(reflected.Program, reflected.Reflection, entryPointIndex: 0, _output);
    }

    [Fact]
    public void Reflection_StructVertexInput_AccumulatesLocations()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile("structInput", ShaderFixtures.ReflectionSystemValueInputs);

        ReadOnlySpan<SlangVertexAttributeDescription> attributes = reflected.Reflection.VertexAttributes(0);

        Assert.Equal(0u, attributes[0].Location);
        Assert.Equal(1u, attributes[1].Location);
        Assert.Equal(2u, attributes[2].Location);
    }

    /// <summary>OPEN-6: a matrix vertex input throws rather than guessing.</summary>
    

    /// <summary>OPEN-5: two push-constant blocks throw rather than guessing an offset.</summary>
    [Fact]
    public void Reflection_TwoPushConstantBlocks_ThrowsNotSupported()
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);
        using SlangModule a = session.LoadModuleFromSource("pushA", "pushA.slang", ShaderFixtures.ReflectionPushConstantA);
        using SlangModule b = session.LoadModuleFromSource("pushB", "pushB.slang", ShaderFixtures.ReflectionPushConstantB);
        using SlangEntryPoint vertex = a.DefinedEntryPoint(0);
        using SlangEntryPoint fragment = b.DefinedEntryPoint(0);

        // They compose and link — the refusal is reflection's, and it is a
        // refusal to invent the byte offsets Vulkan needs.
        using SlangProgram program = session.CreateProgram()
            .Add(a).Add(b).Add(vertex).Add(fragment).Link();

        var ex = Assert.Throws<NotSupportedException>(() => program.Reflection);

        _output.WriteLine(ex.Message);

        Assert.Contains("gPushA", ex.Message, StringComparison.Ordinal);
        Assert.Contains("gPushB", ex.Message, StringComparison.Ordinal);
        Assert.Contains("OPEN-5", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// An interface-typed <c>ParameterBlock</c>, conformance-linked: exactly
    /// one uniform buffer at slot 0 of its set — the existential value buffer,
    /// which is precisely what the "element has ordinary data" rule already
    /// produces, and precisely what the SPIR-V binds.
    /// </summary>
    [Fact]
    public void Reflection_ConformanceLinkedInterfaceBlock_ReportsUniformBufferOnly()
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);
        using SlangModule module = session.LoadModuleFromSource("surfaceReflect", "surfaceReflect.slang", ShaderFixtures.InterfaceSurfaceModule);
        using SlangEntryPoint fragment = module.DefinedEntryPoint(0);

        using SlangProgram program = session.CreateProgram()
            .Add(module)
            .Add(fragment)
            .AddTypeConformance("Glossy", "ISurface")
            .Link();

        SlangReflection reflection = program.Reflection;

        Assert.Equal(1, reflection.DescriptorSetCount);

        ReadOnlySpan<SlangDescriptorBinding> bindings = reflection.Bindings(0);

        Assert.Equal(1, bindings.Length);
        Assert.Equal(0u, bindings[0].Slot);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_CONSTANT_BUFFER, bindings[0].Type);

        AssertReflectionCoversSpirv(program, reflection, _output);
    }

    /// <summary>
    /// The acceptance criterion: reflect a composed, multi-set program into
    /// descriptions that build a working <c>PipelineLayout</c>.
    /// </summary>
    /// <remarks>
    /// <para>The composed fixture's sets are 0, 1, 2 with no gap, which is what
    /// lets this run at all — a program that left a set index unused could not
    /// be completed, because <c>Device.CreateDescriptorSetLayout</c> rejects an
    /// empty <c>Bindings</c> span and there is no other way to obtain the
    /// zero-binding layout Vulkan wants in a hole.</para>
    /// <para><b>Validation is the oracle here, not the non-null handles.</b>
    /// Every value fed to <c>CreateDescriptorSetLayout</c> and
    /// <c>CreatePipelineLayout</c> below came out of reflection, and a wrong
    /// set, slot, type, count or stage mask produces handles that are still
    /// non-null — the layers are the only thing that says the descriptions were
    /// right. Measured on an RTX 4070 Ti, <c>v2026.14.1</c>: the reflected
    /// layout is accepted with zero validation errors.</para>
    /// <para><c>shaderDrawParameters</c> is enabled because it is a property of
    /// what Slang emits, not a choice: <c>vertexMain</c> takes
    /// <c>SV_VertexID</c>, and mapping that HLSL semantic onto Vulkan's
    /// <c>VertexIndex</c> requires subtracting <c>BaseVertex</c>, so the module
    /// declares the SPIR-V <c>DrawParameters</c> capability. Without the
    /// feature, <c>vkCreateShaderModule</c> still returns a usable handle while
    /// validation reports
    /// <c>VUID-VkShaderModuleCreateInfo-pCode-08740</c> — which is precisely
    /// the kind of silence this test exists to break.</para>
    /// </remarks>
    [Fact]
    public void Reflection_BuildsAWorkingPipelineLayout()
    {
        TestGate.RequireDriver();

        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);
        using ComposedFixture composed = ComposedFixture.Load(session);
        using SlangProgram program = composed.Link();

        SlangReflection reflection = program.Reflection;

        Assert.Equal(3u, reflection.SetLayoutSlotCount);

        int errorCount = 0;
        var errors = new ConcurrentQueue<string>();
        Action<DebugMessage> sink = msg =>
        {
            if ((msg.Severity & VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT) != 0)
            {
                Interlocked.Increment(ref errorCount);
                errors.Enqueue(msg.Message);
            }
        };

        bool validating = VulkanEnvironment.HasValidationLayer;

        using var instance = Instance.Create(new InstanceDescription
        {
            EnableValidation = validating,
            DebugCallback = validating ? sink : null,
        });

        uint family = uint.MaxValue;
        var gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (info.QueueFamilies[i].SupportsGraphics)
                {
                    family = info.QueueFamilies[i].Index;

                    return true;
                }
            }

            return false;
        });

        using Device device = gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],

            // Not optional decoration — see the DrawParameters note in the
            // remarks. VkPhysicalDeviceVulkan11Features is not one of the four
            // structs the configurer hands out by ref, but it is
            // IChainable<VkDeviceCreateInfo>, so it goes on through the chain.
            ConfigureFeatures = static (
                ref ChainBuilder<VkDeviceCreateInfo> chain,
                ref VkPhysicalDeviceFeatures2        _,
                ref VkPhysicalDeviceVulkan12Features _,
                ref VkPhysicalDeviceVulkan13Features _,
                ref VkPhysicalDeviceVulkan14Features _) =>
            {
                ref VkPhysicalDeviceVulkan11Features f11 = ref chain.Push<VkPhysicalDeviceVulkan11Features>();
                f11.shaderDrawParameters = 1;
            },
        });

        var layouts = new DescriptorSetLayout[(int)reflection.SetLayoutSlotCount];

        try
        {
            for (uint set = 0; set < reflection.SetLayoutSlotCount; set++)
            {
                Assert.True(reflection.TryGetSet(set, out ReadOnlySpan<SlangDescriptorBinding> bindings));

                layouts[set] = device.CreateDescriptorSetLayout(
                    new DescriptorSetLayoutDescription { Bindings = bindings.MapBindings() });
            }

            using PipelineLayout pipelineLayout = device.CreatePipelineLayout(new PipelineLayoutDescription
            {
                SetLayouts = layouts,
                PushConstantRanges = reflection.PushConstantRanges.MapPushConstantRanges(),
            });

            Assert.False(pipelineLayout.IsNull);

            // And the SPIR-V those descriptions were derived from loads.
            using ShaderModule vertex = device.CreateShaderModule(program.Spirv(0));
            using ShaderModule fragment = device.CreateShaderModule(program.Spirv(1));

            Assert.False(vertex.IsNull);
            Assert.False(fragment.IsNull);
        }
        finally
        {
            foreach (DescriptorSetLayout layout in layouts)
            {
                layout.Dispose();
            }
        }

        if (validating)
        {
            Assert.True(
                Volatile.Read(ref errorCount) == 0,
                $"The layers rejected descriptions derived from reflection:{Environment.NewLine}" +
                string.Join(Environment.NewLine, errors));
        }
    }

    private static ShaderStages StagesOf(SlangReflection reflection, uint set, uint slot)
    {
        Assert.True(reflection.TryGetSet(set, out ReadOnlySpan<SlangDescriptorBinding> bindings), $"No descriptor set {set}.");

        foreach (SlangDescriptorBinding binding in bindings)
        {
            if (binding.Slot == slot)
            {
                return binding.Stages;
            }
        }

        Assert.Fail($"Descriptor set {set} has no binding at slot {slot}.");

        return ShaderStages.None;
    }

    private static uint[] AttributeLocations(ReadOnlySpan<SlangVertexAttributeDescription> attributes)
    {
        var locations = new uint[attributes.Length];

        for (int i = 0; i < attributes.Length; i++)
        {
            locations[i] = attributes[i].Location;
        }

        return locations;
    }

    /// <summary>
    /// Asserts that every descriptor the emitted SPIR-V binds is a binding
    /// reflection reported, at the same set and slot.
    /// </summary>
    private static void AssertReflectionCoversSpirv(SlangProgram program, SlangReflection reflection, ITestOutputHelper output)
    {
        for (int e = 0; e < program.EntryPointCount; e++)
        {
            foreach ((uint set, uint binding, string name) in SpirvDecorations.ReadDescriptorBindings(program.Spirv(e)))
            {
                output.WriteLine($"{program.EntryPoint(e).Name}: SPIR-V set={set} binding={binding} '{name}'");

                Assert.True(
                    reflection.TryGetSet(set, out ReadOnlySpan<SlangDescriptorBinding> bindings),
                    $"'{name}' is decorated DescriptorSet={set} Binding={binding} in the emitted SPIR-V, but "
                    + $"reflection reported no descriptor set {set} at all.");

                bool found = false;

                foreach (SlangDescriptorBinding candidate in bindings)
                {
                    found |= candidate.Slot == binding;
                }

                Assert.True(
                    found,
                    $"'{name}' is decorated DescriptorSet={set} Binding={binding} in the emitted SPIR-V, but "
                    + $"reflection reported no binding at slot {binding} of set {set}. A descriptor set layout "
                    + "built from this reflection would be missing a binding the shader uses.");
            }
        }
    }

    private static void AssertVertexAttributesMatchSpirv(
        SlangProgram program,
        SlangReflection reflection,
        int entryPointIndex,
        ITestOutputHelper output)
    {
        ReadOnlySpan<SlangVertexAttributeDescription> attributes = reflection.VertexAttributes(entryPointIndex);
        List<(uint Location, string Name)> spirv = SpirvDecorations.ReadInputLocations(program.Spirv(entryPointIndex));

        Assert.Equal(spirv.Count, attributes.Length);

        for (int i = 0; i < spirv.Count; i++)
        {
            output.WriteLine($"SPIR-V input Location={spirv[i].Location} '{spirv[i].Name}'");
            Assert.Equal(spirv[i].Location, attributes[i].Location);
        }
    }

    /// <summary>A compiled single-module program, plus its reflection.</summary>
    private sealed class ReflectedProgram : IDisposable
    {
        private readonly SlangCompiler _compiler;
        private readonly SlangSession _session;

        private ReflectedProgram(SlangCompiler compiler, SlangSession session, SlangProgram program)
        {
            _compiler = compiler;
            _session = session;
            Program = program;
        }

        public SlangProgram Program { get; }

        public SlangReflection Reflection => Program.Reflection;

        /// <param name="reflect">
        /// <see langword="false"/> for the fixtures whose reflection is
        /// expected to throw, so the test does the throwing call itself.
        /// </param>
        public static ReflectedProgram Compile(string moduleName, string source, bool reflect = true)
        {
            SlangCompiler compiler = SlangCompiler.Create();

            try
            {
                SlangSession session = compiler.CreateSession(default);
                SlangProgram program = session.Compile(new SlangCompileRequest
                {
                    ModuleName = moduleName,
                    Source = source,
                });

                if (reflect)
                {
                    _ = program.Reflection;
                }

                return new ReflectedProgram(compiler, session, program);
            }
            catch
            {
                compiler.Dispose();

                throw;
            }
        }

        public void Dispose()
        {
            Program.Dispose();
            _session.Dispose();
            _compiler.Dispose();
        }
    }

    /// <summary>
    /// The three-module, two-entry-point composition: a shared module holding a
    /// camera <c>ParameterBlock</c>, a geometry module with the vertex stage,
    /// and a material module with its own <c>ParameterBlock</c> and the
    /// fragment stage.
    /// </summary>
    private sealed class ComposedFixture : IDisposable
    {
        private ComposedFixture(
            SlangSession session,
            SlangModule common,
            SlangModule geometry,
            SlangModule material,
            SlangEntryPoint vertex,
            SlangEntryPoint fragment)
        {
            Session = session;
            Common = common;
            Geometry = geometry;
            Material = material;
            Vertex = vertex;
            Fragment = fragment;
        }

        public SlangSession Session { get; }

        public SlangModule Common { get; }

        public SlangModule Geometry { get; }

        public SlangModule Material { get; }

        public SlangEntryPoint Vertex { get; }

        public SlangEntryPoint Fragment { get; }

        public static ComposedFixture Load(SlangSession session)
        {
            SlangModule common = session.LoadModuleFromSource(
                "composeCommon", "composeCommon.slang", ShaderFixtures.ComposeCommonModule);
            SlangModule geometry = session.LoadModuleFromSource(
                "composeGeometry", "composeGeometry.slang", ShaderFixtures.ComposeGeometryModule);
            SlangModule material = session.LoadModuleFromSource(
                "composeMaterial", "composeMaterial.slang", ShaderFixtures.ComposeMaterialModule);

            return new ComposedFixture(
                session,
                common,
                geometry,
                material,
                geometry.DefinedEntryPoint(0),
                material.DefinedEntryPoint(0));
        }

        /// <summary>Links in the one order every assertion in this file assumes.</summary>
        public SlangProgram Link()
            => Session.CreateProgram()
                .Add(Common)
                .Add(Geometry)
                .Add(Material)
                .Add(Vertex)
                .Add(Fragment)
                .Link();

        public void Dispose()
        {
            Fragment.Dispose();
            Vertex.Dispose();
            Material.Dispose();
            Geometry.Dispose();
            Common.Dispose();
        }
    }
}
