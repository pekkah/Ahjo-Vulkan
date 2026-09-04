using Ahjo.Vulkan.Ngx.Native;

namespace Ahjo.Vulkan.Ngx;

/// <summary>
/// What NGX says this host needs in order to run DLSS, and whether it has it.
/// Projection of <c>NVSDK_NGX_FeatureRequirement</c>, from
/// <see cref="NgxSupport.TryGetSuperSamplingRequirements"/>.
/// </summary>
/// <remarks>
/// Useful for a settings screen that wants to say <i>why</i> DLSS is greyed
/// out rather than just greying it out. <see cref="Reason"/> is
/// <see cref="NgxFeatureSupport.Supported"/> (zero) exactly when
/// <see cref="IsSupported"/> is <see langword="true"/>.
/// </remarks>
public readonly record struct DlssRequirements
{
    /// <summary>Whether DLSS is supported on this adapter, driver and OS.</summary>
    public bool IsSupported { get; init; }

    /// <summary>Why not, when <see cref="IsSupported"/> is
    /// <see langword="false"/>. May carry more than one bit.</summary>
    public NgxFeatureSupport Reason { get; init; }

    /// <summary>Minimum NVIDIA hardware architecture, as NGX's own opaque
    /// number. Compare against another host's value; do not decode it.</summary>
    public uint MinimumArchitecture { get; init; }

    /// <summary>Minimum OS version, as the SDK's own string. Empty when NGX
    /// reported none.</summary>
    public string MinimumOsVersion { get; init; }

    /// <summary>Projects the native struct. The <c>MinOSVersion</c> field is a
    /// <c>char[255]</c> inline array read as UTF-8 up to its first NUL.</summary>
    internal static unsafe DlssRequirements FromNative(in NVSDK_NGX_FeatureRequirement requirement)
    {
        const int MinOsVersionCapacity = 255;

        string osVersion;
        fixed (NVSDK_NGX_FeatureRequirement* p = &requirement)
            osVersion = NgxUtf8.ToString(new ReadOnlySpan<byte>(&p->MinOSVersion, MinOsVersionCapacity), MinOsVersionCapacity);

        var reason = (NgxFeatureSupport)requirement.FeatureSupported;
        return new DlssRequirements
        {
            IsSupported         = reason == NgxFeatureSupport.Supported,
            Reason              = reason,
            MinimumArchitecture = requirement.MinHWArchitecture,
            MinimumOsVersion    = osVersion,
        };
    }
}
