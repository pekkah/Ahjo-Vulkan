namespace Ahjo.Vulkan.Ngx;

/// <summary>
/// Why a feature is or is not supported on this host. Shadow of
/// <c>NVSDK_NGX_Feature_Support_Result</c>, carried by
/// <see cref="DlssRequirements.Reason"/>.
/// </summary>
/// <remarks>
/// <c>[Flags]</c> because the native values are disjoint bits and NGX may
/// report more than one reason at once. <see cref="Supported"/> is zero — the
/// absence of every reason — so test it with equality, not
/// <c>HasFlag</c>.
/// </remarks>
[Flags]
public enum NgxFeatureSupport : uint
{
    /// <summary>No reason to refuse: the feature is supported.</summary>
    Supported = 0,

    /// <summary>NGX could not run the support check at all.</summary>
    CheckNotPresent = 1,

    /// <summary>The installed NVIDIA driver is older than the feature requires.</summary>
    DriverVersionUnsupported = 2,

    /// <summary>This GPU cannot run the feature.</summary>
    AdapterUnsupported = 4,

    /// <summary>The OS is older than
    /// <see cref="DlssRequirements.MinimumOsVersion"/>.</summary>
    OsVersionBelowMinimum = 8,

    /// <summary>The feature is not implemented on this platform.</summary>
    NotImplemented = 16,
}
