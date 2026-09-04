using Ahjo.Vulkan.Ngx.Native;

namespace Ahjo.Vulkan.Ngx;

/// <summary>
/// A non-success <see cref="NVSDK_NGX_Result"/> from NGX.
/// </summary>
/// <remarks>
/// Distinct from <see cref="AhjoValidationException"/> (a wrapper-contract
/// violation — you called the API wrong) and from <c>VulkanException</c> (a
/// non-success <c>VkResult</c> from the Vulkan driver). This one means NGX
/// itself refused, and <see cref="Result"/> carries its verdict.
/// <para><see cref="Result"/> values worth branching on:
/// <c>FAIL_RWFlagMissing</c>, <c>FAIL_UnsupportedInputFormat</c> and
/// <c>FAIL_MissingInput</c> are what an image-layout or usage mistake on the
/// evaluate path produces — see <see cref="DlssEvaluateInputs"/> for the layout
/// contract the wrapper cannot check.</para>
/// </remarks>
public class NgxException : Exception
{
    /// <summary>The NGX result that produced this exception.</summary>
    public NVSDK_NGX_Result Result { get; }

    public NgxException(NVSDK_NGX_Result result, string message) : base(message)
        => Result = result;
}

/// <summary>
/// DLSS is unavailable because NVIDIA's feature library
/// (<c>nvngx_dlss.dll</c> / <c>libnvidia-ngx-dlss.so.&lt;version&gt;</c>) was not
/// found. That file is <b>not</b> shipped by <c>Ahjo.Vulkan.Ngx</c> — the
/// application supplies it from NVIDIA's DLSS SDK (issue #214).
/// </summary>
/// <remarks>
/// The message lists every directory that was searched. This is deliberately a
/// distinct type: it is the one NGX failure with a purely deployment-shaped
/// fix, and the wrapper never falls back silently when it happens.
/// </remarks>
public sealed class NgxFeatureLibraryNotFoundException : NgxException
{
    public NgxFeatureLibraryNotFoundException(NVSDK_NGX_Result result, string message)
        : base(result, message)
    {
    }
}

/// <summary>
/// DLSS is unavailable because the installed NVIDIA driver is older than the
/// minimum the feature reports. <see cref="MinimumDriverVersionMajor"/> /
/// <see cref="MinimumDriverVersionMinor"/> come from NGX's capability parameter
/// map.
/// </summary>
public sealed class NgxDriverTooOldException : NgxException
{
    public uint MinimumDriverVersionMajor { get; }
    public uint MinimumDriverVersionMinor { get; }

    public NgxDriverTooOldException(NVSDK_NGX_Result result, string message, uint major, uint minor)
        : base(result, message)
    {
        MinimumDriverVersionMajor = major;
        MinimumDriverVersionMinor = minor;
    }
}
