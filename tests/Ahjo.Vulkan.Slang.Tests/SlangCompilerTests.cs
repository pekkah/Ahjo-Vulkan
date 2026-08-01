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
        Assert.Equal(new SlangEntryPointInfo("vertexMain", ShaderStages.Vertex), program.EntryPoint(0));
        Assert.Equal(new SlangEntryPointInfo("fragmentMain", ShaderStages.Fragment), program.EntryPoint(1));
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
    /// Issue #166, <b>OPEN-1</b>. Every optimization level must produce valid
    /// SPIR-V with the binary subset <c>Ahjo.Vulkan.Slang.Native</c> ships.
    /// </summary>
    /// <remarks>
    /// <para>Measured on <c>v2026.14.1</c> / linux-x64, one process per level,
    /// so a cached load failure cannot mask a later level: <b>every level
    /// succeeds</b> — <c>SLANG_OK</c>, a valid SPIR-V module, byte-identical
    /// output at all four levels. But levels above
    /// <see cref="SlangOptimizationLevel.None"/> put this in the diagnostics
    /// blob of the first <c>getEntryPointCode</c> call:</para>
    /// <code>
    /// error[E00100]: failed to load downstream compiler 'spirv-opt'
    /// note[E99996]: failed to load dynamic library 'slang-glslang-2026.14.1'
    /// </code>
    /// <para>It reaches the caller as <see cref="SlangProgram.Warnings"/>
    /// rather than being swallowed, and the identical blob sizes are the
    /// measurement that says the level is a no-op without that library.
    /// Whether to ship <c>slang-glslang</c> is a human decision; this test
    /// does not encode either answer, because the diagnostic text is the part
    /// that would change and it has not been verified on <c>win-x64</c>.</para>
    /// </remarks>
    [Theory]
    [InlineData(SlangOptimizationLevel.None)]
    [InlineData(SlangOptimizationLevel.Default)]
    [InlineData(SlangOptimizationLevel.High)]
    [InlineData(SlangOptimizationLevel.Maximal)]
    public void OptimizationLevels_AllSucceed(SlangOptimizationLevel level)
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(new SlangSessionDescription { Optimization = level });
        using SlangProgram program = session.Compile(new SlangCompileRequest
        {
            ModuleName = "opt" + level,
            Source = ShaderFixtures.VertexAndFragment,
        });

        for (int i = 0; i < program.EntryPointCount; i++)
        {
            ReadOnlySpan<uint> spirv = program.Spirv(i);

            Assert.False(spirv.IsEmpty, $"Optimization level {level} produced an empty blob for entry point {i}.");
            Assert.Equal(ShaderFixtures.SpirvMagic, spirv[0]);

            // Printed even on a pass: this is the OPEN-1 evidence a human
            // quotes when deciding whether slang-glslang ships. Word counts
            // that are identical across levels mean the level is a no-op.
            _output.WriteLine($"level={level} entryPoint={i} words={spirv.Length}");
        }

        _output.WriteLine($"level={level} warnings={program.Warnings ?? "<none>"}");
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
