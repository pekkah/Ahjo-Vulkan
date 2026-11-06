using Ahjo.Vulkan.Ngx.Native;

namespace Ahjo.Vulkan.Ngx;

/// <summary>
/// The raw "why not" a feature's capability keys report, kept by
/// <see cref="NgxContext"/> for every feature so that an optional feature's
/// create call can throw the same diagnosed exception <see cref="NgxContext.Create"/>
/// would have thrown for a required one (spec D4).
/// </summary>
/// <param name="FeatureInitResult"><c>&lt;Feature&gt;.FeatureInitResult</c>.</param>
/// <param name="NeedsUpdatedDriver"><c>&lt;Feature&gt;.NeedsUpdatedDriver</c>, non-zero = true.</param>
/// <param name="MinDriverVersionMajor"><c>&lt;Feature&gt;.MinDriverVersionMajor</c>.</param>
/// <param name="MinDriverVersionMinor"><c>&lt;Feature&gt;.MinDriverVersionMinor</c>.</param>
internal readonly record struct FeatureDiagnosis(
    NVSDK_NGX_Result FeatureInitResult,
    uint NeedsUpdatedDriver,
    uint MinDriverVersionMajor,
    uint MinDriverVersionMinor);
