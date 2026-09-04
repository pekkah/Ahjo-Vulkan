namespace Ahjo.Vulkan.Ngx;

/// <summary>
/// What DLSS reports about itself. From <see cref="NgxContext.TryGetStats"/>,
/// after at least one <see cref="DlssFeature"/> exists.
/// </summary>
/// <remarks>
/// <para><see cref="VramAllocatedBytes"/> is the figure VMA cannot see: DLSS
/// allocates its history and scratch surfaces inside the driver, outside any
/// allocator this wrapper drives (issue #214). Pair it with
/// <see cref="Allocator.GetHeapBudgets"/> to account for both halves.</para>
/// <para><b>How the other two are reachable.</b> NVIDIA's own
/// <c>NGX_DLSS_GET_STATS_2</c> reads <c>OptLevel</c> and
/// <c>IsDevSnippetBranch</c> through the <c>NVSDK_NGX_EParameter_*</c> hash
/// aliases (<c>nvsdk_ngx_helpers.h:42-43</c>) — the 74-macro family excluded
/// from the bindings on purpose, because their values embed raw control bytes
/// (#216 D7/E7). Whether NGX's parameter map treats the plain string forms as
/// equivalents was undocumented, so it was <b>measured</b> (issue #218,
/// OPEN-3), on an RTX 4070 Ti / driver 610.47 against the <c>rel/</c> feature
/// DLL of NGX <c>v310.7.0</c>:</para>
/// <list type="bullet">
///   <item><description><c>"Snippet.OptLevel"</c> →
///   <c>NVSDK_NGX_Result_Success</c>, value <c>40</c> =
///   <c>NVSDK_NGX_OPT_LEVEL_RELEASE</c> — correct for the <c>rel/</c>
///   build.</description></item>
///   <item><description><c>"Snippet.IsDevBranch"</c> →
///   <c>NVSDK_NGX_Result_Success</c>, value <c>0</c> — likewise
///   correct.</description></item>
///   <item><description>Control: a fabricated key
///   (<c>"Snippet.NoSuchKeyAtAll"</c>) → <c>0xBAD00010</c> =
///   <c>FAIL_UnsupportedParameter</c>. So <c>Success</c> here means the key was
///   genuinely present, not that the map answers anything.</description></item>
/// </list>
/// <para>Both are therefore read by name, and a non-<c>Success</c> leaves the
/// field at its default rather than failing the whole query — a feature DLL
/// that does not publish them is a degraded answer, not an error.</para>
/// </remarks>
public readonly record struct DlssStats
{
    /// <summary>Bytes of video memory DLSS has allocated for itself.</summary>
    public ulong VramAllocatedBytes { get; init; }

    /// <summary>
    /// The optimization level of the loaded feature library, as NVIDIA's
    /// <c>NVSDK_NGX_Opt_Level</c> numbering: <c>0</c> undefined, <c>20</c>
    /// debug, <c>30</c> develop, <c>40</c> release. Zero when the feature
    /// library does not publish it.
    /// </summary>
    /// <remarks>
    /// A shipped application should see <c>40</c>. Anything lower means a
    /// <c>dev/</c> feature DLL got deployed — which carries an on-screen
    /// watermark and must not be redistributed.
    /// </remarks>
    public uint OptLevel { get; init; }

    /// <summary>
    /// <see langword="true"/> when the loaded feature library came from a
    /// development branch. <see langword="false"/> for a release build, and
    /// <see langword="false"/> when the library does not publish it.
    /// </summary>
    public bool IsDevSnippetBranch { get; init; }
}
