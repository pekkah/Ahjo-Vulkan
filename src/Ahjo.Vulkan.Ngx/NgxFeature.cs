using System.Runtime.InteropServices;
using Ahjo.Vulkan.Ngx.Native;

namespace Ahjo.Vulkan.Ngx;

/// <summary>
/// One NGX feature, for the APIs that ask about a single feature at a time —
/// the <see cref="NgxSupport"/> queries and
/// <see cref="NgxFeatureLibraryNotFoundException.Feature"/>.
/// </summary>
/// <remarks>
/// Wrapper-owned, not a shadow of <c>NVSDK_NGX_Feature</c>: NGX numbers its
/// features sparsely (<c>SuperSampling = 1</c>, <c>FrameGeneration = 11</c>)
/// and exposes far more of them than this package wraps.
/// </remarks>
public enum NgxFeature
{
    /// <summary>DLSS Super Resolution and DLAA (<c>nvngx_dlss.dll</c>).</summary>
    SuperResolution = 0,

    /// <summary>DLSS Frame Generation (<c>nvngx_dlssg.dll</c>).</summary>
    FrameGeneration = 1,
}

/// <summary>
/// A set of NGX features — what <see cref="NgxDescription.RequiredFeatures"/>
/// makes <see cref="NgxContext.Create"/> insist on.
/// </summary>
[Flags]
public enum NgxFeatures : uint
{
    /// <summary>No feature. Not a valid
    /// <see cref="NgxDescription.RequiredFeatures"/>.</summary>
    None = 0,

    /// <summary>DLSS Super Resolution and DLAA.</summary>
    SuperResolution = 1u << 0,

    /// <summary>DLSS Frame Generation.</summary>
    FrameGeneration = 1u << 1,
}

/// <summary>
/// <see cref="NgxFeature"/> to the native feature id, the feature library's
/// file name and the name used in messages.
/// </summary>
internal static class NgxFeatureMap
{
    /// <summary>Every defined <see cref="NgxFeatures"/> bit.</summary>
    internal const NgxFeatures AllFeatures = NgxFeatures.SuperResolution | NgxFeatures.FrameGeneration;

    /// <exception cref="ArgumentOutOfRangeException"><paramref name="feature"/>
    /// is not a defined <see cref="NgxFeature"/>.</exception>
    internal static NVSDK_NGX_Feature ToNative(NgxFeature feature) => feature switch
    {
        NgxFeature.SuperResolution => NVSDK_NGX_Feature.NVSDK_NGX_Feature_SuperSampling,
        NgxFeature.FrameGeneration => NVSDK_NGX_Feature.NVSDK_NGX_Feature_FrameGeneration,
        _ => throw new ArgumentOutOfRangeException(nameof(feature), feature, "Not an NgxFeature."),
    };

    /// <summary>
    /// The file NGX looks for: <c>nvngx_dlss.dll</c> / <c>nvngx_dlssg.dll</c>
    /// on Windows, <c>libnvidia-ngx-dlss.so.&lt;version&gt;</c> /
    /// <c>libnvidia-ngx-dlssg.so.&lt;version&gt;</c> on Linux.
    /// </summary>
    internal static string ExpectedLibraryFile(NgxFeature feature)
    {
        bool windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        return feature switch
        {
            NgxFeature.SuperResolution => windows ? "nvngx_dlss.dll"  : "libnvidia-ngx-dlss.so.<version>",
            NgxFeature.FrameGeneration => windows ? "nvngx_dlssg.dll" : "libnvidia-ngx-dlssg.so.<version>",
            _ => throw new ArgumentOutOfRangeException(nameof(feature), feature, "Not an NgxFeature."),
        };
    }

    /// <summary>The feature's name in exception messages.</summary>
    internal static string DisplayName(NgxFeature feature) => feature switch
    {
        NgxFeature.SuperResolution => "DLSS Super Resolution",
        NgxFeature.FrameGeneration => "DLSS Frame Generation",
        _ => throw new ArgumentOutOfRangeException(nameof(feature), feature, "Not an NgxFeature."),
    };
}
