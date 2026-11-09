using Ahjo.Vulkan.Ngx.Native;
using Xunit;

namespace Ahjo.Vulkan.Ngx.Tests;

/// <summary>
/// <see cref="NgxContext.Create"/>'s throw matrix over
/// <see cref="NgxDescription.RequiredFeatures"/>, driven through the
/// device-free <c>NgxContext.ThrowIfRequiredUnavailable</c> projection.
/// </summary>
/// <remarks>
/// Needs no device, no driver and no shim: the projection is pure over the raw
/// capability-key values <c>Create</c> reads (spec D10, issue #253). The one
/// fact it cannot show — which key a missing <c>nvngx_dlssg.dll</c> actually
/// sets — was measured on hardware: <c>FrameGeneration.FeatureInitResult</c>
/// = <c>FAIL_FeatureNotFound</c>, the same key Super Resolution uses.
/// </remarks>
public sealed class NgxContextAvailabilityTests
{
    private static readonly FeatureDiagnosis Healthy =
        new(NVSDK_NGX_Result.NVSDK_NGX_Result_Success, 0, 0, 0);

    private static readonly FeatureDiagnosis NotFound =
        new(NVSDK_NGX_Result.NVSDK_NGX_Result_FAIL_FeatureNotFound, 0, 0, 0);

    private static readonly string[] Paths = ["C:/first/search", "C:/second/search"];

    [Fact]
    public void RequiredSuperResolution_FrameGenerationUnavailable_DoesNotThrow()
    {
        // The pekkah/ahjo#2783 criterion: hardware without FG runs unchanged.
        NgxContext.ThrowIfRequiredUnavailable(
            NgxFeatures.SuperResolution,
            srAvailable: true,  in Healthy,
            fgAvailable: false, in NotFound,
            Paths);
    }

    [Fact]
    public void RequiredFrameGeneration_LibraryNotFound_ThrowsNamingTheFileAndEveryDirectoryInOrder()
    {
        var ex = Assert.Throws<NgxFeatureLibraryNotFoundException>(() =>
            NgxContext.ThrowIfRequiredUnavailable(
                NgxFeatures.SuperResolution | NgxFeatures.FrameGeneration,
                srAvailable: true,  in Healthy,
                fgAvailable: false, in NotFound,
                Paths));

        Assert.Equal(NgxFeature.FrameGeneration, ex.Feature);
        Assert.Equal(NVSDK_NGX_Result.NVSDK_NGX_Result_FAIL_FeatureNotFound, ex.Result);
        Assert.Equal(
            OperatingSystem.IsWindows() ? "nvngx_dlssg.dll" : "libnvidia-ngx-dlssg.so.<version>",
            ex.ExpectedFile);
        Assert.Equal([.. Paths, AppContext.BaseDirectory], ex.SearchedDirectories);

        Assert.StartsWith(
            "DLSS Frame Generation is unavailable: the NVIDIA feature library was not found.\n",
            ex.Message, StringComparison.Ordinal);
        Assert.Contains("Expected file: " + ex.ExpectedFile + "\n", ex.Message, StringComparison.Ordinal);
        foreach (string directory in ex.SearchedDirectories)
            Assert.Contains("\n  " + directory + "\n", ex.Message, StringComparison.Ordinal);
        Assert.Contains("docs/ngx-frame-generation-notes.md", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RequiredFrameGeneration_DriverTooOld_ThrowsWithTheMinimum()
    {
        var tooOld = new FeatureDiagnosis(NVSDK_NGX_Result.NVSDK_NGX_Result_FAIL_FeatureNotSupported, 1, 600, 10);

        var ex = Assert.Throws<NgxDriverTooOldException>(() =>
            NgxContext.ThrowIfRequiredUnavailable(
                NgxFeatures.FrameGeneration,
                srAvailable: true,  in Healthy,
                fgAvailable: false, in tooOld,
                searchPaths: null));

        Assert.Equal(600u, ex.MinimumDriverVersionMajor);
        Assert.Equal(10u, ex.MinimumDriverVersionMinor);
        Assert.Contains("DLSS Frame Generation", ex.Message, StringComparison.Ordinal);
        Assert.Contains("600.10", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RequiredFrameGeneration_UnavailableForNoReason_ThrowsNamingTheFeature()
    {
        var ex = Assert.Throws<NgxException>(() =>
            NgxContext.ThrowIfRequiredUnavailable(
                NgxFeatures.FrameGeneration,
                srAvailable: true,  in Healthy,
                // Available 0, FeatureInitResult Success, everything else 0:
                // NGX said "no" and recorded no reason.
                fgAvailable: false, in Healthy,
                searchPaths: null));

        // The base type exactly: neither diagnosed subtype applies.
        Assert.Equal(typeof(NgxException), ex.GetType());
        Assert.Equal(NVSDK_NGX_Result.NVSDK_NGX_Result_FAIL_FeatureNotSupported, ex.Result);
        Assert.Contains("DLSS Frame Generation", ex.Message, StringComparison.Ordinal);
        Assert.Contains("FrameGeneration.Available is 0", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(NgxFeatures.FrameGeneration, NgxFeature.FrameGeneration)]
    [InlineData(NgxFeatures.SuperResolution, NgxFeature.SuperResolution)]
    [InlineData(NgxFeatures.SuperResolution | NgxFeatures.FrameGeneration, NgxFeature.SuperResolution)]
    public void InitFeatureNotFound_NamesTheFirstRequiredFeature(NgxFeatures required, NgxFeature expected)
    {
        var ex = Assert.Throws<NgxFeatureLibraryNotFoundException>(() =>
            NgxContext.ThrowInitFailed(NVSDK_NGX_Result.NVSDK_NGX_Result_FAIL_FeatureNotFound, required, Paths));

        Assert.Equal(expected, ex.Feature);
        Assert.Equal(
            expected == NgxFeature.FrameGeneration
                ? (OperatingSystem.IsWindows() ? "nvngx_dlssg.dll" : "libnvidia-ngx-dlssg.so.<version>")
                : (OperatingSystem.IsWindows() ? "nvngx_dlss.dll" : "libnvidia-ngx-dlss.so.<version>"),
            ex.ExpectedFile);
        Assert.Equal([.. Paths, AppContext.BaseDirectory], ex.SearchedDirectories);
    }

    [Fact]
    public void RequiredSuperResolution_LibraryNotFound_KeepsTheExistingMessageShape()
    {
        var ex = Assert.Throws<NgxFeatureLibraryNotFoundException>(() =>
            NgxContext.ThrowIfRequiredUnavailable(
                NgxFeatures.SuperResolution,
                srAvailable: false, in NotFound,
                fgAvailable: true,  in Healthy,
                Paths));

        Assert.Equal(NgxFeature.SuperResolution, ex.Feature);
        Assert.Equal(
            OperatingSystem.IsWindows() ? "nvngx_dlss.dll" : "libnvidia-ngx-dlss.so.<version>",
            ex.ExpectedFile);
        Assert.Equal([.. Paths, AppContext.BaseDirectory], ex.SearchedDirectories);

        // The regression guard: the pre-#253 message, byte for byte.
        string expected =
            "DLSS is unavailable: the NVIDIA feature library was not found.\n" +
            "Expected file: " + ex.ExpectedFile + "\n" +
            "Searched:\n" +
            "  " + Paths[0] + "\n" +
            "  " + Paths[1] + "\n" +
            "  " + AppContext.BaseDirectory + "\n" +
            "This library is NOT shipped by Ahjo.Vulkan.Ngx — the application supplies it from NVIDIA's DLSS SDK " +
            "(https://github.com/NVIDIA/DLSS, lib/<plat>/rel/). See docs/ngx-notes.md.";
        Assert.Equal(expected, ex.Message);
    }
}
