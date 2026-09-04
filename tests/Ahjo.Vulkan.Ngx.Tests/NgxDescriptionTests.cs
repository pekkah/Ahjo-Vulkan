using Xunit;

namespace Ahjo.Vulkan.Ngx.Tests;

/// <summary>
/// <see cref="NgxDescription"/>'s validation is deliberately not gated on
/// <see cref="AhjoValidation.Enabled"/>: a malformed project ID reaches a
/// <c>strlen</c> and a GUID parse inside NVIDIA's SDK, where there is nothing
/// to inspect and no useful diagnostic. These tests hold that line.
/// </summary>
/// <remarks>Needs no device, no driver and no shim — the queries validate
/// before they touch NGX.</remarks>
public sealed class NgxDescriptionTests
{
    private static NgxDescription Valid => new()
    {
        ProjectId     = "8d19b5b3-8f7d-4a2f-9f6e-6c2d9a1f4e73",
        EngineVersion = "1.0.0",
    };

    [Fact]
    public void WellFormedDescription_Validates()
    {
        // No assertion beyond "does not throw" — that is the whole contract.
        Valid.Validate();
    }

    [Fact]
    public void NullProjectId_Throws()
    {
        var description = Valid with { ProjectId = null! };
        ArgumentException ex = AssertThrowsOnValidate(description);
        Assert.Contains("ProjectId", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WhitespaceProjectId_Throws()
    {
        var description = Valid with { ProjectId = "   " };
        ArgumentException ex = AssertThrowsOnValidate(description);
        Assert.Contains("ProjectId", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NonGuidProjectId_Throws_AndQuotesTheOffendingValue()
    {
        var description = Valid with { ProjectId = "not-a-guid" };
        ArgumentException ex = AssertThrowsOnValidate(description);
        Assert.Contains("not-a-guid", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NullEngineVersion_Throws()
    {
        var description = Valid with { EngineVersion = null! };
        ArgumentException ex = AssertThrowsOnValidate(description);
        Assert.Contains("EngineVersion", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NullSearchPathEntry_Throws_AndNamesTheIndex()
    {
        var description = Valid with { DlssSearchPaths = ["C:/first", null!, "C:/third"] };
        ArgumentException ex = AssertThrowsOnValidate(description);
        Assert.Contains("DlssSearchPaths[1]", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WhitespaceSearchPathEntry_Throws_AndNamesTheIndex()
    {
        var description = Valid with { DlssSearchPaths = ["  "] };
        ArgumentException ex = AssertThrowsOnValidate(description);
        Assert.Contains("DlssSearchPaths[0]", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Drives <c>NgxDescription.Validate</c> directly. Every public entry point
    /// — <see cref="NgxContext.Create"/> and all four
    /// <see cref="NgxSupport"/> queries — runs it before touching NGX, so this
    /// covers all of them without needing the shim staged.
    /// </summary>
    private static ArgumentException AssertThrowsOnValidate(NgxDescription description)
        => Assert.Throws<ArgumentException>(description.Validate);
}
