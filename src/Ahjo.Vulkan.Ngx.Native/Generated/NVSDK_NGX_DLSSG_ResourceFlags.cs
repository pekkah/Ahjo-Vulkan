namespace Ahjo.Vulkan.Ngx.Native;

[NativeTypeName("unsigned int")]
public enum NVSDK_NGX_DLSSG_ResourceFlags : uint
{
    NVSDK_NGX_DLSSG_ResourceFlags_Backbuffer = 1 << 0,
    NVSDK_NGX_DLSSG_ResourceFlags_MVecs = 1 << 1,
    NVSDK_NGX_DLSSG_ResourceFlags_Depth = 1 << 2,
    NVSDK_NGX_DLSSG_ResourceFlags_HUDLess = 1 << 3,
    NVSDK_NGX_DLSSG_ResourceFlags_UI = 1 << 4,
    NVSDK_NGX_DLSSG_ResourceFlags_BidirectionalDistortionField = 1 << 5,
    NVSDK_NGX_DLSSG_ResourceFlags_Reserved6 = 1 << 6,
    NVSDK_NGX_DLSSG_ResourceFlags_UIAlpha = 1 << 7,
    NVSDK_NGX_DLSSG_ResourceFlags_OutputInterpolated = 1 << 24,
    NVSDK_NGX_DLSSG_ResourceFlags_OutputReal = 1 << 25,
    NVSDK_NGX_DLSSG_ResourceFlags_OutputDisableInterpolation = 1 << 26,
}
