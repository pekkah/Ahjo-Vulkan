using Xunit;

namespace Ahjo.Vulkan.Slang.Tests;

/// <summary>
/// Composition: N modules plus N entry points, composed and linked in the
/// order the caller asked for.
/// </summary>
/// <remarks>
/// None of this touches Vulkan. The consumer shape being pinned here is a
/// material system that assembles a program at run time out of a shared
/// module, a geometry module and a per-material module.
/// </remarks>
public sealed class SlangProgramBuilderTests
{
    private readonly ITestOutputHelper _output;

    public SlangProgramBuilderTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Compose_ThreeModulesTwoEntryPoints_Links()
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);
        using Composition composition = Composition.Load(session);

        using SlangProgram program = session.CreateProgram()
            .Add(composition.Common)
            .Add(composition.Geometry)
            .Add(composition.Material)
            .Add(composition.Vertex)
            .Add(composition.Fragment)
            .Link();

        Assert.Equal(2, program.EntryPointCount);
        Assert.Equal(new SlangEntryPointInfo("vertexMain", ShaderStages.Vertex), program.EntryPoint(0));
        Assert.Equal(new SlangEntryPointInfo("fragmentMain", ShaderStages.Fragment), program.EntryPoint(1));
    }

    /// <summary>
    /// <c>Spirv(i)</c> and <c>EntryPoint(i)</c> index the same entry point, and
    /// each index produces its own module — the failure a naive "generate
    /// once, reuse the blob" cache produces is two identical spans.
    /// </summary>
    [Fact]
    public void Compose_EntryPointIndex_MatchesSpirv()
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);
        using Composition composition = Composition.Load(session);

        using SlangProgram program = session.CreateProgram()
            .Add(composition.Common)
            .Add(composition.Geometry)
            .Add(composition.Material)
            .Add(composition.Vertex)
            .Add(composition.Fragment)
            .Link();

        ReadOnlySpan<uint> vertex = program.Spirv(0);
        ReadOnlySpan<uint> fragment = program.Spirv(1);

        Assert.Equal(ShaderFixtures.SpirvMagic, vertex[0]);
        Assert.Equal(ShaderFixtures.SpirvMagic, fragment[0]);
        Assert.NotEqual(vertex.Length, fragment.Length);
    }

    /// <summary>
    /// The ordering contract, asserted rather than assumed: the same five
    /// components in two orders produce two different programs.
    /// </summary>
    [Fact]
    public void Compose_OrderIsObservable()
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);
        using Composition composition = Composition.Load(session);

        using SlangProgram geometryFirst = session.CreateProgram()
            .Add(composition.Common)
            .Add(composition.Geometry)
            .Add(composition.Material)
            .Add(composition.Vertex)
            .Add(composition.Fragment)
            .Link();

        using SlangProgram materialFirst = session.CreateProgram()
            .Add(composition.Material)
            .Add(composition.Common)
            .Add(composition.Geometry)
            .Add(composition.Fragment)
            .Add(composition.Vertex)
            .Link();

        Assert.NotEqual(geometryFirst.EntryPoint(0).Name, materialFirst.EntryPoint(0).Name);
        Assert.Equal("vertexMain", geometryFirst.EntryPoint(0).Name);
        Assert.Equal("fragmentMain", materialFirst.EntryPoint(0).Name);
    }

    /// <summary>
    /// The acceptance criterion for "conformance, not <c>specialize</c>": an
    /// interface-typed <c>ParameterBlock</c> cannot generate code until some
    /// implementation is in the linkage, and
    /// <see cref="SlangProgramBuilder.AddTypeConformance"/> is how it gets
    /// there.
    /// </summary>
    [Fact]
    public void Compose_TypeConformance_Links()
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);
        using SlangModule module = session.LoadModuleFromSource(
            "surface", "surface.slang", ShaderFixtures.InterfaceSurfaceModule);
        using SlangEntryPoint fragment = module.DefinedEntryPoint(0);

        using (SlangProgram unconformed = session.CreateProgram().Add(module).Add(fragment).Link())
        {
            // Links fine. The refusal comes at code generation, which is why
            // a link-only test would not have caught this.
            var ex = Assert.Throws<SlangCompilationException>(() => unconformed.Spirv(0).Length);

            _output.WriteLine(ex.Diagnostics);
            Assert.Contains("no type conformances found", ex.Diagnostics, StringComparison.Ordinal);
        }

        using SlangProgram conformed = session.CreateProgram()
            .Add(module)
            .Add(fragment)
            .AddTypeConformance("Glossy", "ISurface")
            .Link();

        ReadOnlySpan<uint> spirv = conformed.Spirv(0);

        Assert.Equal(ShaderFixtures.SpirvMagic, spirv[0]);
    }

    [Fact]
    public void Compose_UnknownConformanceType_Throws()
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);
        using SlangModule module = session.LoadModuleFromSource(
            "unknownConformance", "unknownConformance.slang", ShaderFixtures.InterfaceSurfaceModule);
        using SlangEntryPoint fragment = module.DefinedEntryPoint(0);

        SlangProgramBuilder builder = session.CreateProgram()
            .Add(module)
            .Add(fragment)
            .AddTypeConformance("Nope", "ISurface");

        // Deferred resolution: the name is only looked up once there is a
        // composite to look it up against, so this surfaces from Link().
        var ex = Assert.Throws<ArgumentException>(() => builder.Link());

        Assert.Contains("Nope", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Compose_Empty_Throws()
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);

        Assert.Throws<InvalidOperationException>(() => session.CreateProgram().Link());
    }

    [Fact]
    public void Compose_LinkTwice_ProducesIndependentPrograms()
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);
        using Composition composition = Composition.Load(session);

        SlangProgramBuilder builder = session.CreateProgram()
            .Add(composition.Common)
            .Add(composition.Geometry)
            .Add(composition.Vertex);

        using SlangProgram first = builder.Link();
        using SlangProgram second = builder.Link();

        Assert.NotSame(first, second);
        Assert.Equal(first.EntryPoint(0).Name, second.EntryPoint(0).Name);

        // Disposing one must not invalidate the other.
        first.Dispose();

        Assert.Equal(ShaderFixtures.SpirvMagic, second.Spirv(0)[0]);
    }

    /// <summary>
    /// The three-module, two-entry-point composition every ordering test uses,
    /// loaded once so the tests read as assertions about composition rather
    /// than about module loading.
    /// </summary>
    private sealed class Composition : IDisposable
    {
        private Composition(
            SlangModule common,
            SlangModule geometry,
            SlangModule material,
            SlangEntryPoint vertex,
            SlangEntryPoint fragment)
        {
            Common = common;
            Geometry = geometry;
            Material = material;
            Vertex = vertex;
            Fragment = fragment;
        }

        public SlangModule Common { get; }

        public SlangModule Geometry { get; }

        public SlangModule Material { get; }

        public SlangEntryPoint Vertex { get; }

        public SlangEntryPoint Fragment { get; }

        public static Composition Load(SlangSession session)
        {
            SlangModule common = session.LoadModuleFromSource(
                "composeCommon", "composeCommon.slang", ShaderFixtures.ComposeCommonModule);
            SlangModule geometry = session.LoadModuleFromSource(
                "composeGeometry", "composeGeometry.slang", ShaderFixtures.ComposeGeometryModule);
            SlangModule material = session.LoadModuleFromSource(
                "composeMaterial", "composeMaterial.slang", ShaderFixtures.ComposeMaterialModule);

            return new Composition(
                common,
                geometry,
                material,
                geometry.DefinedEntryPoint(0),
                material.DefinedEntryPoint(0));
        }

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
