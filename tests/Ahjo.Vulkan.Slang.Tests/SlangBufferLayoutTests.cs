using Ahjo.Vulkan.Slang.Native;

using Xunit;

namespace Ahjo.Vulkan.Slang.Tests;

/// <summary>
/// What is inside a buffer: <c>SlangReflection.TryGetBufferLayout</c>,
/// <c>TryGetPushConstantLayout</c> and <c>ToJson</c>.
/// </summary>
/// <remarks>
/// <para><b>Issue #175 exists because of a test suite that could not see this.</b>
/// Widening <c>float2 UvScale</c> to <c>float4</c> inside a material uniform
/// buffer changes that buffer's size and every member offset after it, and no
/// assertion in a 97-test reflection suite moved. Three layers answer that
/// here, and they are deliberately not the same layer three times:</para>
/// <list type="number">
/// <item><description><b>The SPIR-V oracle.</b>
/// <c>BufferLayout_MaterialBlock_OffsetsMatchTheEmittedSpirv</c> compares every
/// offset against <c>OpMemberDecorate … Offset</c> in the module Slang emitted.
/// Reading the offsets back out of reflection would prove nothing, and picking
/// the wrong parameter category produces offsets that look entirely plausible.</description></item>
/// <item><description><b>Goldens.</b> Recorded from a green run, with the Slang
/// version named, so a change in what Slang computes is visible as a diff
/// rather than as a silently different number.</description></item>
/// <item><description><b>The widened twin.</b>
/// <c>BufferLayout_WideningAMember_ChangesSizeAndSubsequentOffsets</c> compiles
/// two fixtures that differ in exactly the mutation the issue describes and
/// asserts the results <em>differ</em>. It is the one test here that cannot be
/// made green by editing a constant.</description></item>
/// </list>
/// </remarks>
public sealed class SlangBufferLayoutTests
{
    private readonly ITestOutputHelper _output;

    public SlangBufferLayoutTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// The load-bearing one: reflected member offsets against
    /// <c>OpMemberDecorate … Offset</c> in the emitted module.
    /// </summary>
    /// <remarks>
    /// SPIR-V states member offsets <em>relative to the struct that declares
    /// them</em>; <c>SlangBufferMember.Offset</c> is relative to the buffer. The
    /// comparison subtracts the parent's offset, which is also an assertion that
    /// <c>ParentIndex</c> points where it says it does.
    /// </remarks>
    [Fact]
    public void BufferLayout_MaterialBlock_OffsetsMatchTheEmittedSpirv()
    {
        using var compiled = Compiled.Compile("offsets", ShaderFixtures.ReflectionMaterialBlock);

        SlangBufferLayout layout = compiled.MaterialLayout();
        var spirv = new Dictionary<string, uint>(StringComparer.Ordinal);

        foreach ((string structName, string memberName, uint index, uint offset) in
            SpirvDecorations.ReadMemberOffsets(compiled.Program.Spirv(0)))
        {
            _output.WriteLine($"SPIR-V {structName}.{memberName} (member {index}) Offset={offset}");

            Assert.False(
                string.IsNullOrEmpty(memberName),
                "Slang emitted no OpMemberName for this module, so members cannot be matched by name. "
                + "Switch this assertion to the ordered offset sequence per struct (SpirvDecorations remarks).");

            // Unique across this fixture's two structs, which is what lets the
            // comparison key on the name alone.
            Assert.False(spirv.ContainsKey(memberName), $"'{memberName}' is decorated in two structs.");

            spirv[memberName] = offset;
        }

        Assert.NotEmpty(spirv);

        int compared = 0;

        for (int i = 0; i < layout.Members.Length; i++)
        {
            SlangBufferMember member = layout.Members[i];
            uint parentOffset = member.ParentIndex < 0 ? 0 : layout.Members[member.ParentIndex].Offset;
            string shortName = member.Name[(member.Name.LastIndexOf('.') + 1)..];

            Assert.True(spirv.TryGetValue(shortName, out uint spirvOffset), $"'{member.Name}' is not decorated at all.");
            Assert.Equal(spirvOffset, member.Offset - parentOffset);

            compared++;
        }

        Assert.Equal(spirv.Count, compared);
    }

    /// <summary>
    /// Golden values, recorded from the first green run on Slang
    /// <c>v2026.14.1</c> / win-x64 rather than computed by hand.
    /// </summary>
    [Fact]
    public void BufferLayout_MaterialBlock_HasGoldenSizeAndOffsets()
    {
        using var compiled = Compiled.Compile("goldens", ShaderFixtures.ReflectionMaterialBlock);

        SlangBufferLayout layout = compiled.MaterialLayout();

        foreach (SlangBufferMember member in layout.Members)
        {
            _output.WriteLine(
                $"{member.Name} parent={member.ParentIndex} offset={member.Offset} size={member.Size} "
                + $"stride={member.Stride} align={member.Alignment} kind={member.Kind}");
        }

        Assert.Equal("gMaterial", layout.Name);
        Assert.Equal(128u, layout.Size);
        Assert.Equal(8, layout.Members.Length);

        // Every Params.* offset here is 16 higher than the struct-relative one
        // SPIR-V decorates, which is what makes the offset oracle above able to
        // fail — see ReflectionMaterialBlock's remarks.
        AssertMember(layout, "Tint", offset: 0, size: 16, SlangTypeKind.SLANG_TYPE_KIND_VECTOR);
        AssertMember(layout, "Params", offset: 16, size: 48, SlangTypeKind.SLANG_TYPE_KIND_STRUCT);
        AssertMember(layout, "Params.BaseColor", offset: 16, size: 12, SlangTypeKind.SLANG_TYPE_KIND_VECTOR);
        AssertMember(layout, "Params.Roughness", offset: 28, size: 4, SlangTypeKind.SLANG_TYPE_KIND_SCALAR);
        AssertMember(layout, "Params.Metallic", offset: 32, size: 4, SlangTypeKind.SLANG_TYPE_KIND_SCALAR);
        AssertMember(layout, "Params.UvScale", offset: 40, size: 8, SlangTypeKind.SLANG_TYPE_KIND_VECTOR);
        AssertMember(layout, "Params.Flags", offset: 48, size: 4, SlangTypeKind.SLANG_TYPE_KIND_SCALAR);
        AssertMember(layout, "Transform", offset: 64, size: 64, SlangTypeKind.SLANG_TYPE_KIND_MATRIX);
    }

    /// <summary>
    /// <b>The fault-sweep survivor, turned into a test.</b> Two shaders that
    /// differ only in <c>float2 UvScale</c> versus <c>float4 UvScale</c> must
    /// not produce the same layout.
    /// </summary>
    /// <remarks>
    /// This is the one test in the file that cannot be made green by editing a
    /// constant: it compares two compilations against each other, so it fails
    /// whenever the API stops being able to see the mutation — which is exactly
    /// the state issue #175 was filed about.
    /// </remarks>
    [Fact]
    public void BufferLayout_WideningAMember_ChangesSizeAndSubsequentOffsets()
    {
        using var narrow = Compiled.Compile("narrow", ShaderFixtures.ReflectionMaterialBlock);
        using var wide = Compiled.Compile("wide", ShaderFixtures.ReflectionMaterialBlockWidened);

        SlangBufferLayout narrowLayout = narrow.MaterialLayout();
        SlangBufferLayout wideLayout = wide.MaterialLayout();

        Assert.True(narrowLayout.TryGetMember("Params.UvScale", out SlangBufferMember narrowUvScale));
        Assert.True(wideLayout.TryGetMember("Params.UvScale", out SlangBufferMember wideUvScale));
        Assert.True(narrowLayout.TryGetMember("Params.Flags", out SlangBufferMember narrowFlags));
        Assert.True(wideLayout.TryGetMember("Params.Flags", out SlangBufferMember wideFlags));

        _output.WriteLine($"narrow: size={narrowLayout.Size} UvScale={narrowUvScale.Size} Flags@{narrowFlags.Offset}");
        _output.WriteLine($"wide:   size={wideLayout.Size} UvScale={wideUvScale.Size} Flags@{wideFlags.Offset}");

        Assert.NotEqual(narrowLayout.Size, wideLayout.Size);
        Assert.NotEqual(narrowUvScale.Size, wideUvScale.Size);
        Assert.NotEqual(narrowFlags.Offset, wideFlags.Offset);
    }

    /// <summary>
    /// Spec D4(c): a buffer layout exists exactly where a buffer-shaped binding
    /// was reported, and a standalone <c>ConstantBuffer&lt;T&gt;</c> resolves to
    /// its own members.
    /// </summary>
    [Fact]
    public void BufferLayout_KeysMatchTheReportedBindings()
    {
        using var compiled = Compiled.Compile("keys", ShaderFixtures.ReflectionGlobals);

        SlangReflection reflection = compiled.Program.Reflection;

        // gXform is a standalone ConstantBuffer<Xform> at global scope — the
        // shape neither of the two free sources reaches, and the reason the
        // binding-range join had to work.
        Assert.True(reflection.TryGetBufferLayout(0, 0, out SlangBufferLayout? xform));
        Assert.Equal("gXform", xform.Name);
        Assert.True(xform.TryGetMember("mvp", out SlangBufferMember mvp));
        Assert.Equal(SlangTypeKind.SLANG_TYPE_KIND_MATRIX, mvp.Kind);

        // A texture and a sampler are not buffers.
        Assert.False(reflection.TryGetBufferLayout(0, 1, out _));
        Assert.False(reflection.TryGetBufferLayout(0, 2, out _));

        // And every layout that does exist sits on a buffer-shaped binding.
        for (int i = 0; i < reflection.DescriptorSetCount; i++)
        {
            uint set = reflection.SetIndex(i);

            foreach (SlangDescriptorBinding binding in reflection.Bindings(i))
            {
                if (!reflection.TryGetBufferLayout(set, binding.Slot, out SlangBufferLayout? layout))
                {
                    continue;
                }

                _output.WriteLine($"set {set} slot {binding.Slot} '{layout.Name}' is {binding.Type}");

                Assert.Contains(
                    binding.Type & SlangBindingType.SLANG_BINDING_TYPE_BASE_MASK,
                    (SlangBindingType[])
                    [
                        SlangBindingType.SLANG_BINDING_TYPE_CONSTANT_BUFFER,
                        SlangBindingType.SLANG_BINDING_TYPE_RAW_BUFFER,
                        SlangBindingType.SLANG_BINDING_TYPE_TYPED_BUFFER,
                        SlangBindingType.SLANG_BINDING_TYPE_PARAMETER_BLOCK,
                    ]);
            }
        }
    }

    /// <summary>
    /// Rule 4: a field with zero <c>UNIFORM</c> size occupies no bytes and is
    /// not a member. Listing it at offset 0 with size 0 reads as writable.
    /// </summary>
    [Fact]
    public void BufferLayout_ResourceFieldsAreNotMembers()
    {
        using var compiled = Compiled.Compile("resources", ShaderFixtures.ReflectionMaterialBlock);

        SlangBufferLayout layout = compiled.MaterialLayout();

        Assert.False(layout.TryGetMember("BaseColorMap", out _));
        Assert.False(layout.TryGetMember("Sampler", out _));

        foreach (SlangBufferMember member in layout.Members)
        {
            Assert.NotEqual(SlangTypeKind.SLANG_TYPE_KIND_RESOURCE, member.Kind);
            Assert.NotEqual(SlangTypeKind.SLANG_TYPE_KIND_SAMPLER_STATE, member.Kind);
        }
    }

    /// <summary>
    /// Rule 2, asserted rather than argued: the flat list is losslessly a tree.
    /// </summary>
    /// <remarks>
    /// This is what justifies shipping one representation instead of a flat list
    /// plus a parallel nested type. The test rebuilds the tree from
    /// <c>ParentIndex</c> alone — no string parsing — and checks it against the
    /// nesting the fixture declares.
    /// </remarks>
    [Fact]
    public void BufferLayout_NestedStruct_IsLosslesslyATree()
    {
        using var compiled = Compiled.Compile("tree", ShaderFixtures.ReflectionMaterialBlock);

        SlangBufferLayout layout = compiled.MaterialLayout();

        Assert.True(layout.TryGetMember("Params", out SlangBufferMember paramsMember));
        Assert.Equal(SlangTypeKind.SLANG_TYPE_KIND_STRUCT, paramsMember.Kind);
        Assert.Equal(-1, paramsMember.ParentIndex);

        // Paths are the whole path from the root, not the leaf name.
        Assert.True(layout.TryGetMember("Params.UvScale", out _));
        Assert.False(layout.TryGetMember("UvScale", out _));

        // Rebuild the tree from ParentIndex, the way the type's remarks say to.
        var children = new List<int>[layout.Members.Length];

        for (int i = 0; i < layout.Members.Length; i++)
        {
            children[i] = [];
        }

        var roots = new List<int>();

        for (int i = 0; i < layout.Members.Length; i++)
        {
            int parent = layout.Members[i].ParentIndex;

            if (parent < 0)
            {
                roots.Add(i);
            }
            else
            {
                Assert.InRange(parent, 0, i - 1);
                children[parent].Add(i);
            }
        }

        // The declared nesting: MaterialBlock { float4 Tint; MaterialParams Params; float4x4 Transform; }
        // with MaterialParams { BaseColor, Roughness, Metallic, UvScale, Flags }.
        Assert.Equal(["Tint", "Params", "Transform"], roots.Select(i => layout.Members[i].Name).ToArray());

        int paramsIndex = roots[1];

        Assert.Equal(
            ["Params.BaseColor", "Params.Roughness", "Params.Metallic", "Params.UvScale", "Params.Flags"],
            children[paramsIndex].Select(i => layout.Members[i].Name).ToArray());

        // Pre-order means a struct's children are contiguous and follow it.
        Assert.Equal(Enumerable.Range(paramsIndex + 1, 5).ToArray(), children[paramsIndex].ToArray());

        // The two non-struct roots have none.
        Assert.Empty(children[roots[0]]);
        Assert.Empty(children[roots[2]]);
    }

    /// <summary>
    /// Row- or column-major decides whether a <c>float4x4</c> is written
    /// transposed, with no other symptom.
    /// </summary>
    [Fact]
    public void BufferLayout_Matrix_ReportsLayoutMode()
    {
        using var compiled = Compiled.Compile("matrix", ShaderFixtures.ReflectionMaterialBlock);

        Assert.True(compiled.MaterialLayout().TryGetMember("Transform", out SlangBufferMember transform));

        // Measured on v2026.14.1 / win-x64 with a default SlangSessionDescription.
        // Note that the emitted SPIR-V decorates this same member ColMajor —
        // the two conventions are inverted and do not disagree. See
        // SlangBufferMember.MatrixLayout.
        Assert.Equal(SlangMatrixLayoutMode.SLANG_MATRIX_LAYOUT_ROW_MAJOR, transform.MatrixLayout);
        Assert.Equal(4u, transform.RowCount);
        Assert.Equal(4u, transform.ColumnCount);

        // Slang has no dedicated matrix-stride getter. GetElementTypeLayout on
        // the matrix yields the row vector's layout, whose UNIFORM stride is 16
        // — measured, not derived from Size / RowCount.
        Assert.Equal(16u, transform.MatrixStride);

        // The byte-level statement the words argue about: row j starts at
        // Offset + j * MatrixStride, so RowCount strides fill the member. This
        // is what a caller actually needs, and it is true whichever word each
        // side uses.
        Assert.Equal(transform.Size, transform.RowCount * transform.MatrixStride);
    }

    [Fact]
    public void BufferLayout_PushConstantBlock_HasMembers()
    {
        using var compiled = Compiled.Compile("pushLayout", ShaderFixtures.ReflectionGlobals);

        Assert.True(compiled.Program.Reflection.TryGetPushConstantLayout(out SlangBufferLayout? push));

        Assert.Equal("gPush", push.Name);
        Assert.Equal(16u, push.Size);
        Assert.Equal(compiled.Program.Reflection.PushConstantRanges[0].Size, push.Size);

        Assert.Equal(1, push.Members.Length);
        Assert.Equal("tint", push.Members[0].Name);
        Assert.Equal(0u, push.Members[0].Offset);
        Assert.Equal(16u, push.Members[0].Size);
        Assert.Equal(SlangTypeKind.SLANG_TYPE_KIND_VECTOR, push.Members[0].Kind);
        Assert.Equal(4u, push.Members[0].ComponentCount);
    }

    /// <summary>
    /// <c>Size</c> alone is not enough to fill a buffer by hand: a padded
    /// <c>float3</c> is 12 bytes of data in a 16-byte footprint.
    /// </summary>
    [Fact]
    public void BufferLayout_StrideAndAlignment_AreReported()
    {
        using var compiled = Compiled.Compile("strides", ShaderFixtures.ReflectionMaterialBlock);

        SlangBufferLayout layout = compiled.MaterialLayout();

        Assert.True(layout.TryGetMember("Transform", out SlangBufferMember transform));
        Assert.Equal(64u, transform.Stride);
        Assert.Equal(16u, transform.Alignment);

        Assert.True(layout.TryGetMember("Params.BaseColor", out SlangBufferMember baseColor));
        Assert.Equal(12u, baseColor.Size);
        Assert.Equal(16u, baseColor.Stride);
        Assert.Equal(16u, baseColor.Alignment);

        Assert.True(layout.TryGetMember("Params.Roughness", out SlangBufferMember roughness));
        Assert.Equal(4u, roughness.Size);
        Assert.Equal(4u, roughness.Stride);
        Assert.Equal(4u, roughness.Alignment);
    }

    [Fact]
    public void Reflection_ToJson_ContainsDeclaredParameters()
    {
        using var compiled = Compiled.Compile("json", ShaderFixtures.ReflectionMaterialBlock);

        SlangReflection reflection = compiled.Program.Reflection;
        string json = reflection.ToJson();

        _output.WriteLine(json[..Math.Min(400, json.Length)]);

        Assert.NotEmpty(json);
        Assert.Contains("gMaterial", json, StringComparison.Ordinal);
        Assert.Contains("UvScale", json, StringComparison.Ordinal);

        // Lazy and cached: the second call is the same instance, not a second
        // serialization of the whole layout.
        Assert.Same(json, reflection.ToJson());
    }

    private static void AssertMember(SlangBufferLayout layout, string path, uint offset, uint size, SlangTypeKind kind)
    {
        Assert.True(layout.TryGetMember(path, out SlangBufferMember member), $"No member '{path}'.");
        Assert.Equal(offset, member.Offset);
        Assert.Equal(size, member.Size);
        Assert.Equal(kind, member.Kind);
        Assert.False(member.IsUnsized);
    }

    /// <summary>A compiled single-module program and everything it owns.</summary>
    private sealed class Compiled : IDisposable
    {
        private readonly SlangCompiler _compiler;
        private readonly SlangSession _session;

        private Compiled(SlangCompiler compiler, SlangSession session, SlangProgram program)
        {
            _compiler = compiler;
            _session = session;
            Program = program;
        }

        public SlangProgram Program { get; }

        public static Compiled Compile(string moduleName, string source)
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

                return new Compiled(compiler, session, program);
            }
            catch
            {
                compiler.Dispose();

                throw;
            }
        }

        /// <summary>
        /// The <c>gMaterial</c> block's implicit uniform buffer: binding 0 of
        /// the one populated set, whatever number that set has.
        /// </summary>
        public SlangBufferLayout MaterialLayout()
        {
            SlangReflection reflection = Program.Reflection;

            Assert.Equal(1, reflection.DescriptorSetCount);
            Assert.True(reflection.TryGetBufferLayout(reflection.SetIndex(0), 0, out SlangBufferLayout? layout));

            return layout;
        }

        public void Dispose()
        {
            Program.Dispose();
            _session.Dispose();
            _compiler.Dispose();
        }
    }
}
