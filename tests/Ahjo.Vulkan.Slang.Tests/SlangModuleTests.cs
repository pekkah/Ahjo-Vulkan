using Xunit;

namespace Ahjo.Vulkan.Slang.Tests;

/// <summary>
/// The composition surface Phase 3a builds on: modules loaded by name, entry
/// points found on them, and the module registry that makes
/// <c>import</c> work with no file system present.
/// </summary>
public sealed class SlangModuleTests
{
    /// <summary>
    /// A module loaded from a string is registered in the session under its
    /// name, so a later module's <c>import &lt;name&gt;;</c> resolves — which
    /// is what lets a material system assemble a program out of generated
    /// source with nothing on disk.
    /// </summary>
    [Fact]
    public void LoadModuleFromSource_TwoModules_SecondImportsFirst()
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);

        using SlangModule common = session.LoadModuleFromSource(
            "common", "common.slang", ShaderFixtures.CommonModule);

        Assert.Equal("common", common.Name);
        Assert.Null(common.Warnings);

        using SlangModule material = session.LoadModuleFromSource(
            "material", "material.slang", ShaderFixtures.ImportsCommonModule);

        Assert.Equal("material", material.Name);
        Assert.Null(material.Warnings);
        Assert.Equal(1, material.DefinedEntryPointCount);
    }

    [Fact]
    public void DefinedEntryPoints_Enumerate()
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);
        using SlangModule module = session.LoadModuleFromSource(
            "defined", "defined.slang", ShaderFixtures.VertexAndFragment);

        Assert.Equal(2, module.DefinedEntryPointCount);

        using SlangEntryPoint vertex = module.DefinedEntryPoint(0);
        using SlangEntryPoint fragment = module.DefinedEntryPoint(1);

        Assert.Equal("vertexMain", vertex.Name);
        Assert.Equal(ShaderStages.Vertex, vertex.Stage);
        Assert.Equal("fragmentMain", fragment.Name);
        Assert.Equal(ShaderStages.Fragment, fragment.Stage);
    }

    [Fact]
    public void FindEntryPoint_ByNameAndStage_Succeeds()
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);
        using SlangModule module = session.LoadModuleFromSource(
            "find", "find.slang", ShaderFixtures.VertexAndFragment);

        using SlangEntryPoint fragment = module.FindEntryPoint("fragmentMain", ShaderStages.Fragment);

        Assert.Equal("fragmentMain", fragment.Name);
        Assert.Equal(ShaderStages.Fragment, fragment.Stage);
    }

    /// <summary>
    /// <b>Deviation from the plan's §2.5 case 12, recorded rather than
    /// hidden.</b> The plan expected asking for a <c>[shader("fragment")]</c>
    /// entry point as <see cref="ShaderStages.Vertex"/> to throw with compiler
    /// text. It does not: measured on <c>v2026.14.1</c>,
    /// <c>IModule::findAndCheckEntryPoint</c> returns <c>SLANG_OK</c>, hands
    /// back the fragment entry point, and produces an <em>empty</em>
    /// diagnostics blob for every wrong stage tried (Compute and Vertex both).
    /// Slang uses the stage to <em>find</em> an unattributed function, not to
    /// validate an attributed one.
    /// </summary>
    /// <remarks>
    /// This test pins the measured behaviour so the wrapper's documented
    /// contract — <see cref="SlangEntryPoint.Stage"/> is the stage the caller
    /// asked for, not one Slang verified — cannot rot silently. If a future
    /// Slang starts rejecting the mismatch, this test goes red and the
    /// contract gets revisited on purpose.
    /// </remarks>
    [Fact]
    public void FindEntryPoint_WrongStage_IsNotRejectedBySlang()
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);
        using SlangModule module = session.LoadModuleFromSource(
            "wrongStage", "wrongStage.slang", ShaderFixtures.VertexAndFragment);

        using SlangEntryPoint mislabelled = module.FindEntryPoint("fragmentMain", ShaderStages.Vertex);

        Assert.Equal("fragmentMain", mislabelled.Name);
        Assert.Equal(ShaderStages.Vertex, mislabelled.Stage);
    }

    [Fact]
    public void FindEntryPoint_UnknownName_ThrowsWithCompilerText()
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);
        using SlangModule module = session.LoadModuleFromSource(
            "unknownName", "unknownName.slang", ShaderFixtures.VertexAndFragment);

        var ex = Assert.Throws<SlangCompilationException>(
            () => module.FindEntryPoint("noSuchFunction", ShaderStages.Vertex));

        Assert.Contains("noSuchFunction", ex.Diagnostics, StringComparison.Ordinal);
    }

    [Fact]
    public void FindEntryPoint_UnmappableStage_ThrowsNotSupported()
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);
        using SlangModule module = session.LoadModuleFromSource(
            "unmappable", "unmappable.slang", ShaderFixtures.VertexAndFragment);

        // AllGraphics is a mask, not a stage. Passing it through as
        // SLANG_STAGE_NONE would make Slang miss with a confusing message.
        Assert.Throws<NotSupportedException>(
            () => module.FindEntryPoint("vertexMain", ShaderStages.AllGraphics));
    }

    [Fact]
    public void LoadModule_UnknownName_ThrowsWithCompilerText()
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);

        var ex = Assert.Throws<SlangCompilationException>(() => session.LoadModule("no-such-module-anywhere"));

        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }
}
