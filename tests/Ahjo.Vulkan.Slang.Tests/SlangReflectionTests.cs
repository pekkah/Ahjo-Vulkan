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
    [InlineData("xcheckCompute", ShaderFixtures.ReflectionComputeStorageImage)]
    [InlineData("xcheckMaterial", ShaderFixtures.ReflectionMaterialBlock)]
    [InlineData("xcheckMaterialWide", ShaderFixtures.ReflectionMaterialBlockWidened)]
    [InlineData("xcheckExplicitSpaceCb", ShaderFixtures.ReflectionExplicitSpaceConstantBuffer)]
    [InlineData("xcheckExplicitSpaceMixed", ShaderFixtures.ReflectionExplicitSpaceMixed)]
    [InlineData("xcheckExplicitSpaceTwoCbs", ShaderFixtures.ReflectionExplicitSpaceTwoConstantBuffers)]
    [InlineData("xcheckExplicitSpaceStruct", ShaderFixtures.ReflectionExplicitSpaceStructGlobal)]
    [InlineData("xcheckLooseSpace", ShaderFixtures.ReflectionLooseGlobalsWithExplicitSpace)]
    [InlineData("xcheckLooseOnly", ShaderFixtures.ReflectionLooseGlobalsOnly)]
    [InlineData("xcheckLoosePlain", ShaderFixtures.ReflectionLooseGlobalsNoExplicitBinding)]
    [InlineData("xcheckLooseBlock", ShaderFixtures.ReflectionLooseGlobalsWithParameterBlock)]

    // Issue #183's fixtures. These rows are NOT discriminating — the theory is
    // one-way, so it passes whether or not the zero-count binding is reported —
    // and are here only so the new fixtures are covered by the standing
    // cross-check. ReflectionZeroLengthArrayOnly is deliberately absent: its
    // SPIR-V decorates nothing, so the row would iterate zero times.
    [InlineData("xcheckZeroArray", ShaderFixtures.ReflectionZeroLengthArray)]
    [InlineData("xcheckZeroArrayConst", ShaderFixtures.ReflectionZeroLengthArrayFromConstant)]
    [InlineData("xcheckZeroArraySet1", ShaderFixtures.ReflectionZeroLengthArrayInOwnSet)]
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

    /// <summary>
    /// Issue #183: a zero-length resource array is a real descriptor range whose
    /// count is literally zero, and its binding number is reserved.
    /// </summary>
    /// <remarks>
    /// <para>The SPIR-V assertion is the load-bearing one and it runs in the
    /// direction the suite's standing cross-check cannot:
    /// <c>Reflection_CoversEverySetAndBinding_TheSpirvDecorates</c> is one-way
    /// (declared ⊇ used), so it passes whether or not slot 0 is reported. Here
    /// the claim is that the emitted module decorates <b>no</b> variable at
    /// binding 0 while reflection still reports one — which is exactly why the
    /// mapper, not reflection, is where the binding gets dropped.</para>
    /// </remarks>
    [Fact]
    public void Reflection_ZeroLengthArray_ReportsFixedZeroAndReservesTheSlot()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile("zeroArray", ShaderFixtures.ReflectionZeroLengthArray);

        Assert.True(reflected.Reflection.TryGetSet(0, out ReadOnlySpan<SlangDescriptorBinding> bindings));
        Assert.Equal(3, bindings.Length);

        Assert.Equal(0u, bindings[0].Slot);
        Assert.Equal("gTex", bindings[0].Name);
        Assert.Equal(SlangDescriptorCountKind.Fixed, bindings[0].Count.Kind);
        Assert.Equal(0u, bindings[0].Count.Value);
        Assert.True(bindings[0].Count.IsZero);

        // The slot is reserved: the survivors keep the numbers Slang gave them.
        Assert.Equal(1u, bindings[1].Slot);
        Assert.Equal(2u, bindings[2].Slot);

        List<(uint Set, uint Binding, string Name)> spirv =
            SpirvDecorations.ReadDescriptorBindings(reflected.Program.Spirv(0));

        foreach ((uint set, uint binding, string name) in spirv)
        {
            _output.WriteLine($"SPIR-V set={set} binding={binding} '{name}'");
        }

        Assert.Contains(spirv, d => d.Set == 0 && d.Binding == 1);
        Assert.Contains(spirv, d => d.Set == 0 && d.Binding == 2);
        Assert.DoesNotContain(spirv, d => d.Binding == 0);
    }

    /// <summary>
    /// The same shape reached without anyone typing <c>[0]</c>:
    /// <c>gMaps[NUM_MAPS]</c> with <c>NUM_MAPS = 0</c>, which is generated or
    /// parameterized shader code rather than a typo.
    /// </summary>
    [Fact]
    public void Reflection_ZeroLengthArrayFromConstant_IsTheSameShape()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile(
            "zeroArrayConst", ShaderFixtures.ReflectionZeroLengthArrayFromConstant);

        Assert.True(reflected.Reflection.TryGetSet(0, out ReadOnlySpan<SlangDescriptorBinding> bindings));
        Assert.Equal(3, bindings.Length);

        Assert.Equal(0u, bindings[0].Slot);
        Assert.Equal("gTex", bindings[0].Name);
        Assert.Equal(SlangDescriptorCountKind.Fixed, bindings[0].Count.Kind);
        Assert.Equal(0u, bindings[0].Count.Value);
        Assert.True(bindings[0].Count.IsZero);

        Assert.Equal(1u, bindings[1].Slot);
        Assert.Equal(2u, bindings[2].Slot);
    }

    /// <summary>
    /// The layout that matches the emitted SPIR-V is the one <b>without</b> the
    /// zero-count binding: a hole at the reserved number, which Vulkan permits.
    /// </summary>
    [Fact]
    public void MapBindings_ZeroCountBinding_IsOmittedFromTheLayout()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile("zeroArrayMap", ShaderFixtures.ReflectionZeroLengthArray);

        Assert.True(reflected.Reflection.TryGetSet(0, out ReadOnlySpan<SlangDescriptorBinding> bindings));

        DescriptorBinding[] mapped = bindings.MapBindings();

        Assert.Equal(2, mapped.Length);

        Assert.Equal(1u, mapped[0].Slot);
        Assert.Equal(VkDescriptorType.VK_DESCRIPTOR_TYPE_SAMPLER, mapped[0].Type);
        Assert.Equal(2u, mapped[1].Slot);
        Assert.Equal(VkDescriptorType.VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE, mapped[1].Type);

        foreach (DescriptorBinding binding in mapped)
        {
            Assert.NotEqual(0u, binding.Count);
            Assert.NotEqual(0u, binding.Slot);
        }
    }

    /// <summary>
    /// The single-binding path has no return value meaning "nothing", so it
    /// refuses and names the batch call. It must agree with
    /// <c>MapBindings</c>: emitting <c>descriptorCount = 0</c> here and omitting
    /// the binding there are both legal Vulkan and are not compatible with each
    /// other at <c>vkCmdBindDescriptorSets</c>
    /// (<c>VUID-vkCmdBindDescriptorSets-pDescriptorSets-00358</c>).
    /// </summary>
    [Fact]
    public void MapBinding_ZeroCountBinding_Throws()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile("zeroArrayOne", ShaderFixtures.ReflectionZeroLengthArray);

        Assert.True(reflected.Reflection.TryGetSet(0, out ReadOnlySpan<SlangDescriptorBinding> bindings));

        SlangDescriptorBinding binding = bindings[0];
        var ex = Assert.Throws<NotSupportedException>(() => binding.MapBinding());

        _output.WriteLine(ex.Message);

        Assert.Contains("binding 0", ex.Message, StringComparison.Ordinal);
        Assert.Contains("gTex", ex.Message, StringComparison.Ordinal);
        Assert.Contains("zero descriptors", ex.Message, StringComparison.Ordinal);
        Assert.Contains("MapBindings", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The capacity overload will not size a zero-length array either: reserving
    /// descriptors there would put descriptors in the layout that no shader code
    /// can index.
    /// </summary>
    /// <remarks>
    /// <b>The message assertion is load-bearing.</b> The pre-existing
    /// "already has a descriptor count" branch throws the same exception type
    /// for the same input, so a type-only assertion here could not go red when
    /// the zero clause is deleted.
    /// </remarks>
    [Fact]
    public void MapBinding_WithCapacity_OnZeroCountBinding_Throws()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile("zeroArrayCap", ShaderFixtures.ReflectionZeroLengthArray);

        Assert.True(reflected.Reflection.TryGetSet(0, out ReadOnlySpan<SlangDescriptorBinding> bindings));

        SlangDescriptorBinding binding = bindings[0];
        var ex = Assert.Throws<ArgumentException>(() => binding.MapBinding(64));

        _output.WriteLine(ex.Message);

        Assert.Contains("zero descriptors", ex.Message, StringComparison.Ordinal);
        Assert.Contains("E30029", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The resolver overload omits the same bindings, and never asks the
    /// resolver about one — it never did, since a zero count is
    /// <see cref="SlangDescriptorCountKind.Fixed"/>.
    /// </summary>
    [Fact]
    public void MapBindings_WithResolver_OmitsZeroCountWithoutAskingTheResolver()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile(
            "zeroArrayResolver", ShaderFixtures.ReflectionZeroLengthArray);

        int asked = 0;
        SlangUnboundedCapacity capacity = _ =>
        {
            asked++;

            return 1u;
        };

        Assert.True(reflected.Reflection.TryGetSet(0, out ReadOnlySpan<SlangDescriptorBinding> bindings));

        DescriptorBinding[] mapped = bindings.MapBindings(capacity);

        Assert.Equal(2, mapped.Length);
        Assert.Equal(1u, mapped[0].Slot);
        Assert.Equal(2u, mapped[1].Slot);
        Assert.Equal(0, asked);
    }

    /// <summary>
    /// A set whose every binding is a zero-length array maps to <b>no</b>
    /// bindings at all. That is the layout matching the emitted SPIR-V, which
    /// decorates nothing, and since issue #191
    /// <c>Device.CreateDescriptorSetLayout</c> accepts an empty <c>Bindings</c>
    /// span and produces exactly it. The gap #183 recorded here is closed.
    /// </summary>
    [Fact]
    public void MapBindings_EverythingZeroCount_ReturnsEmpty()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile(
            "zeroArrayOnly", ShaderFixtures.ReflectionZeroLengthArrayOnly);

        // Reflection still reports the binding — that is its job.
        Assert.True(reflected.Reflection.TryGetSet(0, out ReadOnlySpan<SlangDescriptorBinding> bindings));
        Assert.Equal(1, bindings.Length);
        Assert.True(bindings[0].Count.IsZero);

        SlangDescriptorBinding[] copy = bindings.ToArray();

        Assert.Empty(((ReadOnlySpan<SlangDescriptorBinding>)copy).MapBindings());
    }

    /// <summary>
    /// One dead array does not make a program unmappable — only the set that
    /// consists entirely of dead arrays. This is the #176 consistency claim.
    /// </summary>
    [Fact]
    public void MapBindings_ZeroCountInItsOwnSet_StillMapsTheOtherSet()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile(
            "zeroArraySet1", ShaderFixtures.ReflectionZeroLengthArrayInOwnSet);

        Assert.True(reflected.Reflection.TryGetSet(0, out ReadOnlySpan<SlangDescriptorBinding> live));

        DescriptorBinding[] mapped = live.MapBindings();

        Assert.Equal(2, mapped.Length);
        Assert.Equal(0u, mapped[0].Slot);
        Assert.Equal(1u, mapped[1].Slot);

        Assert.True(reflected.Reflection.TryGetSet(1, out ReadOnlySpan<SlangDescriptorBinding> dead));
        Assert.Equal(1, dead.Length);
        Assert.True(dead[0].Count.IsZero);

        SlangDescriptorBinding[] copy = dead.ToArray();

        // Zero bindings, not one. That count is what discriminates "MapBindings
        // omits the dead array" from "MapBindings maps every binding" — the
        // latter would produce one binding here (of count 0, which Ahjo.Vulkan
        // then normalizes to 1, issue #119).
        Assert.Empty(((ReadOnlySpan<SlangDescriptorBinding>)copy).MapBindings());
    }

    /// <summary>
    /// The resolver overload agrees, and reaches the same answer without asking
    /// the resolver anything — the second of the two refusal sites issue #191
    /// deleted, which nothing else covers.
    /// </summary>
    [Fact]
    public void MapBindings_WithResolver_EverythingZeroCount_ReturnsEmptyWithoutAsking()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile(
            "zeroArrayOnlyResolver", ShaderFixtures.ReflectionZeroLengthArrayOnly);

        int asked = 0;
        SlangUnboundedCapacity capacity = _ =>
        {
            asked++;

            return 1u;
        };

        Assert.True(reflected.Reflection.TryGetSet(0, out ReadOnlySpan<SlangDescriptorBinding> bindings));

        Assert.Empty(bindings.MapBindings(capacity));
        Assert.Equal(0, asked);
    }

    /// <summary>
    /// Provenance does not matter: a hand-built <c>Fixed(0)</c> and a zeroed
    /// span element are treated exactly like a reflected zero-length array. No
    /// shader and no compiler here.
    /// </summary>
    [Fact]
    public void MapBinding_HandBuiltZeroCount_BehavesLikeAReflectedOne()
    {
        var handBuilt = new SlangDescriptorBinding
        {
            Slot = 4,
            Name = "hand",
            Type = SlangBindingType.SLANG_BINDING_TYPE_TEXTURE,
            Count = SlangDescriptorCount.Fixed(0),
            Stages = ShaderStages.Fragment,
        };

        var ex = Assert.Throws<NotSupportedException>(() => handBuilt.MapBinding());

        _output.WriteLine(ex.Message);

        Assert.Contains("binding 4", ex.Message, StringComparison.Ordinal);
        Assert.Contains("hand", ex.Message, StringComparison.Ordinal);

        // A zeroed span element is a zero-count binding …
        Assert.True(default(SlangDescriptorBinding).Count.IsZero);

        // … while the parameterless constructor still supplies Fixed(1), which
        // is the valid-by-default rule issue #119 exists for.
        Assert.False(new SlangDescriptorBinding().Count.IsZero);
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
    /// Issue #180: a global-scope <c>ConstantBuffer&lt;T&gt;</c> with an
    /// explicit <c>[[vk::binding(0, 1)]]</c> is reported in set <b>1</b>, the
    /// space it declares and the one the emitted SPIR-V decorates.
    /// </summary>
    /// <remarks>
    /// <para>Before the fix, Slang's descriptor-set view put the buffer's range
    /// in the record for space 0 with its binding index intact, so reflection
    /// reported <em>two</em> bindings at <c>(0,0)</c> — the texture and the
    /// constant buffer — no set 1 at all, and the texture renamed
    /// <c>gXform</c>. The <c>bindings.Length == 2</c> assertion on set 0 is the
    /// one that catches the duplicate; <c>DescriptorSetCount == 2</c> is the one
    /// that catches the missing set.</para>
    /// <para>Both halves of the <c>TryGetBufferLayout</c> pair are required:
    /// buffer layouts are keyed off the same facts dictionary as the names, so
    /// before the fix <c>Xform</c>'s member layout hung off the
    /// <em>texture</em>'s slot.</para>
    /// <para><b>Stage attribution across a corrected set was measured, not
    /// assumed</b> (spec's bounded uncertainty).
    /// <c>IMetadata::isParameterLocationUsed</c> is queried with the reported —
    /// i.e. corrected — set number. <b>Observed on <c>v2026.14.1</c> / win-x64:
    /// <c>Vertex | Fragment</c> for <c>gXform</c> at set 1 slot 0.</b> That is
    /// the same value as this fixture's program union, so it was measured a
    /// second time with the union fallback temporarily removed from
    /// <c>ApplyStages</c> — the value did not change, so the query genuinely
    /// answers <see langword="true"/> for a corrected set number in both entry
    /// points rather than falling back. Both stages do read
    /// <c>gXform.mvp</c>, so <c>Vertex | Fragment</c> is also the correct narrow
    /// answer.</para>
    /// <para>The assertion is deliberately only "not <c>None</c>": a failed or
    /// <see langword="false"/> query falls back to the program union, which is
    /// always a legal <c>stageFlags</c>, so a wide answer would be acceptable
    /// here too and pinning the exact mask would pin Slang's metadata behaviour
    /// rather than this correction.</para>
    /// </remarks>
    [Fact]
    public void Reflection_ExplicitVkBindingConstantBuffer_LandsInTheDeclaredSpace()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile(
            "explicitSpaceCb", ShaderFixtures.ReflectionExplicitSpaceConstantBuffer);
        SlangReflection reflection = reflected.Reflection;

        Assert.Equal(2, reflection.DescriptorSetCount);
        Assert.Equal(0u, reflection.SetIndex(0));
        Assert.Equal(1u, reflection.SetIndex(1));
        Assert.Equal(2u, reflection.SetLayoutSlotCount);

        Assert.True(reflection.TryGetSet(0, out ReadOnlySpan<SlangDescriptorBinding> zero));

        // Two, not three: the constant buffer must not also be in here.
        Assert.Equal(2, zero.Length);
        Assert.Equal(0u, zero[0].Slot);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_TEXTURE, zero[0].Type);
        Assert.Equal("gAlbedo", zero[0].Name);
        Assert.Equal(1u, zero[1].Slot);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_SAMPLER, zero[1].Type);
        Assert.Equal("gSampler", zero[1].Name);

        Assert.True(reflection.TryGetSet(1, out ReadOnlySpan<SlangDescriptorBinding> one));

        Assert.Equal(1, one.Length);
        Assert.Equal(0u, one[0].Slot);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_CONSTANT_BUFFER, one[0].Type);
        Assert.Equal("gXform", one[0].Name);

        // The buffer layout moved with the binding, and did not stay behind on
        // the texture's slot.
        Assert.True(reflection.TryGetBufferLayout(1, 0, out SlangBufferLayout? layout));
        Assert.Equal(1, layout.Members.Length);
        Assert.Equal("mvp", layout.Members[0].Name);
        Assert.False(reflection.TryGetBufferLayout(0, 0, out _));

        // Stage attribution against the corrected set number — measured.
        SlangReflection narrowed = reflected.Program.GetReflection(SlangStageAttribution.PerEntryPointUsage);
        ShaderStages stages = StagesOf(narrowed, set: 1, slot: 0);

        _output.WriteLine($"PerEntryPointUsage stages for gXform at set 1 slot 0: {stages}");

        Assert.NotEqual(ShaderStages.None, stages);

        AssertReflectionCoversSpirv(reflected.Program, reflection, _output);
    }

    /// <summary>
    /// Issue #180: two constant buffers in two distinct non-zero spaces stay two
    /// bindings, in the two sets they declare.
    /// </summary>
    /// <remarks>
    /// Before the fix both folded to <c>(0,0)</c> and reflection reported the
    /// same binding twice, both named <c>gB</c> — <c>gA</c> and its buffer
    /// layout were unreachable through the API entirely. The
    /// <c>TryGetBufferLayout</c> pair at the end is the point of the fixture:
    /// each key must yield the buffer that declares it.
    /// </remarks>
    [Fact]
    public void Reflection_TwoConstantBuffersInDistinctSpaces_StayDistinct()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile(
            "explicitSpaceTwoCbs", ShaderFixtures.ReflectionExplicitSpaceTwoConstantBuffers);
        SlangReflection reflection = reflected.Reflection;

        Assert.Equal(2, reflection.DescriptorSetCount);
        Assert.Equal(1u, reflection.SetIndex(0));
        Assert.Equal(2u, reflection.SetIndex(1));

        // Set 0 is a hole — the same shape Reflection_ExplicitVkBinding_
        // ReportsSparseSets documents, and SetLayoutSlotCount says the
        // positional SetLayouts span still needs three entries.
        Assert.Equal(3u, reflection.SetLayoutSlotCount);
        Assert.False(reflection.TryGetSet(0, out ReadOnlySpan<SlangDescriptorBinding> gap));
        Assert.True(gap.IsEmpty);

        Assert.True(reflection.TryGetSet(1, out ReadOnlySpan<SlangDescriptorBinding> one));
        Assert.Equal(1, one.Length);
        Assert.Equal(0u, one[0].Slot);
        Assert.Equal("gA", one[0].Name);

        Assert.True(reflection.TryGetSet(2, out ReadOnlySpan<SlangDescriptorBinding> two));
        Assert.Equal(1, two.Length);
        Assert.Equal(0u, two[0].Slot);
        Assert.Equal("gB", two[0].Name);

        Assert.True(reflection.TryGetBufferLayout(1, 0, out SlangBufferLayout? first));
        Assert.Equal(1, first.Members.Length);
        Assert.Equal("a", first.Members[0].Name);

        Assert.True(reflection.TryGetBufferLayout(2, 0, out SlangBufferLayout? second));
        Assert.Equal(1, second.Members.Length);
        Assert.Equal("b", second.Members[0].Name);

        AssertReflectionCoversSpirv(reflected.Program, reflection, _output);
    }

    /// <summary>
    /// Issue #180, with a descriptor-set record for space 1 that
    /// <b>already exists</b>: the constant buffer still lands in space 1, and
    /// nothing in either set borrows a neighbour's name.
    /// </summary>
    /// <remarks>
    /// This is the shape that proves the defect was not "Slang forgot to make a
    /// set record" — <c>gOther</c> at <c>(1,0)</c> was always correct while
    /// <c>gXform</c> at <c>(1,1)</c> was emitted into space 0's record and
    /// reported as <c>(0,1)</c>, overwriting the sampler's name.
    /// </remarks>
    [Fact]
    public void Reflection_ExplicitSpaceMixed_DoesNotBorrowANeighbourName()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile(
            "explicitSpaceMixed", ShaderFixtures.ReflectionExplicitSpaceMixed);
        SlangReflection reflection = reflected.Reflection;

        Assert.True(reflection.TryGetSet(1, out ReadOnlySpan<SlangDescriptorBinding> one));

        Assert.Equal(2, one.Length);
        Assert.Equal(0u, one[0].Slot);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_TEXTURE, one[0].Type);
        Assert.Equal("gOther", one[0].Name);
        Assert.Equal(1u, one[1].Slot);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_CONSTANT_BUFFER, one[1].Type);
        Assert.Equal("gXform", one[1].Name);

        Assert.True(reflection.TryGetSet(0, out ReadOnlySpan<SlangDescriptorBinding> zero));

        // Exactly two: no third entry, and no CONSTANT_BUFFER in set 0.
        Assert.Equal(2, zero.Length);
        Assert.Equal(0u, zero[0].Slot);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_TEXTURE, zero[0].Type);
        Assert.Equal("gAlbedo", zero[0].Name);
        Assert.Equal(1u, zero[1].Slot);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_SAMPLER, zero[1].Type);
        Assert.Equal("gSampler", zero[1].Name);

        AssertReflectionCoversSpirv(reflected.Program, reflection, _output);
    }

    /// <summary>
    /// Issue #180 one level deeper: a plain struct global placed at
    /// <c>[[vk::binding(0, 1)]]</c> puts <b>both</b> of its binding ranges in
    /// space 1.
    /// </summary>
    /// <remarks>
    /// This is the fixture that pins the span rule — one field owning two
    /// binding ranges. A correction that read only
    /// <c>getFieldBindingRangeOffset(f)</c> and applied it to that one range
    /// would repair <c>tex</c> and leave <c>cb</c> in set 0, which is what this
    /// test's set-0 assertion catches.
    /// </remarks>
    [Fact]
    public void Reflection_ExplicitSpaceStructGlobal_PlacesBothRangesInTheFieldsSpace()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile(
            "explicitSpaceStruct", ShaderFixtures.ReflectionExplicitSpaceStructGlobal);
        SlangReflection reflection = reflected.Reflection;

        Assert.True(reflection.TryGetSet(1, out ReadOnlySpan<SlangDescriptorBinding> one));

        Assert.Equal(2, one.Length);
        Assert.Equal(0u, one[0].Slot);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_TEXTURE, one[0].Type);
        Assert.Equal(1u, one[1].Slot);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_CONSTANT_BUFFER, one[1].Type);

        Assert.True(reflection.TryGetSet(0, out ReadOnlySpan<SlangDescriptorBinding> zero));

        Assert.Equal(1, zero.Length);
        Assert.Equal(0u, zero[0].Slot);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_TEXTURE, zero[0].Type);

        AssertReflectionCoversSpirv(reflected.Program, reflection, _output);
    }

    /// <summary>
    /// Issue #180's second shape: a module with <b>loose global uniform data</b>
    /// reports the implicit <c>globalParams</c> buffer <em>and</em> everything
    /// else, each in the set the emitted SPIR-V decorates.
    /// </summary>
    /// <remarks>
    /// <para>One <c>float4 gTint;</c> at file scope makes
    /// <c>spReflection_getGlobalParamsTypeLayout</c> hand back a
    /// <c>SLANG_TYPE_KIND_CONSTANT_BUFFER</c> wrapper instead of a struct. Before
    /// <c>UnwrapGlobalScope</c>, reflection reported <b>three bindings all at slot
    /// 0 of set 0</b>, all with an empty <c>Name</c>, and no set 1 — three
    /// <c>VkDescriptorSetLayoutBinding.binding = 0</c> entries into
    /// <c>vkCreateDescriptorSetLayout</c>
    /// (<c>VUID-VkDescriptorSetLayoutCreateInfo-binding-00279</c>). The
    /// <c>zero.Length == 2</c> assertion is what catches that pile-up; the set 1
    /// assertion is what proves the space correction resumes working once the
    /// scope is unwrapped, since the wrapper's kind short-circuits it.</para>
    /// <para><b>Stage attribution for a synthesized binding was measured, not
    /// assumed</b> (the spec's bounded uncertainty).
    /// <c>IMetadata::isParameterLocationUsed</c> had never been asked about a
    /// binding that no descriptor range backs. <b>Observed on <c>v2026.14.1</c> /
    /// win-x64: <c>Fragment</c> for <c>globalParams</c> at set 0 slot 1.</b> That
    /// is also this single-entry-point program's whole union, so it was measured
    /// a second time with the union fallback temporarily removed from
    /// <c>ApplyStages</c> — the value did not change, so the query genuinely
    /// answers <see langword="true"/> for a synthesized binding rather than
    /// falling back. The assertion is deliberately only "not <c>None</c>": a
    /// failed or
    /// <see langword="false"/> query falls back to the program union, which is
    /// always a legal <c>stageFlags</c>, so a wide answer is acceptable and only
    /// <c>ShaderStages.None</c> would be a bug.</para>
    /// </remarks>
    [Fact]
    public void Reflection_LooseGlobalUniforms_ReportTheImplicitBufferAndTheRest()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile(
            "looseSpace", ShaderFixtures.ReflectionLooseGlobalsWithExplicitSpace);
        SlangReflection reflection = reflected.Reflection;

        Assert.Equal(2, reflection.DescriptorSetCount);
        Assert.Equal(0u, reflection.SetIndex(0));
        Assert.Equal(1u, reflection.SetIndex(1));
        Assert.Equal(2u, reflection.SetLayoutSlotCount);

        Assert.True(reflection.TryGetSet(0, out ReadOnlySpan<SlangDescriptorBinding> zero));

        // Two, not three: the texture's slot is not also the implicit buffer's,
        // and the constant buffer is not in this set at all.
        Assert.Equal(2, zero.Length);
        Assert.Equal(0u, zero[0].Slot);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_TEXTURE, zero[0].Type);
        Assert.Equal("gAlbedo", zero[0].Name);
        Assert.Equal(1u, zero[1].Slot);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_CONSTANT_BUFFER, zero[1].Type);
        Assert.Equal("globalParams", zero[1].Name);

        Assert.True(reflection.TryGetSet(1, out ReadOnlySpan<SlangDescriptorBinding> one));

        Assert.Equal(1, one.Length);
        Assert.Equal(0u, one[0].Slot);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_CONSTANT_BUFFER, one[0].Type);
        Assert.Equal("gXform", one[0].Name);

        // The loose data is reachable: this layout is the only way a caller finds
        // out where to write gTint. gAlbedo and gXform are fields of the same
        // element scope and report GetSize(UNIFORM) = 0, so they are not members.
        Assert.True(reflection.TryGetBufferLayout(0, 1, out SlangBufferLayout? loose));
        Assert.Equal(1, loose.Members.Length);
        Assert.Equal("gTint", loose.Members[0].Name);
        Assert.Equal(0u, loose.Members[0].Offset);

        Assert.True(reflection.TryGetBufferLayout(1, 0, out SlangBufferLayout? xform));
        Assert.Equal("mvp", xform.Members[0].Name);

        // The texture's slot owns no buffer — the layout did not stay behind on
        // it the way the pre-#180 mis-key left Xform's members there.
        Assert.False(reflection.TryGetBufferLayout(0, 0, out _));

        // Stage attribution for a binding no descriptor range backs — measured.
        SlangReflection narrowed = reflected.Program.GetReflection(SlangStageAttribution.PerEntryPointUsage);
        ShaderStages stages = StagesOf(narrowed, set: 0, slot: 1);

        _output.WriteLine($"PerEntryPointUsage stages for globalParams at set 0 slot 1: {stages}");

        Assert.NotEqual(ShaderStages.None, stages);

        AssertReflectionCoversSpirv(reflected.Program, reflection, _output);
    }

    /// <summary>
    /// The degenerate control: a module whose global scope is nothing but loose
    /// uniform data reports one binding, and its <b>member layout</b> is what
    /// carries the assertion.
    /// </summary>
    /// <remarks>
    /// SPIR-V puts <c>globalParams</c> at <c>(0,0)</c> here, which is exactly
    /// where the un-unwrapped wrapper reported it by accident — so the set and
    /// slot prove nothing in this fixture and the member offsets prove
    /// everything. That asymmetry is deliberate and load-bearing: hard-coding the
    /// synthesized slot to <c>0</c> leaves this test green while turning the
    /// explicit-space one red, which is what shows the sibling tests assert the
    /// offset Slang reports rather than a constant.
    /// </remarks>
    [Fact]
    public void Reflection_LooseGlobalUniformsOnly_ReportOneBufferWithBothMembers()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile(
            "looseOnly", ShaderFixtures.ReflectionLooseGlobalsOnly);
        SlangReflection reflection = reflected.Reflection;

        Assert.Equal(1, reflection.DescriptorSetCount);
        Assert.Equal(0u, reflection.SetIndex(0));

        ReadOnlySpan<SlangDescriptorBinding> bindings = reflection.Bindings(0);

        Assert.Equal(1, bindings.Length);
        Assert.Equal(0u, bindings[0].Slot);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_CONSTANT_BUFFER, bindings[0].Type);
        Assert.Equal("globalParams", bindings[0].Name);

        Assert.True(reflection.TryGetBufferLayout(0, 0, out SlangBufferLayout? layout));

        _output.WriteLine($"globalParams layout: size {layout.Size}, {layout.Members.Length} member(s)");

        // 32, not 20: Slang rounds the implicit buffer's UNIFORM size up.
        Assert.Equal(32u, layout.Size);
        Assert.Equal(2, layout.Members.Length);
        Assert.Equal("gTint", layout.Members[0].Name);
        Assert.Equal(0u, layout.Members[0].Offset);
        Assert.Equal("gScale", layout.Members[1].Name);
        Assert.Equal(16u, layout.Members[1].Offset);

        AssertReflectionCoversSpirv(reflected.Program, reflection, _output);
    }

    /// <summary>
    /// The wrapper defect is not about <c>[[vk::binding]]</c>: a module with no
    /// explicit binding anywhere is affected too.
    /// </summary>
    /// <remarks>
    /// <para><b>Measured on <c>v2026.14.1</c> / win-x64, and it contradicts what
    /// the plan predicted for this shape.</b> With nothing explicitly bound, the
    /// implicit buffer takes slot <b>0</b> and pushes the two resources to 1 and
    /// 2 — <c>globalParams (0,0)</c>, <c>gAlbedo (0,1)</c>, <c>gSampler (0,2)</c>
    /// — not <c>globalParams (0,2)</c>. Re-measured with <c>float4 gTint;</c>
    /// moved below both resources: unchanged, so it is not declaration order. The
    /// implicit buffer is therefore only pushed off slot 0 when something else
    /// claims it, which is what
    /// <see cref="ShaderFixtures.ReflectionLooseGlobalsWithExplicitSpace"/>
    /// does.</para>
    /// <para>What this fixture pins is consequently the <em>collision</em>, not
    /// the slot: before the unwrap all three bindings were reported at slot 0 of
    /// set 0 with empty names, in a module containing no <c>[[vk::binding]]</c>
    /// at all. The <c>bindings.Length == 3</c> plus ascending-slot assertions are
    /// what catch that.</para>
    /// </remarks>
    [Fact]
    public void Reflection_LooseGlobalUniforms_DoNotNeedAnExplicitBinding()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile(
            "loosePlain", ShaderFixtures.ReflectionLooseGlobalsNoExplicitBinding);
        SlangReflection reflection = reflected.Reflection;

        Assert.Equal(1, reflection.DescriptorSetCount);
        Assert.Equal(0u, reflection.SetIndex(0));

        Assert.True(reflection.TryGetSet(0, out ReadOnlySpan<SlangDescriptorBinding> zero));

        Assert.Equal(3, zero.Length);
        Assert.Equal(0u, zero[0].Slot);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_CONSTANT_BUFFER, zero[0].Type);
        Assert.Equal("globalParams", zero[0].Name);
        Assert.Equal(1u, zero[1].Slot);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_TEXTURE, zero[1].Type);
        Assert.Equal("gAlbedo", zero[1].Name);
        Assert.Equal(2u, zero[2].Slot);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_SAMPLER, zero[2].Type);
        Assert.Equal("gSampler", zero[2].Name);

        Assert.True(reflection.TryGetBufferLayout(0, 0, out SlangBufferLayout? loose));
        Assert.Equal(1, loose.Members.Length);
        Assert.Equal("gTint", loose.Members[0].Name);

        AssertReflectionCoversSpirv(reflected.Program, reflection, _output);
    }

    /// <summary>
    /// Loose global uniform data <b>and</b> a <c>ParameterBlock</c>: two implicit
    /// buffers, in two sets, placed by two different rules, neither displacing
    /// the other.
    /// </summary>
    /// <remarks>
    /// The one combined shape worth its own fixture. Unwrapping changes what the
    /// top-level walk is handed, while a block's set number comes from an offset
    /// accumulated separately off the sub-object range's variable layout — so if
    /// the unwrap disturbed the element's own index space, or if the synthesized
    /// global buffer were placed by the block rule (slot 0 of its space, listed
    /// ranges shifted up), this is where it would show. Measured on
    /// <c>v2026.14.1</c> / win-x64: <c>globalParams (0,0)</c>,
    /// <c>gAlbedo (0,1)</c>, <c>gXform (1,0)</c>.
    /// </remarks>
    [Fact]
    public void Reflection_LooseGlobalsWithParameterBlock_PlaceBothImplicitBuffers()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile(
            "looseBlock", ShaderFixtures.ReflectionLooseGlobalsWithParameterBlock);
        SlangReflection reflection = reflected.Reflection;

        Assert.Equal(2, reflection.DescriptorSetCount);
        Assert.Equal(0u, reflection.SetIndex(0));
        Assert.Equal(1u, reflection.SetIndex(1));

        Assert.True(reflection.TryGetSet(0, out ReadOnlySpan<SlangDescriptorBinding> zero));

        Assert.Equal(2, zero.Length);
        Assert.Equal(0u, zero[0].Slot);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_CONSTANT_BUFFER, zero[0].Type);
        Assert.Equal("globalParams", zero[0].Name);
        Assert.Equal(1u, zero[1].Slot);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_TEXTURE, zero[1].Type);
        Assert.Equal("gAlbedo", zero[1].Name);

        // The block still lands in set 1, at slot 0 of its own space: the
        // sub-object range offset it accumulates from is untouched by the unwrap.
        Assert.True(reflection.TryGetSet(1, out ReadOnlySpan<SlangDescriptorBinding> one));

        Assert.Equal(1, one.Length);
        Assert.Equal(0u, one[0].Slot);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_CONSTANT_BUFFER, one[0].Type);
        Assert.Equal("gXform", one[0].Name);

        // Two distinct buffer layouts, one per implicit buffer.
        Assert.True(reflection.TryGetBufferLayout(0, 0, out SlangBufferLayout? loose));
        Assert.Equal(1, loose.Members.Length);
        Assert.Equal("gTint", loose.Members[0].Name);

        Assert.True(reflection.TryGetBufferLayout(1, 0, out SlangBufferLayout? block));
        Assert.Equal("mvp", block.Members[0].Name);

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
    /// The call that used to kill the test host, made on purpose: the
    /// <c>EXISTENTIAL_VALUE</c> range that a <c>ParameterBlock&lt;ISurface&gt;</c>
    /// element scope reports comes back <c>unknown</c> instead of taking the
    /// process down (#181).
    /// </summary>
    /// <remarks>
    /// <para><b>The second assertion is the test.</b> The first one passes the
    /// range's real binding type, which <see cref="SlangReflection.ImageFormatOf"/>
    /// rejects on kind before it reaches the guard — true, worth stating, and
    /// vacuous as a test of the guard. So the second one passes
    /// <c>SLANG_BINDING_TYPE_TEXTURE</c> for the same range, which is a
    /// deliberate lie about its kind: it is exactly what a future widening of
    /// the kind predicate would do, and it drives the call through to the null
    /// check that stands behind it. Passing means widening is survivable;
    /// before the guard existed this argument was fatal.</para>
    /// <para><b>A regression here does not fail — it vanishes.</b> If the null
    /// check is removed or reordered after the native call, this test takes the
    /// whole run down with <c>0xC0000005</c> rather than reporting red. That is
    /// the nature of the defect (see the class remarks on measuring against
    /// SPIR-V: the same "no result code to check" problem), and it is why the
    /// precondition below is asserted rather than assumed — the day upstream
    /// populates <c>leafVariable</c> for this range, the guard stops being
    /// exercised and this test would quietly prove nothing.</para>
    /// </remarks>
    [Fact]
    public unsafe void ImageFormat_ExistentialRange_IsUnknownRatherThanFatal()
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);
        using SlangModule module = session.LoadModuleFromSource("surfaceFormat", "surfaceFormat.slang", ShaderFixtures.InterfaceSurfaceModule);
        using SlangEntryPoint fragment = module.DefinedEntryPoint(0);

        using SlangProgram program = session.CreateProgram()
            .Add(module)
            .Add(fragment)
            .AddTypeConformance("Glossy", "ISurface")
            .Link();

        SlangProgramLayout* layout = SlangReflection.GetLayout(program.LinkedComponent);

        // Global range 0 is the ParameterBlock itself; the existential value
        // buffer lives one level down, in the block's element scope.
        SlangReflectionTypeLayout* element = SlangApi.spReflectionTypeLayout_GetElementTypeLayout(
            SlangApi.spReflectionTypeLayout_getBindingRangeLeafTypeLayout(
                SlangApi.spReflection_getGlobalParamsTypeLayout(layout), 0));

        Assert.Equal(1, SlangApi.spReflectionTypeLayout_getBindingRangeCount(element));

        SlangBindingType type = SlangApi.spReflectionTypeLayout_getBindingRangeType(element, 0);
        Assert.Equal(SlangBindingType.SLANG_BINDING_TYPE_EXISTENTIAL_VALUE, type);

        // The precondition the guard keys on. Null here is what upstream
        // dereferences; see ImageFormatOf's remarks.
        Assert.True(
            SlangApi.spReflectionTypeLayout_getBindingRangeLeafVariable(element, 0) == null,
            "leafVariable is no longer null for an EXISTENTIAL_VALUE range — the guard below is untested, and #181 needs re-measuring.");

        Assert.Equal(
            SlangImageFormat.SLANG_IMAGE_FORMAT_unknown,
            SlangReflection.ImageFormatOf(element, 0, type));

        // Reached only because of the null check: the kind test admits this,
        // and the native call underneath it is the one that crashes.
        Assert.Equal(
            SlangImageFormat.SLANG_IMAGE_FORMAT_unknown,
            SlangReflection.ImageFormatOf(element, 0, SlangBindingType.SLANG_BINDING_TYPE_TEXTURE));
    }

    /// <summary>
    /// The SPIR-V oracle for the binding-range join: every name reflection
    /// reports for a <c>(set, slot)</c> is the <c>OpName</c> the emitted module
    /// gives the variable it decorates at that very <c>(set, binding)</c>.
    /// </summary>
    /// <remarks>
    /// <para>This is the cheapest possible test of spec E8 route 1. The join
    /// runs <c>getBindingRangeDescriptorSetIndex</c> /
    /// <c>getBindingRangeFirstDescriptorRangeIndex</c> through the same space
    /// and index-offset calls the verified walk uses; if any of that produced
    /// keys off by one — the way
    /// <c>getSubObjectRangeSpaceOffset</c> silently returns <c>0</c> — the names
    /// would land on the wrong bindings and this fails with both names in the
    /// message.</para>
    /// <para><b>The sparse row is the one that would catch a regression back
    /// onto the loop index.</b> In the other two fixtures the descriptor-set
    /// loop index equals the Vulkan set number, so a join that read the wrong
    /// one would still land on the right binding.
    /// <c>ReflectionSparseSets</c> puts <c>gSamp</c> in space 2 at loop index 1,
    /// and a wrong read names a binding in a set that does not exist.</para>
    /// <para>Only fixtures whose bindings are declared at global scope are
    /// listed. A resource declared <em>inside</em> a <c>ParameterBlock</c> — or
    /// inside the plain struct global of
    /// <c>ReflectionExplicitSpaceStructGlobal</c>, which is why that one is not
    /// a row here — is named <c>maps</c> / <c>cb</c> by reflection and
    /// <c>gWith.maps</c> / <c>gBundle.cb</c> by SPIR-V: a qualification
    /// difference, not a join failure, and asserting on it would pin Slang's
    /// naming convention rather than the join.</para>
    /// <para><b>The three explicit-space rows are what catch a name
    /// transplant.</b> Before issue #180 the mis-keyed constant buffer
    /// overwrote whatever legitimately owned its key, so <c>(0,0)</c> was named
    /// <c>gXform</c> in the first row and <c>(0,1)</c> was named
    /// <c>gSampler</c> in the second.</para>
    /// <para><b>The four loose-globals rows are what pin the implicit global
    /// buffer's name.</b> Slang supplies none for it —
    /// <c>spReflectionVariableLayout_GetVariable</c> on the global params var
    /// layout is <see langword="null"/> — so <c>SlangReflection</c> chooses
    /// <c>globalParams</c>, and this theory is what says that choice is the
    /// <c>OpName</c> the emitted module actually uses. Pinning it is a
    /// rename-detector, not a correctness assertion: unlike a set or a slot, this
    /// name is cosmetic and nothing a driver sees depends on it. These rows also
    /// catch the empty names every binding in these modules had before the
    /// wrapper was unwrapped.</para>
    /// </remarks>
    [Theory]
    [InlineData("namesGlobals", ShaderFixtures.ReflectionGlobals)]
    [InlineData("namesTwoBlocks", ShaderFixtures.ReflectionTwoBlocks)]
    [InlineData("namesSparse", ShaderFixtures.ReflectionSparseSets)]
    [InlineData("namesExplicitSpaceCb", ShaderFixtures.ReflectionExplicitSpaceConstantBuffer)]
    [InlineData("namesExplicitSpaceMixed", ShaderFixtures.ReflectionExplicitSpaceMixed)]
    [InlineData("namesExplicitSpaceTwoCbs", ShaderFixtures.ReflectionExplicitSpaceTwoConstantBuffers)]
    [InlineData("namesLooseSpace", ShaderFixtures.ReflectionLooseGlobalsWithExplicitSpace)]
    [InlineData("namesLooseOnly", ShaderFixtures.ReflectionLooseGlobalsOnly)]
    [InlineData("namesLoosePlain", ShaderFixtures.ReflectionLooseGlobalsNoExplicitBinding)]
    [InlineData("namesLooseBlock", ShaderFixtures.ReflectionLooseGlobalsWithParameterBlock)]
    public void Reflection_BindingNames_MatchTheSpirvVariableNames(string moduleName, string source)
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile(moduleName, source);
        SlangReflection reflection = reflected.Reflection;
        int checkedBindings = 0;

        for (int e = 0; e < reflected.Program.EntryPointCount; e++)
        {
            foreach ((uint set, uint binding, string name) in
                SpirvDecorations.ReadDescriptorBindings(reflected.Program.Spirv(e)))
            {
                Assert.True(reflection.TryGetSet(set, out ReadOnlySpan<SlangDescriptorBinding> bindings));

                foreach (SlangDescriptorBinding candidate in bindings)
                {
                    if (candidate.Slot != binding)
                    {
                        continue;
                    }

                    _output.WriteLine($"set={set} binding={binding}: SPIR-V '{name}' vs reflection '{candidate.Name}'");
                    Assert.Equal(name, candidate.Name);
                    checkedBindings++;
                }
            }
        }

        Assert.True(checkedBindings > 0, "The fixture produced no bindings to compare.");
    }

    /// <summary>
    /// The implicit uniform buffer a <c>ParameterBlock</c> owns at binding 0 has
    /// no descriptor range and therefore no name of its own; it takes the
    /// block's.
    /// </summary>
    [Fact]
    public void Reflection_ParameterBlockUniformBuffer_TakesTheBlockName()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile("blockName", ShaderFixtures.ReflectionBlockOrdinaryData);
        SlangReflection reflection = reflected.Reflection;

        Assert.True(reflection.TryGetSet(0, out ReadOnlySpan<SlangDescriptorBinding> withData));
        Assert.Equal("gWith", withData[0].Name);

        // The declared resources inside the block keep their own field names.
        Assert.Equal("maps", withData[1].Name);
        Assert.Equal("samp", withData[2].Name);
    }

    /// <summary>
    /// <c>isBindingRangeSpecializable</c> discriminates: the interface-typed
    /// block reports <see langword="true"/>, everything concrete reports
    /// <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// The measurement gate for shipping <c>IsSpecializable</c> at all — a field
    /// that is constant reads as information and is not. Measured on
    /// <c>v2026.14.1</c> / win-x64: <c>1</c> for <c>gSurface</c>'s binding range
    /// and <c>0</c> for every binding of every other fixture.
    /// </remarks>
    [Fact]
    public void Reflection_InterfaceTypedBlock_IsSpecializable()
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);
        using SlangModule module = session.LoadModuleFromSource(
            "specializable", "specializable.slang", ShaderFixtures.InterfaceSurfaceModule);
        using SlangEntryPoint fragment = module.DefinedEntryPoint(0);
        using SlangProgram program = session.CreateProgram()
            .Add(module)
            .Add(fragment)
            .AddTypeConformance("Glossy", "ISurface")
            .Link();

        // The one populated set, whatever its number: this program's only
        // binding is the block's existential value buffer.
        ReadOnlySpan<SlangDescriptorBinding> existential = program.Reflection.Bindings(0);

        _output.WriteLine($"set {program.Reflection.SetIndex(0)} slot {existential[0].Slot} '{existential[0].Name}'");

        Assert.Equal(1, existential.Length);
        Assert.True(existential[0].IsSpecializable);
        Assert.Equal("gSurface", existential[0].Name);

        using ReflectedProgram concrete = ReflectedProgram.Compile("notSpecializable", ShaderFixtures.ReflectionTwoBlocks);

        for (int i = 0; i < concrete.Reflection.DescriptorSetCount; i++)
        {
            foreach (SlangDescriptorBinding binding in concrete.Reflection.Bindings(i))
            {
                Assert.False(binding.IsSpecializable, $"'{binding.Name}' should not be specializable.");
            }
        }
    }

    [Fact]
    public void Reflection_StorageImageFormat_IsReported()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile(
            "imageFormat", ShaderFixtures.ReflectionComputeStorageImage);

        Assert.True(reflected.Reflection.TryGetSet(0, out ReadOnlySpan<SlangDescriptorBinding> bindings));
        Assert.Equal(2, bindings.Length);

        Assert.Equal("gAnnotated", bindings[0].Name);
        Assert.Equal(SlangImageFormat.SLANG_IMAGE_FORMAT_rgba8, bindings[0].ImageFormat);

        Assert.Equal("gPlain", bindings[1].Name);
        Assert.Equal(SlangImageFormat.SLANG_IMAGE_FORMAT_unknown, bindings[1].ImageFormat);
    }

    [Fact]
    public void Reflection_PushConstantRange_HasBlockName()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile("pushName", ShaderFixtures.ReflectionGlobals);

        Assert.Equal("gPush", reflected.Reflection.PushConstantRanges[0].Name);
    }

    /// <summary>
    /// A vertex-buffer binder matches on the semantic, not on the field name —
    /// and Slang splits <c>TEXCOORD0</c> into a name and an index.
    /// </summary>
    [Fact]
    public void Reflection_VertexAttributes_CarrySemantics()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile(
            "semantics", ShaderFixtures.ReflectionSystemValueInputs);

        ReadOnlySpan<SlangVertexAttributeDescription> attributes = reflected.Reflection.VertexAttributes(0);

        // The two system values report parameter category NONE and are not here.
        Assert.Equal(3, attributes.Length);

        Assert.Equal("POSITION", attributes[0].SemanticName);
        Assert.Equal("TEXCOORD", attributes[1].SemanticName);
        Assert.Equal("TANGENT", attributes[2].SemanticName);

        foreach (SlangVertexAttributeDescription attribute in attributes)
        {
            Assert.Equal(0u, attribute.SemanticIndex);
        }

        // The field names are a different thing, and are still reported.
        Assert.Equal(["pos", "uv", "tangent"], new[] { attributes[0].Name, attributes[1].Name, attributes[2].Name });
    }

    /// <summary>
    /// A compute dispatch cannot compute its group count without this.
    /// </summary>
    /// <remarks>
    /// Measured on <c>v2026.14.1</c> / win-x64: every non-compute stage reports
    /// <c>1, 1, 1</c> rather than zeroes, which is asserted here rather than
    /// left to a comment — a future Slang returning <c>0, 0, 0</c> would make
    /// a caller's <c>ceil(n / groupSize)</c> divide by zero.
    /// </remarks>
    [Fact]
    public void Reflection_ComputeEntryPoint_ReportsThreadGroupSize()
    {
        using ReflectedProgram compute = ReflectedProgram.Compile(
            "threadGroup", ShaderFixtures.ReflectionComputeStorageImage);

        SlangEntryPointInfo computeMain = compute.Reflection.EntryPoint(0);

        Assert.Equal(ShaderStages.Compute, computeMain.Stage);
        Assert.Equal(8u, computeMain.ThreadGroupSizeX);
        Assert.Equal(4u, computeMain.ThreadGroupSizeY);
        Assert.Equal(1u, computeMain.ThreadGroupSizeZ);

        using ReflectedProgram graphics = ReflectedProgram.Compile("threadGroupVs", ShaderFixtures.ReflectionGlobals);

        SlangEntryPointInfo vertexMain = graphics.Reflection.EntryPoint(0);

        Assert.Equal(ShaderStages.Vertex, vertexMain.Stage);
        Assert.Equal((1u, 1u, 1u), (vertexMain.ThreadGroupSizeX, vertexMain.ThreadGroupSizeY, vertexMain.ThreadGroupSizeZ));
    }

    /// <summary>
    /// <b>Reflection's entry-point name is not the name in the emitted
    /// SPIR-V.</b> Measured, and asserted so it cannot drift silently.
    /// </summary>
    /// <remarks>
    /// <para>Slang names every emitted entry point <c>main</c> regardless of the
    /// stage or the Slang function's name, so a caller passing
    /// <c>EntryPoint(i).Name</c> to
    /// <c>VkPipelineShaderStageCreateInfo.pName</c> gets
    /// <c>VUID-VkPipelineShaderStageCreateInfo-pName-00707</c> — a validation
    /// error with no other symptom, because the handle still comes back
    /// non-null.</para>
    /// <para><c>spReflectionEntryPoint_getNameOverride</c> does not close the
    /// gap: measured on <c>v2026.14.1</c> / win-x64 it returned exactly
    /// <c>getName</c>'s string for every fixture and every stage, which is why
    /// <c>SlangEntryPointInfo</c> carries no <c>NameOverride</c> member. If a
    /// later Slang starts emitting the Slang name, this test fails and the
    /// member becomes worth adding.</para>
    /// </remarks>
    [Fact]
    public void Reflection_EntryPointName_IsNotTheNameInTheEmittedSpirv()
    {
        using ReflectedProgram reflected = ReflectedProgram.Compile("opEntryPoint", ShaderFixtures.ReflectionGlobals);

        for (int e = 0; e < reflected.Program.EntryPointCount; e++)
        {
            List<string> emitted = SpirvDecorations.ReadEntryPointNames(reflected.Program.Spirv(e));
            string reported = reflected.Reflection.EntryPoint(e).Name;

            _output.WriteLine($"reflection '{reported}' vs OpEntryPoint '{string.Join(", ", emitted)}'");

            Assert.Equal(["main"], emitted);
            Assert.NotEqual("main", reported);
        }

        Assert.Equal("vertexMain", reflected.Reflection.EntryPoint(0).Name);
        Assert.Equal("fragmentMain", reflected.Reflection.EntryPoint(1).Name);
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

    /// <summary>
    /// Issue #191's end-to-end acceptance test: a program declaring sets 0 and 2
    /// becomes a complete <c>PipelineLayout</c>, with set 1 — the hole — filled
    /// by a descriptor set layout that has zero bindings.
    /// </summary>
    /// <remarks>
    /// No <c>shaderDrawParameters</c> here, unlike
    /// <see cref="Reflection_BuildsAWorkingPipelineLayout"/>: this fixture is
    /// fragment-only and declares no <c>SV_VertexID</c>, so the module never
    /// emits the <c>DrawParameters</c> capability.
    /// </remarks>
    [Fact]
    public void Reflection_SparseSets_BuildsAPipelineLayoutWithAHole()
    {
        TestGate.RequireDriver();

        using ReflectedProgram reflected = ReflectedProgram.Compile(
            "sparseSetsLayout", ShaderFixtures.ReflectionSparseSets);

        SlangReflection reflection = reflected.Reflection;

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
        });

        var layouts = new DescriptorSetLayout[(int)reflection.SetLayoutSlotCount];

        try
        {
            for (uint set = 0; set < reflection.SetLayoutSlotCount; set++)
            {
                layouts[set] = reflection.TryGetSet(set, out ReadOnlySpan<SlangDescriptorBinding> bindings)
                    ? device.CreateDescriptorSetLayout(
                        new DescriptorSetLayoutDescription { Bindings = bindings.MapBindings() })
                    : device.CreateDescriptorSetLayout(default);   // the hole
            }

            // The hole is a real layout, not VK_NULL_HANDLE.
            Assert.False(layouts[1].IsNull);

            using PipelineLayout pipelineLayout = device.CreatePipelineLayout(new PipelineLayoutDescription
            {
                SetLayouts = layouts,
                PushConstantRanges = reflection.PushConstantRanges.MapPushConstantRanges(),
            });

            Assert.False(pipelineLayout.IsNull);

            using ShaderModule fragment = device.CreateShaderModule(reflected.Program.Spirv(0));

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
                $"The layers rejected a pipeline layout with a zero-binding set:{Environment.NewLine}" +
                string.Join(Environment.NewLine, errors));
        }
    }

    /// <summary>
    /// The layout issue #183 produces — binding numbers 1 and 2, with a hole
    /// where the zero-length array reserved 0 — is one a driver and the
    /// validation layer accept, alongside the SPIR-V it was derived from.
    /// </summary>
    /// <remarks>
    /// <para><b>This test is corroboration, not the guard.</b> Measured: revert
    /// this whole change — <c>MapBindings</c> maps every binding and
    /// <c>MapBinding</c> refuses nothing — and it still passes, because the
    /// resulting layout carries a surplus <c>descriptorCount = 1</c> at binding
    /// 0 (<c>Ahjo.Vulkan</c> normalizes the mapper's <c>0</c>, issue #119) and
    /// that is a perfectly valid descriptor set layout. <b>That is exactly why
    /// the defect was invisible</b>, and it is why the discriminating tests are
    /// the ones above, none of which needs a device.</para>
    /// <para>It does go red under a <em>partial</em> revert, but never on its
    /// own assertions: with the omission removed and
    /// <c>MapBinding</c>'s refusal left in place, <c>MapBindings</c> throws
    /// before a device is touched. Do not read that as this test guarding the
    /// behaviour.</para>
    /// <para>What it does prove is the half reasoning cannot: that a
    /// <b>non-contiguous</b> binding list — 1 and 2, with a hole where the
    /// zero-length array reserved 0 — is accepted by a real driver and by the
    /// validation layer, which is what makes omission a legal answer at all.
    /// <c>VUID-VkDescriptorSetLayoutCreateInfo-binding-00279</c> requires
    /// binding numbers to be distinct, not contiguous.</para>
    /// </remarks>
    [Fact]
    public void MapBindings_ZeroLengthArray_BuildsALayoutValidationAccepts()
    {
        TestGate.RequireDriver();
        TestGate.RequireValidationLayer();

        using ReflectedProgram reflected = ReflectedProgram.Compile(
            "zeroArrayDevice", ShaderFixtures.ReflectionZeroLengthArray);

        Assert.True(reflected.Reflection.TryGetSet(0, out ReadOnlySpan<SlangDescriptorBinding> bindings));

        // Deliberately no assertion on the mapped array's shape — that is
        // MapBindings_ZeroCountBinding_IsOmittedFromTheLayout's job, and making
        // it here would turn this test into a second copy of that one that
        // needs a GPU to run.
        DescriptorBinding[] mapped = bindings.MapBindings();

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

        using var instance = Instance.Create(new InstanceDescription
        {
            EnableValidation = true,
            DebugCallback = sink,
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
        });

        using DescriptorSetLayout layout = device.CreateDescriptorSetLayout(
            new DescriptorSetLayoutDescription { Bindings = mapped });

        using PipelineLayout pipelineLayout = device.CreatePipelineLayout(new PipelineLayoutDescription
        {
            SetLayouts = [layout],
            PushConstantRanges = reflected.Reflection.PushConstantRanges.MapPushConstantRanges(),
        });

        Assert.False(pipelineLayout.IsNull);

        using ShaderModule fragment = device.CreateShaderModule(reflected.Program.Spirv(0));

        Assert.False(fragment.IsNull);

        Assert.True(
            Volatile.Read(ref errorCount) == 0,
            $"The layers rejected the layout derived from a zero-length array:{Environment.NewLine}" +
            string.Join(Environment.NewLine, errors));
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
