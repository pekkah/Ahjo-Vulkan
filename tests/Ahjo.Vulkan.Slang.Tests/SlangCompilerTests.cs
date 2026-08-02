using Xunit;

namespace Ahjo.Vulkan.Slang.Tests;

/// <summary>
/// The compiler API: compile Slang source, get SPIR-V or get the compiler's
/// own error text — never an empty blob and a shrug.
/// </summary>
/// <remarks>
/// Only <c>Spirv_FeedsCreateShaderModule</c> touches Vulkan. Everything else
/// runs with no loader and no ICD, because compiling shader text to bytes has
/// no business needing a GPU.
/// </remarks>
public sealed class SlangCompilerTests
{
    private readonly ITestOutputHelper _output;

    public SlangCompilerTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Create_ExposesPinnedBuildTag()
    {
        using var compiler = SlangCompiler.Create();

        // The staged binary is what answers this, so a SlangVersion bump that
        // did not refresh native/slang/staged/<rid>/ fails right here.
        Assert.Equal(SlangPinnedVersion.WithoutLeadingV, compiler.BuildTag);
    }

    [Fact]
    public void Compile_FromSourceString_OneEntryPoint_ProducesSpirv()
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);
        using SlangProgram program = session.Compile(new SlangCompileRequest
        {
            ModuleName = "vertexOnly",
            Source = ShaderFixtures.VertexOnly,
        });

        Assert.Equal(1, program.EntryPointCount);
        Assert.Equal("vertexMain", program.EntryPoint(0).Name);
        Assert.Equal(ShaderStages.Vertex, program.EntryPoint(0).Stage);

        ReadOnlySpan<uint> spirv = program.Spirv(0);

        Assert.False(spirv.IsEmpty);
        Assert.Equal(ShaderFixtures.SpirvMagic, spirv[0]);
    }

    [Fact]
    public void Compile_FromFile_ProducesSpirv()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Shaders", "triangle.slang");

        Assert.True(File.Exists(path), $"Missing test fixture: {path}");

        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);
        using SlangProgram program = session.Compile(new SlangCompileRequest { Path = path });

        Assert.Equal(2, program.EntryPointCount);

        for (int i = 0; i < program.EntryPointCount; i++)
        {
            ReadOnlySpan<uint> spirv = program.Spirv(i);

            Assert.Equal(ShaderFixtures.SpirvMagic, spirv[0]);
        }
    }

    [Fact]
    public void Compile_AllEntryPoints_WhenEntryPointsIsNull()
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);
        using SlangProgram program = session.Compile(new SlangCompileRequest
        {
            ModuleName = "vertAndFrag",
            Source = ShaderFixtures.VertexAndFragment,

            // The point of the test: EntryPoints stays null.
        });

        Assert.Equal(2, program.EntryPointCount);
        Assert.Equal(new SlangEntryPointInfo("vertexMain", ShaderStages.Vertex, 1, 1, 1), program.EntryPoint(0));
        Assert.Equal(new SlangEntryPointInfo("fragmentMain", ShaderStages.Fragment, 1, 1, 1), program.EntryPoint(1));
    }

    [Fact]
    public void Compile_NamedEntryPoints_AreLinkedInTheOrderRequested()
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);
        using SlangProgram program = session.Compile(new SlangCompileRequest
        {
            ModuleName = "namedOrder",
            Source = ShaderFixtures.VertexAndFragment,
            EntryPoints = ["fragmentMain", "vertexMain"],
        });

        Assert.Equal(2, program.EntryPointCount);
        Assert.Equal("fragmentMain", program.EntryPoint(0).Name);
        Assert.Equal("vertexMain", program.EntryPoint(1).Name);
    }

    /// <summary>
    /// The regression pin for routing <see cref="SlangSession.Compile"/> through
    /// <see cref="SlangProgramBuilder"/> (issue #177): the convenience path must
    /// compose <c>[module, ep₀, ep₁, …]</c>, and the composition order <em>is</em>
    /// the layout.
    /// </summary>
    /// <remarks>
    /// Entry-point order and every reflected <c>(set, slot)</c> are asserted as
    /// literals. A composition that differed by so much as one component's
    /// position would move a set or a binding number here — which is a
    /// behaviour change, not a number to rebaseline.
    /// </remarks>
    [Fact]
    public void Compile_EntryPointOrder_IsRequestOrder()
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);
        using SlangProgram program = session.Compile(new SlangCompileRequest
        {
            ModuleName = "orderPin",
            Source = ShaderFixtures.ReflectionTwoBlocks,
            EntryPoints = ["vertexMain", "fragmentMain"],
        });

        Assert.Equal(2, program.EntryPointCount);
        Assert.Equal(new SlangEntryPointInfo("vertexMain", ShaderStages.Vertex, 1, 1, 1), program.EntryPoint(0));
        Assert.Equal(new SlangEntryPointInfo("fragmentMain", ShaderStages.Fragment, 1, 1, 1), program.EntryPoint(1));

        SlangReflection reflection = program.Reflection;

        Assert.Equal(3, reflection.DescriptorSetCount);
        Assert.Equal(3u, reflection.SetLayoutSlotCount);

        var layout = new List<string>();

        for (int i = 0; i < reflection.DescriptorSetCount; i++)
        {
            foreach (SlangDescriptorBinding binding in reflection.Bindings(i))
            {
                layout.Add($"{reflection.SetIndex(i)}:{binding.Slot}:{binding.Type}");
            }
        }

        Assert.Equal(
            [
                "0:0:SLANG_BINDING_TYPE_CONSTANT_BUFFER",
                "0:1:SLANG_BINDING_TYPE_TEXTURE",
                "0:2:SLANG_BINDING_TYPE_SAMPLER",
                "1:0:SLANG_BINDING_TYPE_CONSTANT_BUFFER",
                "2:0:SLANG_BINDING_TYPE_CONSTANT_BUFFER",
            ],
            layout);
    }

    /// <summary>
    /// The acceptance criterion issue #166 states: a broken compile is an
    /// exception carrying the compiler's text, never a silent empty blob.
    /// </summary>
    [Fact]
    public void Compile_SyntaxError_ThrowsWithCompilerText()
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);

        var ex = Assert.Throws<SlangCompilationException>(() => session.Compile(new SlangCompileRequest
        {
            ModuleName = "broken",
            Source = ShaderFixtures.SyntaxError,
        }));

        Assert.Contains("error[E30015]", ex.Diagnostics, StringComparison.Ordinal);
        Assert.Contains("undefined identifier", ex.Diagnostics, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    [Fact]
    public void Compile_UndefinedEntryPoint_Throws()
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);

        var ex = Assert.Throws<SlangCompilationException>(() => session.Compile(new SlangCompileRequest
        {
            ModuleName = "missingEntryPoint",
            Source = ShaderFixtures.VertexAndFragment,
            EntryPoints = ["notThere"],
        }));

        Assert.Contains("notThere", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_NeitherPathNorSource_Throws()
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);

        Assert.Throws<ArgumentException>(() => session.Compile(default));
    }

    [Fact]
    public void Warnings_SurfaceOnSuccess()
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);
        using SlangProgram program = session.Compile(new SlangCompileRequest
        {
            ModuleName = "warns",
            Source = ShaderFixtures.ProducesWarning,
        });

        Assert.NotNull(program.Warnings);
        Assert.Contains("unreachable code", program.Warnings, StringComparison.Ordinal);

        // A warning is not a failure: the blob is still valid SPIR-V.
        Assert.Equal(ShaderFixtures.SpirvMagic, program.Spirv(0)[0]);
    }

    /// <summary>
    /// Issue #166, <b>OPEN-1</b>, resolved to "ship <c>slang-glslang</c>".
    /// Every optimization level must produce valid SPIR-V <em>and</em> reach
    /// the <c>spirv-opt</c> downstream compiler.
    /// </summary>
    /// <remarks>
    /// <para>The diagnostic assertion is the whole point. Before
    /// <c>slang-glslang</c> joined the shipped set, every level above
    /// <see cref="SlangOptimizationLevel.None"/> returned <c>SLANG_OK</c> and
    /// a well-formed module while quietly reporting</para>
    /// <code>
    /// error[E00100]: failed to load downstream compiler 'spirv-opt'
    /// note[E99996]: failed to load dynamic library 'slang-glslang-2026.14.1'
    /// </code>
    /// <para>into <see cref="SlangProgram.Warnings"/> — so a "valid SPIR-V at
    /// every level" assertion passed for a whole phase over an
    /// <c>Optimization</c> setting that did nothing. Losing the library again
    /// (a staging regression, a trimmed pack list) has to fail loudly, and
    /// this is where.</para>
    /// </remarks>
    [Theory]
    [InlineData(SlangOptimizationLevel.None)]
    [InlineData(SlangOptimizationLevel.Default)]
    [InlineData(SlangOptimizationLevel.High)]
    [InlineData(SlangOptimizationLevel.Maximal)]
    public void OptimizationLevels_ReachTheDownstreamCompiler(SlangOptimizationLevel level)
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(new SlangSessionDescription { Optimization = level });
        using SlangProgram program = session.Compile(new SlangCompileRequest
        {
            ModuleName = "opt" + level,
            Source = ShaderFixtures.RedundantVertex,
        });

        for (int i = 0; i < program.EntryPointCount; i++)
        {
            ReadOnlySpan<uint> spirv = program.Spirv(i);

            Assert.False(spirv.IsEmpty, $"Optimization level {level} produced an empty blob for entry point {i}.");
            Assert.Equal(ShaderFixtures.SpirvMagic, spirv[0]);

            _output.WriteLine($"level={level} entryPoint={i} words={spirv.Length}");
        }

        string warnings = program.Warnings ?? string.Empty;

        _output.WriteLine($"level={level} warnings={program.Warnings ?? "<none>"}");

        Assert.DoesNotContain("spirv-opt", warnings, StringComparison.Ordinal);
        Assert.DoesNotContain("failed to load downstream compiler", warnings, StringComparison.Ordinal);
    }

    /// <summary>
    /// <see cref="SlangSessionDescription.Optimization"/> must actually change
    /// the emitted SPIR-V — the assertion that "the level was accepted" cannot
    /// make.
    /// </summary>
    /// <remarks>
    /// <para>Compiles <see cref="ShaderFixtures.RedundantVertex"/> — a
    /// fixed-trip loop that folds, a dead local chain and two arithmetic
    /// identities — at <see cref="SlangOptimizationLevel.None"/> and at
    /// <see cref="SlangOptimizationLevel.Maximal"/> and requires the second to
    /// be strictly smaller. A trivial shader is not usable here: it gives the
    /// optimizer nothing to remove and emits identical words at every level
    /// whether <c>spirv-opt</c> loaded or not, which is precisely how the
    /// no-op went unnoticed.</para>
    /// <para>Measured on <c>v2026.14.1</c> / linux-x64: 317 words at
    /// <c>None</c>, 245 at <c>Maximal</c>. Withholding
    /// <c>libslang-glslang-2026.14.1.so</c> from the output directory puts
    /// both back to 317 and fails this test — which is the evidence that
    /// shipping it is what makes the level mean anything. Only the direction
    /// is asserted; the exact counts are a property of upstream's optimizer,
    /// not of this wrapper.</para>
    /// </remarks>
    [Fact]
    public void Optimization_ChangesTheEmittedSpirv()
    {
        int unoptimized = CompileRedundantVertexWordCount(SlangOptimizationLevel.None);
        int optimized = CompileRedundantVertexWordCount(SlangOptimizationLevel.Maximal);

        _output.WriteLine($"None={unoptimized} words, Maximal={optimized} words");

        Assert.True(
            optimized < unoptimized,
            $"Optimization had no effect: None emitted {unoptimized} words and Maximal emitted {optimized}. " +
            "That is what an absent slang-glslang (no spirv-opt) looks like — check the shipped set in " +
            "src/Ahjo.Vulkan.Slang.Native/Ahjo.Vulkan.Slang.Native.csproj before touching this assertion.");
    }

    private static int CompileRedundantVertexWordCount(SlangOptimizationLevel level)
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(new SlangSessionDescription { Optimization = level });
        using SlangProgram program = session.Compile(new SlangCompileRequest
        {
            ModuleName = "redundant" + level,
            Source = ShaderFixtures.RedundantVertex,
        });

        return program.Spirv(0).Length;
    }

    /// <summary>
    /// Issue #166, <b>OPEN-3</b>. <see cref="SlangCompiler.Dispose"/> releases
    /// the global session and does not call <c>slang_shutdown()</c>, so a
    /// second compiler in the same process must work.
    /// </summary>
    [Fact]
    public void TwoCompilers_InSequence_Work()
    {
        string firstTag;

        using (var first = SlangCompiler.Create())
        {
            firstTag = first.BuildTag;

            using SlangSession session = first.CreateSession(default);
            using SlangProgram program = session.Compile(new SlangCompileRequest
            {
                ModuleName = "lifetimeFirst",
                Source = ShaderFixtures.VertexOnly,
            });

            Assert.Equal(ShaderFixtures.SpirvMagic, program.Spirv(0)[0]);
        }

        using (var second = SlangCompiler.Create())
        {
            Assert.Equal(firstTag, second.BuildTag);

            using SlangSession session = second.CreateSession(default);
            using SlangProgram program = session.Compile(new SlangCompileRequest
            {
                ModuleName = "lifetimeSecond",
                Source = ShaderFixtures.VertexOnly,
            });

            Assert.Equal(ShaderFixtures.SpirvMagic, program.Spirv(0)[0]);
        }
    }

    [Fact]
    public void CreateSession_UnknownProfile_Throws()
    {
        using var compiler = SlangCompiler.Create();

        var ex = Assert.Throws<SlangCompilationException>(() => compiler.CreateSession(
            new SlangSessionDescription { SpirvProfile = Utf8Name.FromLiteral("not_a_profile"u8) }));

        Assert.Contains("not_a_profile", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Spirv_DiffersPerEntryPoint()
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);
        using SlangProgram program = session.Compile(new SlangCompileRequest
        {
            ModuleName = "cached",
            Source = ShaderFixtures.VertexAndFragment,
        });

        // Two entry points must not hand back the same blob — that is the
        // failure mode a naive "generate once, reuse" cache produces.
        Assert.NotEqual(program.Spirv(0).Length, program.Spirv(1).Length);
    }
}
