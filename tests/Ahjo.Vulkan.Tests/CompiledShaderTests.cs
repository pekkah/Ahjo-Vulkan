using System.Buffers.Binary;
using System.IO;
using System.Text;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// The driverless proof that the build compiled the SPIR-V fixtures (#171).
/// Reads the build's own <c>Shaders/*.spv</c> output and checks the
/// properties the rest of the suite silently depends on. Deliberately has no
/// <c>TestGate</c> call: it needs no Vulkan driver, so it runs at tier
/// <c>none</c> on CI, and a missing file is a failure, not a skip — the
/// shared SlangShader build step fails the build rather than produce no
/// output, so an absent fixture means something is broken.
/// </summary>
/// <remarks>
/// Design: <c>docs/design/specs/2026-11-06-issue-171-172-slang-shader-build-design.md</c>
/// (A6). The build step itself lives in
/// <c>tools/msbuild/Ahjo.Vulkan.SlangShaders.targets</c>.
/// </remarks>
public sealed class CompiledShaderTests
{
    private const uint SpirvMagic = 0x07230203;

    private const uint OpName            = 5;
    private const uint OpTypePointer     = 32;
    private const uint OpVariable        = 59;
    private const uint OpEntryPoint      = 15;
    private const uint OpCapability      = 17;
    private const uint OpSpecConstant    = 50;
    private const uint OpDecorate        = 71;
    private const uint OpMemberDecorate  = 72;
    private const uint OpExecutionModeId = 331;

    private const uint DecorationSpecId      = 1;
    private const uint DecorationArrayStride = 6;
    private const uint DecorationOffset      = 35;

    private const uint StorageClassPushConstant = 9;

    private const uint CapabilityShader         = 1;
    private const uint CapabilityDrawParameters = 4427;
    private const uint CapabilityMeshShadingExt = 5283;

    private const uint ExecutionModelTaskExt = 5364;
    private const uint ExecutionModelMeshExt = 5365;

    private const uint DecorationRowMajor = 4;
    private const uint DecorationColMajor = 5;

    private const uint ExecutionModeLocalSizeId = 38;

    [Theory]
    [InlineData("triangle.vert.spv")]
    [InlineData("triangle.frag.spv")]
    [InlineData("near_plane_tri.vert.spv")]
    [InlineData("fill.comp.spv")]
    [InlineData("spec_fill.comp.spv")]
    [InlineData("mesh_tri.mesh.spv")]
    [InlineData("mesh_tri.task.spv")]
    [InlineData("matrix_push.vert.spv")]
    [InlineData("push_std430.vert.spv")]
    public void EveryFixture_IsCompiledSpirv(string name)
    {
        string path = SpvPath(name);
        Assert.True(File.Exists(path), $"{path} is missing; the SlangShader build step should have produced it or failed the build.");

        long length = new FileInfo(path).Length;
        Assert.True(length > 0 && length % 4 == 0, $"{name} is {length} bytes, not a non-empty whole number of SPIR-V words.");

        SpirvFacts facts = SpirvFacts.Read(path);
        var entry = Assert.Single(facts.EntryPoints);
        Assert.Equal("main", entry.Name);
    }

    [Theory]
    [InlineData("triangle.vert.spv")]
    [InlineData("near_plane_tri.vert.spv")]
    public void GlslVertexIndexFixtures_DeclareNoDrawParameters(string name)
    {
        SpirvFacts facts = SpirvFacts.Read(SpvPath(name));

        Assert.DoesNotContain(CapabilityDrawParameters, facts.Capabilities);
        Assert.Equal(CapabilityShader, Assert.Single(facts.Capabilities));
    }

    [Fact]
    public void GlslMatrix_KeepsGlslcColumnMajorLayout()
    {
        SpirvFacts facts = SpirvFacts.Read(SpvPath("matrix_push.vert.spv"));

        const string Why =
            "GLSL compiled through Slang must keep glslc's column-major meaning; check -matrix-layout-row-major on GLSL items in tools/msbuild/Ahjo.Vulkan.SlangShaders.targets (spec E4).";
        Assert.True(facts.MemberDecorations.Contains(DecorationColMajor), Why);
        Assert.False(facts.MemberDecorations.Contains(DecorationRowMajor), Why);
    }

    [Fact]
    public void MeshFixtures_AreMeshShadingModules()
    {
        SpirvFacts mesh = SpirvFacts.Read(SpvPath("mesh_tri.mesh.spv"));
        Assert.Equal(ExecutionModelMeshExt, Assert.Single(mesh.EntryPoints).Model);
        Assert.Contains(CapabilityMeshShadingExt, mesh.Capabilities);

        SpirvFacts task = SpirvFacts.Read(SpvPath("mesh_tri.task.spv"));
        Assert.Equal(ExecutionModelTaskExt, Assert.Single(task.EntryPoints).Model);
        Assert.Contains(CapabilityMeshShadingExt, task.Capabilities);
    }

    /// <summary>
    /// Pins <em>why</em> <c>SpecializationInfoTests</c> and
    /// <c>SpecializationInfoBenchmarks</c> enable <c>maintenance4</c>: Slang
    /// emits <c>OpExecutionModeId LocalSizeId</c> for <c>spec_fill.comp</c>'s
    /// spec-constant workgroup size (spec E6). If this ever fails because a
    /// Slang bump stopped emitting <c>LocalSizeId</c>, those
    /// <c>maintenance4</c> enables may be removable. Do not delete this test
    /// to make a bump green.
    /// </summary>
    [Fact]
    public void SpecFill_UsesLocalSizeId_WhichIsWhyItsDevicesEnableMaintenance4()
    {
        SpirvFacts facts = SpirvFacts.Read(SpvPath("spec_fill.comp.spv"));

        Assert.Contains(ExecutionModeLocalSizeId, facts.ExecutionModeIds);
    }

    /// <summary>
    /// An unspecialized <c>spec_fill</c> pipeline must get glslc's 1-wide
    /// workgroup, not 0: Slang defaults a bare <c>local_size_x_id</c> to 0, so
    /// the fixture declares <c>layout(constant_id = 0) const int … = 1</c>
    /// explicitly. This pins that the declaration still reaches the module.
    /// </summary>
    [Fact]
    public void SpecFill_WorkgroupSizeSpecConstant_DefaultsToOne()
    {
        SpirvFacts facts = SpirvFacts.Read(SpvPath("spec_fill.comp.spv"));

        uint id = Assert.Single(facts.SpecIds, kv => kv.Value == 0).Key;
        Assert.True(facts.SpecConstantDefaults.TryGetValue(id, out uint value), "SpecId 0 does not decorate an OpSpecConstant.");
        Assert.Equal(1u, value);
    }

    /// <summary>
    /// Slang's GLSL frontend lays a push-constant block out std140 unless the
    /// block says <c>std430</c> (glslc defaulted to std430), and no slangc flag
    /// changes that. <c>push_std430.vert</c>'s block is shaped so the two
    /// layouts disagree, so a lost qualifier — or a Slang change in how it is
    /// honoured — fails here instead of silently shifting offsets.
    /// </summary>
    [Fact]
    public void GlslPushConstants_KeepGlslcStd430Offsets()
    {
        SpirvFacts facts = SpirvFacts.Read(SpvPath("push_std430.vert.spv"));

        const string Why =
            "GLSL push-constant blocks must keep glslc's std430 layout; every GLSL push block needs layout(push_constant, std430) (tools/CLAUDE.md).";
        Assert.True(facts.ArrayStrides.SetEquals([4u]), $"{Why} ArrayStrides: {string.Join(", ", facts.ArrayStrides)}");
        Assert.True(facts.MemberOffsets.Contains((1u, 8u)), $"{Why} Member offsets: {string.Join(", ", facts.MemberOffsets)}");
        Assert.DoesNotContain((1u, 32u), facts.MemberOffsets);
    }

    /// <summary>
    /// The guard for the std430 rule on <em>every</em> compiled module, so a
    /// future GLSL fixture that writes a bare <c>layout(push_constant)</c>
    /// fails here rather than silently getting std140 offsets.
    /// </summary>
    /// <remarks>
    /// Name-based on purpose: Slang names the struct behind a push-constant
    /// block <c>…_std140</c> or <c>…_std430</c> (e.g.
    /// <c>SLANG_ParameterGroup_Pc_std140</c>), and that is the only signal that
    /// works for every block. A structural check cannot tell the two layouts
    /// apart for blocks where they coincide (a lone <c>uint</c> or
    /// <c>mat4</c>), which is exactly the shape today's fixtures have. If a
    /// Slang bump stops emitting these names, the "named" assertion below
    /// fails loudly instead of the check going vacuous.
    /// </remarks>
    [Fact]
    public void NoCompiledModule_HasAStd140PushConstantBlock()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "Shaders");
        string[] modules = Directory.GetFiles(dir, "*.spv");
        Assert.NotEmpty(modules);

        var std140 = new List<string>();
        int pushBlocks = 0;
        foreach (string path in modules)
        {
            SpirvFacts facts = SpirvFacts.Read(path);
            foreach (uint structId in facts.PushConstantBlockTypes)
            {
                pushBlocks++;
                Assert.True(
                    facts.Names.TryGetValue(structId, out string? name),
                    $"{Path.GetFileName(path)}: push-constant block type %{structId} has no OpName; this check relies on Slang's _std140/_std430 suffix.");
                if (name!.EndsWith("_std140", StringComparison.Ordinal))
                    std140.Add($"{Path.GetFileName(path)} ({name})");
            }
        }

        // fill.comp, matrix_push.vert and push_std430.vert all have one, so
        // a zero here means the reader stopped finding push blocks at all.
        Assert.True(pushBlocks > 0, "No push-constant blocks found in any compiled module; the check would be vacuous.");
        Assert.True(
            std140.Count == 0,
            "GLSL push-constant blocks must be layout(push_constant, std430) — Slang's GLSL default is std140 where glslc's was std430 (tools/CLAUDE.md). std140 blocks: " +
            string.Join(", ", std140));
    }

    private static string SpvPath(string name) => Path.Combine(AppContext.BaseDirectory, "Shaders", name);

    /// <summary>The handful of module facts the cases above assert on.</summary>
    private sealed class SpirvFacts
    {
        public HashSet<uint> Capabilities { get; } = [];
        public List<(uint Model, string Name)> EntryPoints { get; } = [];
        public HashSet<uint> MemberDecorations { get; } = [];
        public HashSet<uint> ExecutionModeIds { get; } = [];
        /// <summary>Result id → SpecId literal, from <c>OpDecorate … SpecId</c>.</summary>
        public Dictionary<uint, uint> SpecIds { get; } = [];
        /// <summary>Result id → default value, from 32-bit <c>OpSpecConstant</c>.</summary>
        public Dictionary<uint, uint> SpecConstantDefaults { get; } = [];
        public HashSet<uint> ArrayStrides { get; } = [];
        /// <summary>(member index, byte offset) pairs from <c>OpMemberDecorate … Offset</c>.</summary>
        public HashSet<(uint Member, uint Offset)> MemberOffsets { get; } = [];
        /// <summary>Id → debug name, from <c>OpName</c>.</summary>
        public Dictionary<uint, string> Names { get; } = [];
        /// <summary>Pointee type of every <c>OpVariable</c> in the PushConstant storage class.</summary>
        public List<uint> PushConstantBlockTypes { get; } = [];

        public static SpirvFacts Read(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length >= 20 && bytes.Length % 4 == 0, $"{path} is too short or not word-aligned.");

            var words = new uint[bytes.Length / 4];
            for (int i = 0; i < words.Length; i++)
                words[i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i * 4, 4));

            Assert.True(words[0] == SpirvMagic, $"{path}: word 0 is 0x{words[0]:X8}, not the SPIR-V magic 0x{SpirvMagic:X8}.");

            var facts = new SpirvFacts();
            var pointeeOf = new Dictionary<uint, uint>();
            var pushVariableTypes = new List<uint>();
            int pc = 5;
            while (pc < words.Length)
            {
                uint opcode    = words[pc] & 0xFFFF;
                int  wordCount = (int)(words[pc] >> 16);
                Assert.True(wordCount > 0, $"{path}: zero word count at word {pc}.");
                Assert.True(pc + wordCount <= words.Length, $"{path}: instruction at word {pc} runs past the end.");

                switch (opcode)
                {
                    case OpCapability:
                        facts.Capabilities.Add(words[pc + 1]);
                        break;
                    case OpEntryPoint:
                        facts.EntryPoints.Add((words[pc + 1], ReadLiteralString(words, pc + 3, pc + wordCount)));
                        break;
                    case OpMemberDecorate:
                        facts.MemberDecorations.Add(words[pc + 3]);
                        if (words[pc + 3] == DecorationOffset && wordCount >= 5)
                            facts.MemberOffsets.Add((words[pc + 2], words[pc + 4]));
                        break;
                    case OpDecorate when wordCount >= 4 && words[pc + 2] == DecorationSpecId:
                        facts.SpecIds[words[pc + 1]] = words[pc + 3];
                        break;
                    case OpDecorate when wordCount >= 4 && words[pc + 2] == DecorationArrayStride:
                        facts.ArrayStrides.Add(words[pc + 3]);
                        break;
                    case OpSpecConstant when wordCount == 4:
                        facts.SpecConstantDefaults[words[pc + 2]] = words[pc + 3];
                        break;
                    case OpExecutionModeId:
                        facts.ExecutionModeIds.Add(words[pc + 2]);
                        break;
                    case OpName when wordCount >= 3:
                        facts.Names[words[pc + 1]] = ReadLiteralString(words, pc + 2, pc + wordCount);
                        break;
                    case OpTypePointer when wordCount == 4:
                        pointeeOf[words[pc + 1]] = words[pc + 3];
                        break;
                    case OpVariable when wordCount >= 4 && words[pc + 3] == StorageClassPushConstant:
                        pushVariableTypes.Add(words[pc + 1]);
                        break;
                }

                pc += wordCount;
            }

            foreach (uint pointerType in pushVariableTypes)
            {
                Assert.True(pointeeOf.TryGetValue(pointerType, out uint pointee), $"{path}: push-constant variable type %{pointerType} is not an OpTypePointer.");
                facts.PushConstantBlockTypes.Add(pointee);
            }

            return facts;
        }

        /// <summary>A null-terminated UTF-8 literal packed little-endian into words [start, end).</summary>
        private static string ReadLiteralString(uint[] words, int start, int end)
        {
            var bytes = new List<byte>();
            for (int w = start; w < end; w++)
            {
                for (int shift = 0; shift < 32; shift += 8)
                {
                    byte b = (byte)(words[w] >> shift);
                    if (b == 0)
                        return Encoding.UTF8.GetString(bytes.ToArray());
                    bytes.Add(b);
                }
            }

            Assert.Fail("Unterminated SPIR-V literal string.");
            return string.Empty;
        }
    }
}
