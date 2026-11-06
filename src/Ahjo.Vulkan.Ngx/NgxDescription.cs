using Ahjo.Vulkan.Ngx.Native;

namespace Ahjo.Vulkan.Ngx;

/// <summary>
/// Who is calling NGX, and where it may look for the feature library. Passed to
/// <see cref="NgxContext.Create"/> and to every <see cref="NgxSupport"/> query;
/// the same value should be used for all of them.
/// </summary>
/// <remarks>
/// <para>Carries no pointers, so it is a <c>readonly record struct</c>.
/// Everything here is consumed at setup time and copied by the shim before the
/// call returns, so nothing needs to outlive it.</para>
/// </remarks>
public readonly record struct NgxDescription
{
    /// <summary>
    /// Creates a description with every member at its default. Explicit so
    /// that <c>new NgxDescription()</c> runs the property initializers —
    /// <see cref="RequiredFeatures"/> defaults to
    /// <see cref="NgxFeatures.SuperResolution"/>, not to
    /// <see cref="NgxFeatures.None"/> (CS8983, the #119 pattern).
    /// </summary>
    public NgxDescription()
    {
        // The two required strings keep the default they always had — unset —
        // and Validate rejects them with a message naming the property. The
        // explicit constructor only makes the compiler ask the question.
        ProjectId     = null!;
        EngineVersion = null!;
    }

    /// <summary>
    /// Your application's NVIDIA project ID: a GUID-shaped string, e.g.
    /// <c>"a0f57b54-1daf-4934-90ae-c4035c19df04"</c>. Required. NGX parses it
    /// (DLSS Programming Guide §5.2.1), so a malformed value reaches a
    /// <c>strlen</c> plus a GUID parse inside the SDK rather than a clean
    /// rejection — which is why <see cref="NgxContext.Create"/> validates it
    /// unconditionally rather than under <see cref="AhjoValidation.Enabled"/>.
    /// </summary>
    public string ProjectId { get; init; }

    /// <summary>Your engine's version string. Required, free-form.</summary>
    public string EngineVersion { get; init; }

    /// <summary>
    /// Directory NGX may write its own data and logs into. Must be writable.
    /// <see langword="null"/> — the default — uses the process's temporary
    /// directory.
    /// </summary>
    /// <remarks>
    /// <b>The wrapper substitutes the temp path; it never hands NGX a null.</b>
    /// The SDK documents this field as optional, but
    /// <c>NVSDK_NGX_VULKAN_GetFeatureRequirements</c> dereferences it
    /// unconditionally: a null produces an access violation inside NVIDIA's
    /// client library, not a failure result — measured on an RTX 4070 Ti,
    /// driver 610.47 (issue #218). Since no managed <c>catch</c> can recover
    /// from that, the default is materialized here rather than trusted to the
    /// SDK.
    /// </remarks>
    public string? ApplicationDataPath { get; init; }

    /// <summary>
    /// <see cref="ApplicationDataPath"/> with the null case resolved. Cached,
    /// so repeated queries do not re-derive the temp path.
    /// </summary>
    internal string EffectiveApplicationDataPath => ApplicationDataPath ?? s_defaultApplicationDataPath;

    private static readonly string s_defaultApplicationDataPath = Path.GetTempPath();

    /// <summary>
    /// Extra directories to search for the NGX feature libraries, on top of the
    /// application directory. NGX searches this one list for <b>every</b>
    /// feature DLL — <c>nvngx_dlss.dll</c> (Super Resolution) and
    /// <c>nvngx_dlssg.dll</c> (Frame Generation) alike. This is how you point
    /// NGX at feature libraries that do not sit beside your executable.
    /// <see langword="null"/> = search the application directory only.
    /// </summary>
    /// <remarks>
    /// Entries are listed verbatim in
    /// <see cref="NgxFeatureLibraryNotFoundException"/>'s message, so a
    /// deployment mistake names itself.
    /// </remarks>
    public IReadOnlyList<string>? DlssSearchPaths { get; init; }

    /// <summary>
    /// The features <see cref="NgxContext.Create"/> requires. Defaults to
    /// <see cref="NgxFeatures.SuperResolution"/>.
    /// </summary>
    /// <remarks>
    /// <para><see cref="NgxContext.Create"/> probes every feature, but throws
    /// its diagnosed exception only for an unavailable feature listed here. An
    /// unavailable feature that is <i>not</i> listed leaves the context usable
    /// and reports itself through <see cref="NgxContext.IsSuperSamplingAvailable"/>
    /// / <see cref="NgxContext.IsFrameGenerationAvailable"/>; the matching
    /// create call then throws the same diagnosed exception.</para>
    /// <para>So a renderer that offers Frame Generation only where it works
    /// asks for <see cref="NgxFeatures.SuperResolution"/> and reads
    /// <see cref="NgxContext.IsFrameGenerationAvailable"/>; one that uses only
    /// Frame Generation asks for <see cref="NgxFeatures.FrameGeneration"/> and
    /// does not need <c>nvngx_dlss.dll</c> at all.</para>
    /// <para><see cref="NgxFeatures.None"/> and undefined bits are rejected.</para>
    /// </remarks>
    public NgxFeatures RequiredFeatures { get; init; } = NgxFeatures.SuperResolution;

    /// <summary>
    /// How much NGX logs. Defaults to <see cref="NgxLoggingLevel.Off"/>, which
    /// installs no callback at all.
    /// </summary>
    public NgxLoggingLevel LoggingLevel { get; init; }

    /// <summary>
    /// Ask NGX to suppress its other logging sinks (its own files, the debug
    /// output window) and speak only through the callback. Has no effect at
    /// <see cref="NgxLoggingLevel.Off"/>.
    /// </summary>
    public bool DisableOtherLoggingSinks { get; init; }

    /// <summary>
    /// Throws <see cref="ArgumentException"/> for a description NGX would
    /// mishandle rather than reject.
    /// </summary>
    /// <remarks>
    /// Deliberately <b>not</b> gated on <see cref="AhjoValidation.Enabled"/>:
    /// this is a setup-time call where allocation is free, and the failure it
    /// prevents happens inside NVIDIA's SDK where there is nothing to inspect.
    /// </remarks>
    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProjectId))
        {
            throw new ArgumentException(
                "NgxDescription.ProjectId is required and must be a GUID-like string (DLSS Programming Guide §5.2.1), " +
                "e.g. \"a0f57b54-1daf-4934-90ae-c4035c19df04\". Generate one per application and keep it stable.",
                nameof(ProjectId));
        }

        if (!Guid.TryParse(ProjectId, out _))
        {
            throw new ArgumentException(
                $"NgxDescription.ProjectId is \"{ProjectId}\", which is not a GUID. NGX parses this value " +
                "(DLSS Programming Guide §5.2.1); pass a GUID-shaped string such as \"a0f57b54-1daf-4934-90ae-c4035c19df04\".",
                nameof(ProjectId));
        }

        if (string.IsNullOrWhiteSpace(EngineVersion))
        {
            throw new ArgumentException(
                "NgxDescription.EngineVersion is required. Any non-empty version string will do; NGX reports it with telemetry.",
                nameof(EngineVersion));
        }

        if (RequiredFeatures == NgxFeatures.None || (RequiredFeatures & ~NgxFeatureMap.AllFeatures) != 0)
        {
            throw new ArgumentException(
                $"NgxDescription.RequiredFeatures is {(uint)RequiredFeatures}; it must be a non-empty combination of " +
                "NgxFeatures.SuperResolution and NgxFeatures.FrameGeneration.",
                nameof(RequiredFeatures));
        }

        IReadOnlyList<string>? paths = DlssSearchPaths;
        if (paths is null) return;

        for (int i = 0; i < paths.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(paths[i]))
            {
                throw new ArgumentException(
                    $"NgxDescription.DlssSearchPaths[{i}] is null or whitespace. Every entry must be a real directory path — " +
                    "an empty one would be handed to NGX as an empty C string.",
                    nameof(DlssSearchPaths));
            }
        }
    }

    /// <summary>
    /// Encodes every string into <paramref name="block"/> and returns the
    /// <c>AhjoNgxInitInfo</c> the shim expects. The returned struct is only
    /// valid while <paramref name="block"/> is alive.
    /// </summary>
    internal unsafe AhjoNgxInitInfo ToNative(ref NgxUtf8Block block)
    {
        sbyte** searchPaths = block.AddArray(DlssSearchPaths, out uint searchPathCount);

        return new AhjoNgxInitInfo
        {
            // The shim rejects a mismatch with FAIL_InvalidParameter (#216 D2);
            // sizeof() rather than a literal so a binding regen cannot drift.
            StructSize          = (uint)sizeof(AhjoNgxInitInfo),
            IdentifierType      = NVSDK_NGX_Application_Identifier_Type.NVSDK_NGX_Application_Identifier_Type_Project_Id,
            ApplicationId       = 0,
            ProjectId           = block.Add(ProjectId),
            EngineType          = NVSDK_NGX_EngineType.NVSDK_NGX_ENGINE_TYPE_CUSTOM,
            EngineVersion       = block.Add(EngineVersion),
            ApplicationDataPath = block.Add(EffectiveApplicationDataPath),
            FeatureSearchPaths      = searchPaths,
            FeatureSearchPathCount  = searchPathCount,
            // No callback at all when logging is off, so NGX has no path back
            // into managed code.
            LogCallback              = LoggingLevel == NgxLoggingLevel.Off ? null : &NgxContext.LogThunk,
            MinimumLoggingLevel      = (NVSDK_NGX_Logging_Level)LoggingLevel,
            DisableOtherLoggingSinks = (byte)(DisableOtherLoggingSinks ? 1 : 0),
        };
    }

    /// <summary>
    /// Bytes of UTF-8 the block must hold for <see cref="ToNative"/>, and how
    /// many pointer slots the search-path array needs.
    /// </summary>
    internal void MeasureUtf8(out int byteCapacity, out int stringCapacity)
    {
        // +1 per string for the terminator ToNative writes explicitly.
        int bytes = Measure(ProjectId) + Measure(EngineVersion) + Measure(EffectiveApplicationDataPath);

        IReadOnlyList<string>? paths = DlssSearchPaths;
        int count = paths?.Count ?? 0;
        for (int i = 0; i < count; i++)
            bytes += Measure(paths![i]);

        byteCapacity   = bytes;
        stringCapacity = count;

        static int Measure(string? value)
            => value is null ? 0 : System.Text.Encoding.UTF8.GetMaxByteCount(value.Length) + 1;
    }
}
