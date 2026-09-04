namespace Ahjo.Vulkan.Ngx.Native;

[NativeTypeName("unsigned int")]
public enum NVSDK_NGX_Feature_Support_Result : uint
{
    NVSDK_NGX_FeatureSupportResult_Supported = 0,
    NVSDK_NGX_FeatureSupportResult_CheckNotPresent = 1,
    NVSDK_NGX_FeatureSupportResult_DriverVersionUnsupported = 2,
    NVSDK_NGX_FeatureSupportResult_AdapterUnsupported = 4,
    NVSDK_NGX_FeatureSupportResult_OSVersionBelowMinimumSupported = 8,
    NVSDK_NGX_FeatureSupportResult_NotImplemented = 16,
}
