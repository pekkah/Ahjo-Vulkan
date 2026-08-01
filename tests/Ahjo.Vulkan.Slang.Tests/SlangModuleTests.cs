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
    /// Asking for a <c>[shader("fragment")]</c> entry point as
    /// <see cref="ShaderStages.Vertex"/> throws, naming both stages.
    /// </summary>
    /// <remarks>
    /// <para>Slang does not reject this. Measured on <c>v2026.14.1</c>,
    /// <c>IModule::findAndCheckEntryPoint("fragmentMain",
    /// SLANG_STAGE_VERTEX)</c> returns <c>SLANG_OK</c> with an <em>empty</em>
    /// diagnostics blob and hands back the fragment entry point labelled
    /// <c>Vertex</c> — a mislabelled component that composes and links, and
    /// only surfaces as a pipeline-creation failure much later. The stage
    /// parameter is how Slang <em>finds</em> a function with no attribute; it
    /// is not a check on one that has an attribute.</para>
    /// <para>The wrapper closes that by reading the declared stage back first.
    /// The narrowness is deliberate: see
    /// <see cref="FindEntryPoint_UndeclaredStage_UsesTheRequestedStage"/> for
    /// the case that must keep working.</para>
    /// </remarks>
    [Fact]
    public void FindEntryPoint_DeclaredStageDisagrees_Throws()
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);
        using SlangModule module = session.LoadModuleFromSource(
            "wrongStage", "wrongStage.slang", ShaderFixtures.VertexAndFragment);

        var ex = Assert.Throws<SlangCompilationException>(
            () => module.FindEntryPoint("fragmentMain", ShaderStages.Vertex));

        Assert.Contains("fragmentMain", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Fragment", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Vertex", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A function with no <c>[shader("…")]</c> attribute has no declared stage
    /// to disagree with, so the requested stage stays authoritative and the
    /// lookup succeeds — the behaviour
    /// <see cref="FindEntryPoint_DeclaredStageDisagrees_Throws"/> must not
    /// break.
    /// </summary>
    [Fact]
    public void FindEntryPoint_UndeclaredStage_UsesTheRequestedStage()
    {
        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);
        using SlangModule module = session.LoadModuleFromSource(
            "unattributed", "unattributed.slang", ShaderFixtures.UnattributedVertex);

        // No attribute anywhere in the source: nothing is a "defined" entry
        // point, and the stage argument is the only thing that makes this
        // function findable at all.
        Assert.Equal(0, module.DefinedEntryPointCount);

        using SlangEntryPoint entryPoint = module.FindEntryPoint("unattributedMain", ShaderStages.Vertex);

        Assert.Equal("unattributedMain", entryPoint.Name);
        Assert.Equal(ShaderStages.Vertex, entryPoint.Stage);
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
