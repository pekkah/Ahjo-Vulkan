namespace Ahjo.Vulkan.Ngx.Native;

public enum NVSDK_NGX_DLSS_Feature_Flags
{
    NVSDK_NGX_DLSS_Feature_Flags_IsInvalid = 1 << 31,
    NVSDK_NGX_DLSS_Feature_Flags_None = 0,
    NVSDK_NGX_DLSS_Feature_Flags_IsHDR = 1 << 0,
    NVSDK_NGX_DLSS_Feature_Flags_MVLowRes = 1 << 1,
    NVSDK_NGX_DLSS_Feature_Flags_MVJittered = 1 << 2,
    NVSDK_NGX_DLSS_Feature_Flags_DepthInverted = 1 << 3,
    NVSDK_NGX_DLSS_Feature_Flags_Reserved_0 = 1 << 4,
    NVSDK_NGX_DLSS_Feature_Flags_DoSharpening = 1 << 5,
    NVSDK_NGX_DLSS_Feature_Flags_AutoExposure = 1 << 6,
    NVSDK_NGX_DLSS_Feature_Flags_AlphaUpscaling = 1 << 7,
    NVSDK_NGX_DLSS_Feature_Flags_Reserved_8 = 1 << 8,
}
