namespace Ahjo.Vulkan.Ngx.Native;

[NativeTypeName("unsigned int")]
public enum NVSDK_NGX_Result : uint
{
    NVSDK_NGX_Result_Success = 0x1,
    NVSDK_NGX_Result_Fail = 0xBAD00000,
    NVSDK_NGX_Result_FAIL_FeatureNotSupported = NVSDK_NGX_Result_Fail | 1,
    NVSDK_NGX_Result_FAIL_PlatformError = NVSDK_NGX_Result_Fail | 2,
    NVSDK_NGX_Result_FAIL_FeatureAlreadyExists = NVSDK_NGX_Result_Fail | 3,
    NVSDK_NGX_Result_FAIL_FeatureNotFound = NVSDK_NGX_Result_Fail | 4,
    NVSDK_NGX_Result_FAIL_InvalidParameter = NVSDK_NGX_Result_Fail | 5,
    NVSDK_NGX_Result_FAIL_ScratchBufferTooSmall = NVSDK_NGX_Result_Fail | 6,
    NVSDK_NGX_Result_FAIL_NotInitialized = NVSDK_NGX_Result_Fail | 7,
    NVSDK_NGX_Result_FAIL_UnsupportedInputFormat = NVSDK_NGX_Result_Fail | 8,
    NVSDK_NGX_Result_FAIL_RWFlagMissing = NVSDK_NGX_Result_Fail | 9,
    NVSDK_NGX_Result_FAIL_MissingInput = NVSDK_NGX_Result_Fail | 10,
    NVSDK_NGX_Result_FAIL_UnableToInitializeFeature = NVSDK_NGX_Result_Fail | 11,
    NVSDK_NGX_Result_FAIL_OutOfDate = NVSDK_NGX_Result_Fail | 12,
    NVSDK_NGX_Result_FAIL_OutOfGPUMemory = NVSDK_NGX_Result_Fail | 13,
    NVSDK_NGX_Result_FAIL_UnsupportedFormat = NVSDK_NGX_Result_Fail | 14,
    NVSDK_NGX_Result_FAIL_UnableToWriteToAppDataPath = NVSDK_NGX_Result_Fail | 15,
    NVSDK_NGX_Result_FAIL_UnsupportedParameter = NVSDK_NGX_Result_Fail | 16,
    NVSDK_NGX_Result_FAIL_Denied = NVSDK_NGX_Result_Fail | 17,
    NVSDK_NGX_Result_FAIL_NotImplemented = NVSDK_NGX_Result_Fail | 18,
}
