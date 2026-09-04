using Ahjo.Vulkan.Ngx.Native;

namespace Ahjo.Vulkan.Ngx;

/// <summary>
/// Every NGX parameter-map key this wrapper writes or reads, hoisted into
/// <c>static readonly</c> fields once per process.
/// </summary>
/// <remarks>
/// <para>Spec E7: <c>NgxApi</c>'s 204 <c>NVSDK_NGX_Parameter_*</c> properties are
/// <c>"…"u8</c> literals, so they live in the assembly's read-only data segment
/// for the lifetime of the process and carry a terminator past
/// <c>span.Length</c> — exactly and only what
/// <see cref="Utf8Name.FromLiteral"/> accepts. <c>FromLiteral</c> is therefore
/// free here: no <c>fixed</c>, no pinning, no allocation, and the resulting
/// pointers are process-lifetime. The per-frame evaluate path reads a static
/// field instead of deriving a pointer per call, which is one of the four
/// properties holding the zero-allocation guarantee (spec D9).</para>
/// <para><b>Do not add <c>NVSDK_NGX_EParameter_*</c> names here.</b> That
/// 74-member hash-encoded family was excluded from the bindings on purpose
/// (#216 D7/E7) — their values embed raw <c>0x01</c>-<c>0x1f</c> control bytes.
/// If something appears unreachable without one, that is the spec's OPEN-3, not
/// an invitation.</para>
/// <para>Exactly the keys the wrapper uses, and nothing else — an unused key
/// here is a key nobody can tell is dead.</para>
/// </remarks>
internal static class NgxParameterNames
{
    // ---- Feature creation --------------------------------------------------
    public static readonly Utf8Name CreationNodeMask         = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_CreationNodeMask);
    public static readonly Utf8Name VisibilityNodeMask       = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_VisibilityNodeMask);
    public static readonly Utf8Name Width                    = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_Width);
    public static readonly Utf8Name Height                   = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_Height);
    public static readonly Utf8Name OutWidth                 = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_OutWidth);
    public static readonly Utf8Name OutHeight                = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_OutHeight);
    public static readonly Utf8Name PerfQualityValue         = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_PerfQualityValue);
    public static readonly Utf8Name DlssFeatureCreateFlags   = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_DLSS_Feature_Create_Flags);
    public static readonly Utf8Name DlssEnableOutputSubrects = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_DLSS_Enable_Output_Subrects);

    public static readonly Utf8Name HintRenderPresetDlaa             = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_DLSS_Hint_Render_Preset_DLAA);
    public static readonly Utf8Name HintRenderPresetQuality          = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_DLSS_Hint_Render_Preset_Quality);
    public static readonly Utf8Name HintRenderPresetBalanced         = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_DLSS_Hint_Render_Preset_Balanced);
    public static readonly Utf8Name HintRenderPresetPerformance      = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_DLSS_Hint_Render_Preset_Performance);
    public static readonly Utf8Name HintRenderPresetUltraPerformance = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_DLSS_Hint_Render_Preset_UltraPerformance);
    public static readonly Utf8Name HintRenderPresetUltraQuality     = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_DLSS_Hint_Render_Preset_UltraQuality);

    // ---- Evaluate ----------------------------------------------------------
    public static readonly Utf8Name Color                = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_Color);
    public static readonly Utf8Name Output               = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_Output);
    public static readonly Utf8Name Depth                = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_Depth);
    public static readonly Utf8Name MotionVectors        = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_MotionVectors);
    public static readonly Utf8Name ExposureTexture      = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_ExposureTexture);
    public static readonly Utf8Name BiasCurrentColorMask = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_DLSS_Input_Bias_Current_Color_Mask);

    public static readonly Utf8Name JitterOffsetX        = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_Jitter_Offset_X);
    public static readonly Utf8Name JitterOffsetY        = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_Jitter_Offset_Y);
    public static readonly Utf8Name Reset                = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_Reset);
    public static readonly Utf8Name MvScaleX             = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_MV_Scale_X);
    public static readonly Utf8Name MvScaleY             = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_MV_Scale_Y);

    public static readonly Utf8Name RenderSubrectWidth   = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_DLSS_Render_Subrect_Dimensions_Width);
    public static readonly Utf8Name RenderSubrectHeight  = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_DLSS_Render_Subrect_Dimensions_Height);
    public static readonly Utf8Name PreExposure          = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_DLSS_Pre_Exposure);
    public static readonly Utf8Name ExposureScale        = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_DLSS_Exposure_Scale);

    public static readonly Utf8Name ColorSubrectBaseX            = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_DLSS_Input_Color_Subrect_Base_X);
    public static readonly Utf8Name ColorSubrectBaseY            = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_DLSS_Input_Color_Subrect_Base_Y);
    public static readonly Utf8Name DepthSubrectBaseX            = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_DLSS_Input_Depth_Subrect_Base_X);
    public static readonly Utf8Name DepthSubrectBaseY            = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_DLSS_Input_Depth_Subrect_Base_Y);
    public static readonly Utf8Name MotionVectorsSubrectBaseX    = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_DLSS_Input_MV_SubrectBase_X);
    public static readonly Utf8Name MotionVectorsSubrectBaseY    = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_DLSS_Input_MV_SubrectBase_Y);
    public static readonly Utf8Name BiasCurrentColorSubrectBaseX = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_DLSS_Input_Bias_Current_Color_SubrectBase_X);
    public static readonly Utf8Name BiasCurrentColorSubrectBaseY = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_DLSS_Input_Bias_Current_Color_SubrectBase_Y);
    public static readonly Utf8Name OutputSubrectBaseX           = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_DLSS_Output_Subrect_Base_X);
    public static readonly Utf8Name OutputSubrectBaseY           = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_DLSS_Output_Subrect_Base_Y);

    // ---- Capability / settings / stats -------------------------------------
    public static readonly Utf8Name SuperSamplingAvailable             = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_SuperSampling_Available);
    public static readonly Utf8Name SuperSamplingNeedsUpdatedDriver    = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_SuperSampling_NeedsUpdatedDriver);
    public static readonly Utf8Name SuperSamplingFeatureInitResult     = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_SuperSampling_FeatureInitResult);
    public static readonly Utf8Name SuperSamplingMinDriverVersionMajor = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_SuperSampling_MinDriverVersionMajor);
    public static readonly Utf8Name SuperSamplingMinDriverVersionMinor = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_SuperSampling_MinDriverVersionMinor);

    public static readonly Utf8Name OptimalSettingsCallback = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_DLSSOptimalSettingsCallback);
    public static readonly Utf8Name GetStatsCallback        = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_DLSSGetStatsCallback);
    public static readonly Utf8Name RtxValue                = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_RTXValue);
    public static readonly Utf8Name Sharpness               = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_Sharpness);
    public static readonly Utf8Name SizeInBytes             = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_SizeInBytes);

    // The two stats keys NVIDIA's own helper reads through the excluded
    // NVSDK_NGX_EParameter_* hash aliases. The plain string forms were measured
    // to work — see DlssStats' remarks for the numbers and the control. These
    // ARE the string constants, not the hash family; the prohibition above is
    // on NVSDK_NGX_EParameter_*, which stays in force.
    public static readonly Utf8Name OptLevel                = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_OptLevel);
    public static readonly Utf8Name IsDevSnippetBranch      = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_IsDevSnippetBranch);
    public static readonly Utf8Name FreeMemOnReleaseFeature = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_FreeMemOnReleaseFeature);

    public static readonly Utf8Name DynamicMinRenderWidth  = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_DLSS_Get_Dynamic_Min_Render_Width);
    public static readonly Utf8Name DynamicMinRenderHeight = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_DLSS_Get_Dynamic_Min_Render_Height);
    public static readonly Utf8Name DynamicMaxRenderWidth  = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_DLSS_Get_Dynamic_Max_Render_Width);
    public static readonly Utf8Name DynamicMaxRenderHeight = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_DLSS_Get_Dynamic_Max_Render_Height);
}
