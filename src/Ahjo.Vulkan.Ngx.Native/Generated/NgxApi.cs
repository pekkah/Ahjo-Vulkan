using System;
using System.Runtime.InteropServices;

namespace Ahjo.Vulkan.Ngx.Native;

public static unsafe partial class NgxApi
{
    [DllImport("ahjo_ngx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void NVSDK_NGX_Parameter_SetULL(NVSDK_NGX_Parameter* InParameter, [NativeTypeName("const char *")] sbyte* InName, [NativeTypeName("unsigned long long")] ulong InValue);

    [DllImport("ahjo_ngx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void NVSDK_NGX_Parameter_SetF(NVSDK_NGX_Parameter* InParameter, [NativeTypeName("const char *")] sbyte* InName, float InValue);

    [DllImport("ahjo_ngx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void NVSDK_NGX_Parameter_SetD(NVSDK_NGX_Parameter* InParameter, [NativeTypeName("const char *")] sbyte* InName, double InValue);

    [DllImport("ahjo_ngx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void NVSDK_NGX_Parameter_SetUI(NVSDK_NGX_Parameter* InParameter, [NativeTypeName("const char *")] sbyte* InName, [NativeTypeName("unsigned int")] uint InValue);

    [DllImport("ahjo_ngx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void NVSDK_NGX_Parameter_SetI(NVSDK_NGX_Parameter* InParameter, [NativeTypeName("const char *")] sbyte* InName, int InValue);

    [DllImport("ahjo_ngx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void NVSDK_NGX_Parameter_SetVoidPointer(NVSDK_NGX_Parameter* InParameter, [NativeTypeName("const char *")] sbyte* InName, void* InValue);

    [DllImport("ahjo_ngx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern NVSDK_NGX_Result NVSDK_NGX_Parameter_GetULL(NVSDK_NGX_Parameter* InParameter, [NativeTypeName("const char *")] sbyte* InName, [NativeTypeName("unsigned long long *")] ulong* OutValue);

    [DllImport("ahjo_ngx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern NVSDK_NGX_Result NVSDK_NGX_Parameter_GetF(NVSDK_NGX_Parameter* InParameter, [NativeTypeName("const char *")] sbyte* InName, float* OutValue);

    [DllImport("ahjo_ngx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern NVSDK_NGX_Result NVSDK_NGX_Parameter_GetD(NVSDK_NGX_Parameter* InParameter, [NativeTypeName("const char *")] sbyte* InName, double* OutValue);

    [DllImport("ahjo_ngx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern NVSDK_NGX_Result NVSDK_NGX_Parameter_GetUI(NVSDK_NGX_Parameter* InParameter, [NativeTypeName("const char *")] sbyte* InName, [NativeTypeName("unsigned int *")] uint* OutValue);

    [DllImport("ahjo_ngx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern NVSDK_NGX_Result NVSDK_NGX_Parameter_GetI(NVSDK_NGX_Parameter* InParameter, [NativeTypeName("const char *")] sbyte* InName, int* OutValue);

    [DllImport("ahjo_ngx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern NVSDK_NGX_Result NVSDK_NGX_Parameter_GetVoidPointer(NVSDK_NGX_Parameter* InParameter, [NativeTypeName("const char *")] sbyte* InName, void** OutValue);

    [DllImport("ahjo_ngx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern NVSDK_NGX_Result NVSDK_NGX_VULKAN_Shutdown1([NativeTypeName("VkDevice")] Ahjo.Vulkan.Native.VkDevice_T* InDevice);

    [DllImport("ahjo_ngx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern NVSDK_NGX_Result NVSDK_NGX_VULKAN_AllocateParameters(NVSDK_NGX_Parameter** OutParameters);

    [DllImport("ahjo_ngx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern NVSDK_NGX_Result NVSDK_NGX_VULKAN_GetCapabilityParameters(NVSDK_NGX_Parameter** OutParameters);

    [DllImport("ahjo_ngx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern NVSDK_NGX_Result NVSDK_NGX_VULKAN_DestroyParameters(NVSDK_NGX_Parameter* InParameters);

    [DllImport("ahjo_ngx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern NVSDK_NGX_Result NVSDK_NGX_VULKAN_GetScratchBufferSize(NVSDK_NGX_Feature InFeatureId, [NativeTypeName("const NVSDK_NGX_Parameter *")] NVSDK_NGX_Parameter* InParameters, [NativeTypeName("size_t *")] nuint* OutSizeInBytes);

    [DllImport("ahjo_ngx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern NVSDK_NGX_Result NVSDK_NGX_VULKAN_CreateFeature1([NativeTypeName("VkDevice")] Ahjo.Vulkan.Native.VkDevice_T* InDevice, [NativeTypeName("VkCommandBuffer")] Ahjo.Vulkan.Native.VkCommandBuffer_T* InCmdList, NVSDK_NGX_Feature InFeatureID, NVSDK_NGX_Parameter* InParameters, NVSDK_NGX_Handle** OutHandle);

    [DllImport("ahjo_ngx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern NVSDK_NGX_Result NVSDK_NGX_VULKAN_ReleaseFeature(NVSDK_NGX_Handle* InHandle);

    [DllImport("ahjo_ngx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern NVSDK_NGX_Result NVSDK_NGX_VULKAN_EvaluateFeature_C([NativeTypeName("VkCommandBuffer")] Ahjo.Vulkan.Native.VkCommandBuffer_T* InCmdList, [NativeTypeName("const NVSDK_NGX_Handle *")] NVSDK_NGX_Handle* InFeatureHandle, [NativeTypeName("const NVSDK_NGX_Parameter *")] NVSDK_NGX_Parameter* InParameters, [NativeTypeName("PFN_NVSDK_NGX_ProgressCallback_C")] delegate* unmanaged[Cdecl]<float, bool*, void> InCallback);

    [DllImport("ahjo_ngx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("unsigned int")]
    public static extern uint ahjo_ngx_version_api();

    [DllImport("ahjo_ngx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("unsigned int")]
    public static extern uint ahjo_ngx_layout(AhjoNgxLayoutId id);

    [DllImport("ahjo_ngx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("unsigned int")]
    public static extern uint ahjo_ngx_result_to_utf8(NVSDK_NGX_Result result, [NativeTypeName("char *")] sbyte* buffer, [NativeTypeName("unsigned int")] uint bufferSize);

    [DllImport("ahjo_ngx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern NVSDK_NGX_Result ahjo_ngx_vulkan_init_utf8([NativeTypeName("const AhjoNgxInitInfo *")] AhjoNgxInitInfo* info, [NativeTypeName("VkInstance")] Ahjo.Vulkan.Native.VkInstance_T* instance, [NativeTypeName("VkPhysicalDevice")] Ahjo.Vulkan.Native.VkPhysicalDevice_T* physicalDevice, [NativeTypeName("VkDevice")] Ahjo.Vulkan.Native.VkDevice_T* device, [NativeTypeName("PFN_vkGetInstanceProcAddr")] delegate* unmanaged[Cdecl]<Ahjo.Vulkan.Native.VkInstance_T*, sbyte*, delegate* unmanaged[Cdecl]<void>> getInstanceProcAddr, [NativeTypeName("PFN_vkGetDeviceProcAddr")] delegate* unmanaged[Cdecl]<Ahjo.Vulkan.Native.VkDevice_T*, sbyte*, delegate* unmanaged[Cdecl]<void>> getDeviceProcAddr);

    [DllImport("ahjo_ngx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern NVSDK_NGX_Result ahjo_ngx_vulkan_get_feature_requirements_utf8([NativeTypeName("VkInstance")] Ahjo.Vulkan.Native.VkInstance_T* instance, [NativeTypeName("VkPhysicalDevice")] Ahjo.Vulkan.Native.VkPhysicalDevice_T* physicalDevice, NVSDK_NGX_Feature featureId, [NativeTypeName("const AhjoNgxInitInfo *")] AhjoNgxInitInfo* info, NVSDK_NGX_FeatureRequirement* outRequirement);

    [DllImport("ahjo_ngx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern NVSDK_NGX_Result ahjo_ngx_vulkan_get_feature_instance_extension_requirements_utf8(NVSDK_NGX_Feature featureId, [NativeTypeName("const AhjoNgxInitInfo *")] AhjoNgxInitInfo* info, [NativeTypeName("unsigned int *")] uint* outExtensionCount, [NativeTypeName("VkExtensionProperties **")] Ahjo.Vulkan.Native.VkExtensionProperties** outExtensionProperties);

    [DllImport("ahjo_ngx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern NVSDK_NGX_Result ahjo_ngx_vulkan_get_feature_device_extension_requirements_utf8([NativeTypeName("VkInstance")] Ahjo.Vulkan.Native.VkInstance_T* instance, [NativeTypeName("VkPhysicalDevice")] Ahjo.Vulkan.Native.VkPhysicalDevice_T* physicalDevice, NVSDK_NGX_Feature featureId, [NativeTypeName("const AhjoNgxInitInfo *")] AhjoNgxInitInfo* info, [NativeTypeName("unsigned int *")] uint* outExtensionCount, [NativeTypeName("VkExtensionProperties **")] Ahjo.Vulkan.Native.VkExtensionProperties** outExtensionProperties);

    [NativeTypeName("#define NVSDK_NGX_VERSION_API_MACRO 0x0000015")]
    public const int NVSDK_NGX_VERSION_API_MACRO = 0x0000015;

    [NativeTypeName("#define NVSDK_NGX_DLSS_DEBUG_OVERLAY_VALUE_UNSET -1")]
    public const int NVSDK_NGX_DLSS_DEBUG_OVERLAY_VALUE_UNSET = -1;

    [NativeTypeName("#define NVSDK_NGX_Parameter_OptLevel \"Snippet.OptLevel\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_OptLevel => "Snippet.OptLevel"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_IsDevSnippetBranch \"Snippet.IsDevBranch\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_IsDevSnippetBranch => "Snippet.IsDevBranch"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_SuperSampling_ScaleFactor \"SuperSampling.ScaleFactor\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_SuperSampling_ScaleFactor => "SuperSampling.ScaleFactor"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_ImageSignalProcessing_ScaleFactor \"ImageSignalProcessing.ScaleFactor\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_ImageSignalProcessing_ScaleFactor => "ImageSignalProcessing.ScaleFactor"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_SuperSampling_Available \"SuperSampling.Available\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_SuperSampling_Available => "SuperSampling.Available"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_InPainting_Available \"InPainting.Available\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_InPainting_Available => "InPainting.Available"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_ImageSuperResolution_Available \"ImageSuperResolution.Available\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_ImageSuperResolution_Available => "ImageSuperResolution.Available"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_SlowMotion_Available \"SlowMotion.Available\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_SlowMotion_Available => "SlowMotion.Available"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_VideoSuperResolution_Available \"VideoSuperResolution.Available\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_VideoSuperResolution_Available => "VideoSuperResolution.Available"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_ImageSignalProcessing_Available \"ImageSignalProcessing.Available\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_ImageSignalProcessing_Available => "ImageSignalProcessing.Available"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DeepResolve_Available \"DeepResolve.Available\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DeepResolve_Available => "DeepResolve.Available"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_SuperSampling_NeedsUpdatedDriver \"SuperSampling.NeedsUpdatedDriver\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_SuperSampling_NeedsUpdatedDriver => "SuperSampling.NeedsUpdatedDriver"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_InPainting_NeedsUpdatedDriver \"InPainting.NeedsUpdatedDriver\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_InPainting_NeedsUpdatedDriver => "InPainting.NeedsUpdatedDriver"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_ImageSuperResolution_NeedsUpdatedDriver \"ImageSuperResolution.NeedsUpdatedDriver\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_ImageSuperResolution_NeedsUpdatedDriver => "ImageSuperResolution.NeedsUpdatedDriver"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_SlowMotion_NeedsUpdatedDriver \"SlowMotion.NeedsUpdatedDriver\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_SlowMotion_NeedsUpdatedDriver => "SlowMotion.NeedsUpdatedDriver"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_VideoSuperResolution_NeedsUpdatedDriver \"VideoSuperResolution.NeedsUpdatedDriver\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_VideoSuperResolution_NeedsUpdatedDriver => "VideoSuperResolution.NeedsUpdatedDriver"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_ImageSignalProcessing_NeedsUpdatedDriver \"ImageSignalProcessing.NeedsUpdatedDriver\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_ImageSignalProcessing_NeedsUpdatedDriver => "ImageSignalProcessing.NeedsUpdatedDriver"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DeepResolve_NeedsUpdatedDriver \"DeepResolve.NeedsUpdatedDriver\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DeepResolve_NeedsUpdatedDriver => "DeepResolve.NeedsUpdatedDriver"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_FrameInterpolation_NeedsUpdatedDriver \"FrameInterpolation.NeedsUpdatedDriver\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_FrameInterpolation_NeedsUpdatedDriver => "FrameInterpolation.NeedsUpdatedDriver"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_SuperSampling_MinDriverVersionMajor \"SuperSampling.MinDriverVersionMajor\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_SuperSampling_MinDriverVersionMajor => "SuperSampling.MinDriverVersionMajor"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_InPainting_MinDriverVersionMajor \"InPainting.MinDriverVersionMajor\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_InPainting_MinDriverVersionMajor => "InPainting.MinDriverVersionMajor"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_ImageSuperResolution_MinDriverVersionMajor \"ImageSuperResolution.MinDriverVersionMajor\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_ImageSuperResolution_MinDriverVersionMajor => "ImageSuperResolution.MinDriverVersionMajor"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_SlowMotion_MinDriverVersionMajor \"SlowMotion.MinDriverVersionMajor\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_SlowMotion_MinDriverVersionMajor => "SlowMotion.MinDriverVersionMajor"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_VideoSuperResolution_MinDriverVersionMajor \"VideoSuperResolution.MinDriverVersionMajor\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_VideoSuperResolution_MinDriverVersionMajor => "VideoSuperResolution.MinDriverVersionMajor"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_ImageSignalProcessing_MinDriverVersionMajor \"ImageSignalProcessing.MinDriverVersionMajor\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_ImageSignalProcessing_MinDriverVersionMajor => "ImageSignalProcessing.MinDriverVersionMajor"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DeepResolve_MinDriverVersionMajor \"DeepResolve.MinDriverVersionMajor\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DeepResolve_MinDriverVersionMajor => "DeepResolve.MinDriverVersionMajor"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_FrameInterpolation_MinDriverVersionMajor \"FrameInterpolation.MinDriverVersionMajor\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_FrameInterpolation_MinDriverVersionMajor => "FrameInterpolation.MinDriverVersionMajor"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_SuperSampling_MinDriverVersionMinor \"SuperSampling.MinDriverVersionMinor\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_SuperSampling_MinDriverVersionMinor => "SuperSampling.MinDriverVersionMinor"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_InPainting_MinDriverVersionMinor \"InPainting.MinDriverVersionMinor\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_InPainting_MinDriverVersionMinor => "InPainting.MinDriverVersionMinor"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_ImageSuperResolution_MinDriverVersionMinor \"ImageSuperResolution.MinDriverVersionMinor\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_ImageSuperResolution_MinDriverVersionMinor => "ImageSuperResolution.MinDriverVersionMinor"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_SlowMotion_MinDriverVersionMinor \"SlowMotion.MinDriverVersionMinor\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_SlowMotion_MinDriverVersionMinor => "SlowMotion.MinDriverVersionMinor"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_VideoSuperResolution_MinDriverVersionMinor \"VideoSuperResolution.MinDriverVersionMinor\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_VideoSuperResolution_MinDriverVersionMinor => "VideoSuperResolution.MinDriverVersionMinor"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_ImageSignalProcessing_MinDriverVersionMinor \"ImageSignalProcessing.MinDriverVersionMinor\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_ImageSignalProcessing_MinDriverVersionMinor => "ImageSignalProcessing.MinDriverVersionMinor"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DeepResolve_MinDriverVersionMinor \"DeepResolve.MinDriverVersionMinor\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DeepResolve_MinDriverVersionMinor => "DeepResolve.MinDriverVersionMinor"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_SuperSampling_FeatureInitResult \"SuperSampling.FeatureInitResult\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_SuperSampling_FeatureInitResult => "SuperSampling.FeatureInitResult"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_InPainting_FeatureInitResult \"InPainting.FeatureInitResult\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_InPainting_FeatureInitResult => "InPainting.FeatureInitResult"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_ImageSuperResolution_FeatureInitResult \"ImageSuperResolution.FeatureInitResult\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_ImageSuperResolution_FeatureInitResult => "ImageSuperResolution.FeatureInitResult"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_SlowMotion_FeatureInitResult \"SlowMotion.FeatureInitResult\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_SlowMotion_FeatureInitResult => "SlowMotion.FeatureInitResult"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_VideoSuperResolution_FeatureInitResult \"VideoSuperResolution.FeatureInitResult\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_VideoSuperResolution_FeatureInitResult => "VideoSuperResolution.FeatureInitResult"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_ImageSignalProcessing_FeatureInitResult \"ImageSignalProcessing.FeatureInitResult\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_ImageSignalProcessing_FeatureInitResult => "ImageSignalProcessing.FeatureInitResult"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DeepResolve_FeatureInitResult \"DeepResolve.FeatureInitResult\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DeepResolve_FeatureInitResult => "DeepResolve.FeatureInitResult"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_FrameInterpolation_FeatureInitResult \"FrameInterpolation.FeatureInitResult\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_FrameInterpolation_FeatureInitResult => "FrameInterpolation.FeatureInitResult"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_ImageSuperResolution_ScaleFactor_2_1 \"ImageSuperResolution.ScaleFactor.2.1\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_ImageSuperResolution_ScaleFactor_2_1 => "ImageSuperResolution.ScaleFactor.2.1"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_ImageSuperResolution_ScaleFactor_3_1 \"ImageSuperResolution.ScaleFactor.3.1\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_ImageSuperResolution_ScaleFactor_3_1 => "ImageSuperResolution.ScaleFactor.3.1"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_ImageSuperResolution_ScaleFactor_3_2 \"ImageSuperResolution.ScaleFactor.3.2\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_ImageSuperResolution_ScaleFactor_3_2 => "ImageSuperResolution.ScaleFactor.3.2"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_ImageSuperResolution_ScaleFactor_4_3 \"ImageSuperResolution.ScaleFactor.4.3\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_ImageSuperResolution_ScaleFactor_4_3 => "ImageSuperResolution.ScaleFactor.4.3"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_NumFrames \"NumFrames\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_NumFrames => "NumFrames"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Scale \"Scale\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Scale => "Scale"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Width \"Width\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Width => "Width"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Height \"Height\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Height => "Height"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_OutWidth \"OutWidth\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_OutWidth => "OutWidth"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_OutHeight \"OutHeight\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_OutHeight => "OutHeight"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Sharpness \"Sharpness\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Sharpness => "Sharpness"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Scratch \"Scratch\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Scratch => "Scratch"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Scratch_SizeInBytes \"Scratch.SizeInBytes\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Scratch_SizeInBytes => "Scratch.SizeInBytes"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Input1 \"Input1\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Input1 => "Input1"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Input1_Format \"Input1.Format\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Input1_Format => "Input1.Format"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Input1_SizeInBytes \"Input1.SizeInBytes\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Input1_SizeInBytes => "Input1.SizeInBytes"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Input2 \"Input2\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Input2 => "Input2"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Input2_Format \"Input2.Format\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Input2_Format => "Input2.Format"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Input2_SizeInBytes \"Input2.SizeInBytes\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Input2_SizeInBytes => "Input2.SizeInBytes"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Color \"Color\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Color => "Color"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Color_Format \"Color.Format\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Color_Format => "Color.Format"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Color_SizeInBytes \"Color.SizeInBytes\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Color_SizeInBytes => "Color.SizeInBytes"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_FI_Color1 \"Color1\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_FI_Color1 => "Color1"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_FI_Color2 \"Color2\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_FI_Color2 => "Color2"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Albedo \"Albedo\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Albedo => "Albedo"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Output \"Output\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Output => "Output"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Output_Format \"Output.Format\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Output_Format => "Output.Format"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Output_SizeInBytes \"Output.SizeInBytes\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Output_SizeInBytes => "Output.SizeInBytes"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_FI_Output1 \"Output1\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_FI_Output1 => "Output1"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_FI_Output2 \"Output2\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_FI_Output2 => "Output2"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_FI_Output3 \"Output3\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_FI_Output3 => "Output3"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Reset \"Reset\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Reset => "Reset"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_BlendFactor \"BlendFactor\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_BlendFactor => "BlendFactor"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_MotionVectors \"MotionVectors\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_MotionVectors => "MotionVectors"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_FI_MotionVectors1 \"MotionVectors1\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_FI_MotionVectors1 => "MotionVectors1"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_FI_MotionVectors2 \"MotionVectors2\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_FI_MotionVectors2 => "MotionVectors2"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Rect_X \"Rect.X\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Rect_X => "Rect.X"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Rect_Y \"Rect.Y\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Rect_Y => "Rect.Y"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Rect_W \"Rect.W\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Rect_W => "Rect.W"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Rect_H \"Rect.H\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Rect_H => "Rect.H"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_OutRect_X \"OutRect.X\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_OutRect_X => "OutRect.X"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_OutRect_Y \"OutRect.Y\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_OutRect_Y => "OutRect.Y"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_OutRect_W \"OutRect.W\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_OutRect_W => "OutRect.W"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_OutRect_H \"OutRect.H\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_OutRect_H => "OutRect.H"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_MV_Scale_X \"MV.Scale.X\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_MV_Scale_X => "MV.Scale.X"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_MV_Scale_Y \"MV.Scale.Y\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_MV_Scale_Y => "MV.Scale.Y"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Model \"Model\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Model => "Model"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Format \"Format\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Format => "Format"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_SizeInBytes \"SizeInBytes\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_SizeInBytes => "SizeInBytes"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_ResourceAllocCallback \"ResourceAllocCallback\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_ResourceAllocCallback => "ResourceAllocCallback"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_BufferAllocCallback \"BufferAllocCallback\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_BufferAllocCallback => "BufferAllocCallback"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Tex2DAllocCallback \"Tex2DAllocCallback\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Tex2DAllocCallback => "Tex2DAllocCallback"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_ResourceReleaseCallback \"ResourceReleaseCallback\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_ResourceReleaseCallback => "ResourceReleaseCallback"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_CreationNodeMask \"CreationNodeMask\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_CreationNodeMask => "CreationNodeMask"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_VisibilityNodeMask \"VisibilityNodeMask\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_VisibilityNodeMask => "VisibilityNodeMask"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_MV_Offset_X \"MV.Offset.X\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_MV_Offset_X => "MV.Offset.X"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_MV_Offset_Y \"MV.Offset.Y\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_MV_Offset_Y => "MV.Offset.Y"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Hint_UseFireflySwatter \"Hint.UseFireflySwatter\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Hint_UseFireflySwatter => "Hint.UseFireflySwatter"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Resource_Width \"ResourceWidth\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Resource_Width => "ResourceWidth"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Resource_Height \"ResourceHeight\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Resource_Height => "ResourceHeight"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Resource_OutWidth \"ResourceOutWidth\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Resource_OutWidth => "ResourceOutWidth"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Resource_OutHeight \"ResourceOutHeight\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Resource_OutHeight => "ResourceOutHeight"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Depth \"Depth\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Depth => "Depth"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_FI_Depth1 \"Depth1\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_FI_Depth1 => "Depth1"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_FI_Depth2 \"Depth2\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_FI_Depth2 => "Depth2"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSSOptimalSettingsCallback \"DLSSOptimalSettingsCallback\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSSOptimalSettingsCallback => "DLSSOptimalSettingsCallback"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSSGetStatsCallback \"DLSSGetStatsCallback\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSSGetStatsCallback => "DLSSGetStatsCallback"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_PerfQualityValue \"PerfQualityValue\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_PerfQualityValue => "PerfQualityValue"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_RTXValue \"RTXValue\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_RTXValue => "RTXValue"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSSMode \"DLSSMode\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSSMode => "DLSSMode"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_FI_Mode \"FIMode\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_FI_Mode => "FIMode"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_FI_OF_Preset \"FIOFPreset\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_FI_OF_Preset => "FIOFPreset"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_FI_OF_GridSize \"FIOFGridSize\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_FI_OF_GridSize => "FIOFGridSize"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Jitter_Offset_X \"Jitter.Offset.X\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Jitter_Offset_X => "Jitter.Offset.X"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Jitter_Offset_Y \"Jitter.Offset.Y\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Jitter_Offset_Y => "Jitter.Offset.Y"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Denoise \"Denoise\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Denoise => "Denoise"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_TransparencyMask \"TransparencyMask\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_TransparencyMask => "TransparencyMask"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_ExposureTexture \"ExposureTexture\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_ExposureTexture => "ExposureTexture"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Feature_Create_Flags \"DLSS.Feature.Create.Flags\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Feature_Create_Flags => "DLSS.Feature.Create.Flags"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Checkerboard_Jitter_Hack \"DLSS.Checkerboard.Jitter.Hack\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Checkerboard_Jitter_Hack => "DLSS.Checkerboard.Jitter.Hack"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_GBuffer_Normals \"GBuffer.Normals\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_GBuffer_Normals => "GBuffer.Normals"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_GBuffer_Albedo \"GBuffer.Albedo\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_GBuffer_Albedo => "GBuffer.Albedo"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_GBuffer_Roughness \"GBuffer.Roughness\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_GBuffer_Roughness => "GBuffer.Roughness"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_GBuffer_DiffuseAlbedo \"GBuffer.DiffuseAlbedo\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_GBuffer_DiffuseAlbedo => "GBuffer.DiffuseAlbedo"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_GBuffer_SpecularAlbedo \"GBuffer.SpecularAlbedo\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_GBuffer_SpecularAlbedo => "GBuffer.SpecularAlbedo"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_GBuffer_IndirectAlbedo \"GBuffer.IndirectAlbedo\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_GBuffer_IndirectAlbedo => "GBuffer.IndirectAlbedo"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_GBuffer_SpecularMvec \"GBuffer.SpecularMvec\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_GBuffer_SpecularMvec => "GBuffer.SpecularMvec"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_GBuffer_DisocclusionMask \"GBuffer.DisocclusionMask\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_GBuffer_DisocclusionMask => "GBuffer.DisocclusionMask"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_GBuffer_Metallic \"GBuffer.Metallic\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_GBuffer_Metallic => "GBuffer.Metallic"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_GBuffer_Specular \"GBuffer.Specular\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_GBuffer_Specular => "GBuffer.Specular"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_GBuffer_Subsurface \"GBuffer.Subsurface\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_GBuffer_Subsurface => "GBuffer.Subsurface"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_GBuffer_ShadingModelId \"GBuffer.ShadingModelId\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_GBuffer_ShadingModelId => "GBuffer.ShadingModelId"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_GBuffer_MaterialId \"GBuffer.MaterialId\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_GBuffer_MaterialId => "GBuffer.MaterialId"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_GBuffer_Atrrib_8 \"GBuffer.Attrib.8\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_GBuffer_Atrrib_8 => "GBuffer.Attrib.8"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_GBuffer_Atrrib_9 \"GBuffer.Attrib.9\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_GBuffer_Atrrib_9 => "GBuffer.Attrib.9"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_GBuffer_Atrrib_10 \"GBuffer.Attrib.10\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_GBuffer_Atrrib_10 => "GBuffer.Attrib.10"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_GBuffer_Atrrib_11 \"GBuffer.Attrib.11\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_GBuffer_Atrrib_11 => "GBuffer.Attrib.11"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_GBuffer_Atrrib_12 \"GBuffer.Attrib.12\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_GBuffer_Atrrib_12 => "GBuffer.Attrib.12"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_GBuffer_Atrrib_13 \"GBuffer.Attrib.13\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_GBuffer_Atrrib_13 => "GBuffer.Attrib.13"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_GBuffer_Atrrib_14 \"GBuffer.Attrib.14\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_GBuffer_Atrrib_14 => "GBuffer.Attrib.14"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_GBuffer_Atrrib_15 \"GBuffer.Attrib.15\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_GBuffer_Atrrib_15 => "GBuffer.Attrib.15"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_TonemapperType \"TonemapperType\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_TonemapperType => "TonemapperType"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_FreeMemOnReleaseFeature \"FreeMemOnReleaseFeature\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_FreeMemOnReleaseFeature => "FreeMemOnReleaseFeature"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_MotionVectors3D \"MotionVectors3D\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_MotionVectors3D => "MotionVectors3D"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_IsParticleMask \"IsParticleMask\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_IsParticleMask => "IsParticleMask"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_AnimatedTextureMask \"AnimatedTextureMask\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_AnimatedTextureMask => "AnimatedTextureMask"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DepthHighRes \"DepthHighRes\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DepthHighRes => "DepthHighRes"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_Position_ViewSpace \"Position.ViewSpace\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Position_ViewSpace => "Position.ViewSpace"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_FrameTimeDeltaInMsec \"FrameTimeDeltaInMsec\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_FrameTimeDeltaInMsec => "FrameTimeDeltaInMsec"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_RayTracingHitDistance \"RayTracingHitDistance\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_RayTracingHitDistance => "RayTracingHitDistance"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_MotionVectorsReflection \"MotionVectorsReflection\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_MotionVectorsReflection => "MotionVectorsReflection"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Enable_Output_Subrects \"DLSS.Enable.Output.Subrects\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Enable_Output_Subrects => "DLSS.Enable.Output.Subrects"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Input_Color_Subrect_Base_X \"DLSS.Input.Color.Subrect.Base.X\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Input_Color_Subrect_Base_X => "DLSS.Input.Color.Subrect.Base.X"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Input_Color_Subrect_Base_Y \"DLSS.Input.Color.Subrect.Base.Y\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Input_Color_Subrect_Base_Y => "DLSS.Input.Color.Subrect.Base.Y"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Input_Depth_Subrect_Base_X \"DLSS.Input.Depth.Subrect.Base.X\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Input_Depth_Subrect_Base_X => "DLSS.Input.Depth.Subrect.Base.X"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Input_Depth_Subrect_Base_Y \"DLSS.Input.Depth.Subrect.Base.Y\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Input_Depth_Subrect_Base_Y => "DLSS.Input.Depth.Subrect.Base.Y"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Input_MV_SubrectBase_X \"DLSS.Input.MV.Subrect.Base.X\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Input_MV_SubrectBase_X => "DLSS.Input.MV.Subrect.Base.X"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Input_MV_SubrectBase_Y \"DLSS.Input.MV.Subrect.Base.Y\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Input_MV_SubrectBase_Y => "DLSS.Input.MV.Subrect.Base.Y"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Input_Translucency_SubrectBase_X \"DLSS.Input.Translucency.Subrect.Base.X\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Input_Translucency_SubrectBase_X => "DLSS.Input.Translucency.Subrect.Base.X"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Input_Translucency_SubrectBase_Y \"DLSS.Input.Translucency.Subrect.Base.Y\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Input_Translucency_SubrectBase_Y => "DLSS.Input.Translucency.Subrect.Base.Y"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Output_Subrect_Base_X \"DLSS.Output.Subrect.Base.X\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Output_Subrect_Base_X => "DLSS.Output.Subrect.Base.X"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Output_Subrect_Base_Y \"DLSS.Output.Subrect.Base.Y\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Output_Subrect_Base_Y => "DLSS.Output.Subrect.Base.Y"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Render_Subrect_Dimensions_Width \"DLSS.Render.Subrect.Dimensions.Width\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Render_Subrect_Dimensions_Width => "DLSS.Render.Subrect.Dimensions.Width"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Render_Subrect_Dimensions_Height \"DLSS.Render.Subrect.Dimensions.Height\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Render_Subrect_Dimensions_Height => "DLSS.Render.Subrect.Dimensions.Height"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Pre_Exposure \"DLSS.Pre.Exposure\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Pre_Exposure => "DLSS.Pre.Exposure"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Exposure_Scale \"DLSS.Exposure.Scale\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Exposure_Scale => "DLSS.Exposure.Scale"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Input_Bias_Current_Color_Mask \"DLSS.Input.Bias.Current.Color.Mask\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Input_Bias_Current_Color_Mask => "DLSS.Input.Bias.Current.Color.Mask"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Input_Bias_Current_Color_SubrectBase_X \"DLSS.Input.Bias.Current.Color.Subrect.Base.X\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Input_Bias_Current_Color_SubrectBase_X => "DLSS.Input.Bias.Current.Color.Subrect.Base.X"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Input_Bias_Current_Color_SubrectBase_Y \"DLSS.Input.Bias.Current.Color.Subrect.Base.Y\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Input_Bias_Current_Color_SubrectBase_Y => "DLSS.Input.Bias.Current.Color.Subrect.Base.Y"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Indicator_Invert_Y_Axis \"DLSS.Indicator.Invert.Y.Axis\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Indicator_Invert_Y_Axis => "DLSS.Indicator.Invert.Y.Axis"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Indicator_Invert_X_Axis \"DLSS.Indicator.Invert.X.Axis\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Indicator_Invert_X_Axis => "DLSS.Indicator.Invert.X.Axis"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Overlay_Debug_Layer \"DLSS.Overlay.Debug.Layer\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Overlay_Debug_Layer => "DLSS.Overlay.Debug.Layer"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Overlay_Full_Screen \"DLSS.Overlay.Full.Screen\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Overlay_Full_Screen => "DLSS.Overlay.Full.Screen"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Overlay_Show_Nans \"DLSS.Overlay.Show.Nans\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Overlay_Show_Nans => "DLSS.Overlay.Show.Nans"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Overlay_Jitter_Debug \"DLSS.Overlay.Jitter.Debug\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Overlay_Jitter_Debug => "DLSS.Overlay.Jitter.Debug"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_INV_VIEW_PROJECTION_MATRIX \"InvViewProjectionMatrix\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_INV_VIEW_PROJECTION_MATRIX => "InvViewProjectionMatrix"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_CLIP_TO_PREV_CLIP_MATRIX \"ClipToPrevClipMatrix\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_CLIP_TO_PREV_CLIP_MATRIX => "ClipToPrevClipMatrix"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_TransparencyLayer \"DLSS.TransparencyLayer\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_TransparencyLayer => "DLSS.TransparencyLayer"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_TransparencyLayer_Subrect_Base_X \"DLSS.TransparencyLayer.Subrect.Base.X\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_TransparencyLayer_Subrect_Base_X => "DLSS.TransparencyLayer.Subrect.Base.X"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_TransparencyLayer_Subrect_Base_Y \"DLSS.TransparencyLayer.Subrect.Base.Y\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_TransparencyLayer_Subrect_Base_Y => "DLSS.TransparencyLayer.Subrect.Base.Y"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_TransparencyLayerOpacity \"DLSS.TransparencyLayerOpacity\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_TransparencyLayerOpacity => "DLSS.TransparencyLayerOpacity"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_TransparencyLayerOpacity_Subrect_Base_X \"DLSS.TransparencyLayerOpacity.Subrect.Base.X\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_TransparencyLayerOpacity_Subrect_Base_X => "DLSS.TransparencyLayerOpacity.Subrect.Base.X"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_TransparencyLayerOpacity_Subrect_Base_Y \"DLSS.TransparencyLayerOpacity.Subrect.Base.Y\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_TransparencyLayerOpacity_Subrect_Base_Y => "DLSS.TransparencyLayerOpacity.Subrect.Base.Y"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_TransparencyLayerMvecs \"DLSS.TransparencyLayerMvecs\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_TransparencyLayerMvecs => "DLSS.TransparencyLayerMvecs"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_TransparencyLayerMvecs_Subrect_Base_X \"DLSS.TransparencyLayerMvecs.Subrect.Base.X\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_TransparencyLayerMvecs_Subrect_Base_X => "DLSS.TransparencyLayerMvecs.Subrect.Base.X"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_TransparencyLayerMvecs_Subrect_Base_Y \"DLSS.TransparencyLayerMvecs.Subrect.Base.Y\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_TransparencyLayerMvecs_Subrect_Base_Y => "DLSS.TransparencyLayerMvecs.Subrect.Base.Y"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_DisocclusionMask \"DLSS.DisocclusionMask\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_DisocclusionMask => "DLSS.DisocclusionMask"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_DisocclusionMask_Subrect_Base_X \"DLSS.DisocclusionMask.Subrect.Base.X\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_DisocclusionMask_Subrect_Base_X => "DLSS.DisocclusionMask.Subrect.Base.X"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_DisocclusionMask_Subrect_Base_Y \"DLSS.DisocclusionMask.Subrect.Base.Y\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_DisocclusionMask_Subrect_Base_Y => "DLSS.DisocclusionMask.Subrect.Base.Y"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_ResponsivityMask \"DLSS.ResponsivityMask\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_ResponsivityMask => "DLSS.ResponsivityMask"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_ResponsivityMask_Subrect_Base_X \"DLSS.ResponsivityMask.Subrect.Base.X\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_ResponsivityMask_Subrect_Base_X => "DLSS.ResponsivityMask.Subrect.Base.X"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_ResponsivityMask_Subrect_Base_Y \"DLSS.ResponsivityMask.Subrect.Base.Y\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_ResponsivityMask_Subrect_Base_Y => "DLSS.ResponsivityMask.Subrect.Base.Y"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Get_Dynamic_Max_Render_Width \"DLSS.Get.Dynamic.Max.Render.Width\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Get_Dynamic_Max_Render_Width => "DLSS.Get.Dynamic.Max.Render.Width"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Get_Dynamic_Max_Render_Height \"DLSS.Get.Dynamic.Max.Render.Height\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Get_Dynamic_Max_Render_Height => "DLSS.Get.Dynamic.Max.Render.Height"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Get_Dynamic_Min_Render_Width \"DLSS.Get.Dynamic.Min.Render.Width\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Get_Dynamic_Min_Render_Width => "DLSS.Get.Dynamic.Min.Render.Width"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Get_Dynamic_Min_Render_Height \"DLSS.Get.Dynamic.Min.Render.Height\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Get_Dynamic_Min_Render_Height => "DLSS.Get.Dynamic.Min.Render.Height"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Hint_Render_Preset_DLAA \"DLSS.Hint.Render.Preset.DLAA\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Hint_Render_Preset_DLAA => "DLSS.Hint.Render.Preset.DLAA"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Hint_Render_Preset_Quality \"DLSS.Hint.Render.Preset.Quality\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Hint_Render_Preset_Quality => "DLSS.Hint.Render.Preset.Quality"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Hint_Render_Preset_Balanced \"DLSS.Hint.Render.Preset.Balanced\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Hint_Render_Preset_Balanced => "DLSS.Hint.Render.Preset.Balanced"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Hint_Render_Preset_Performance \"DLSS.Hint.Render.Preset.Performance\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Hint_Render_Preset_Performance => "DLSS.Hint.Render.Preset.Performance"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Hint_Render_Preset_UltraPerformance \"DLSS.Hint.Render.Preset.UltraPerformance\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Hint_Render_Preset_UltraPerformance => "DLSS.Hint.Render.Preset.UltraPerformance"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_DLSS_Hint_Render_Preset_UltraQuality \"DLSS.Hint.Render.Preset.UltraQuality\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_DLSS_Hint_Render_Preset_UltraQuality => "DLSS.Hint.Render.Preset.UltraQuality"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_FrameGeneration_Available \"FrameGeneration.Available\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_FrameGeneration_Available => "FrameGeneration.Available"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_FrameGeneration_FeatureInitResult \"FrameGeneration.FeatureInitResult\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_FrameGeneration_FeatureInitResult => "FrameGeneration.FeatureInitResult"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_FrameGeneration_MinDriverVersionMajor \"FrameGeneration.MinDriverVersionMajor\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_FrameGeneration_MinDriverVersionMajor => "FrameGeneration.MinDriverVersionMajor"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_FrameGeneration_MinDriverVersionMinor \"FrameGeneration.MinDriverVersionMinor\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_FrameGeneration_MinDriverVersionMinor => "FrameGeneration.MinDriverVersionMinor"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_FrameGeneration_NeedsUpdatedDriver \"FrameGeneration.NeedsUpdatedDriver\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_FrameGeneration_NeedsUpdatedDriver => "FrameGeneration.NeedsUpdatedDriver"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_FrameInterpolation_Available \"FrameInterpolation.Available\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_FrameInterpolation_Available => "FrameInterpolation.Available"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_FrameInterpolation_MinDriverVersionMinor \"FrameInterpolation.MinDriverVersionMinor\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_FrameInterpolation_MinDriverVersionMinor => "FrameInterpolation.MinDriverVersionMinor"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_BackbufferFormat \"DLSSG.BackbufferFormat\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_BackbufferFormat => "DLSSG.BackbufferFormat"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_Backbuffer \"DLSSG.Backbuffer\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_Backbuffer => "DLSSG.Backbuffer"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_MVecs \"DLSSG.MVecs\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_MVecs => "DLSSG.MVecs"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_Depth \"DLSSG.Depth\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_Depth => "DLSSG.Depth"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_BidirectionalDistortionField \"DLSSG.BidirectionalDistortionField\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_BidirectionalDistortionField => "DLSSG.BidirectionalDistortionField"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_HUDLess \"DLSSG.HUDLess\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_HUDLess => "DLSSG.HUDLess"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_UI \"DLSSG.UI\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_UI => "DLSSG.UI"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_UIAlpha \"DLSSG.UIAlpha\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_UIAlpha => "DLSSG.UIAlpha"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_OutputInterpolated \"DLSSG.OutputInterpolated\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_OutputInterpolated => "DLSSG.OutputInterpolated"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_OutputReal \"DLSSG.OutputReal\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_OutputReal => "DLSSG.OutputReal"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_OutputInterpolated \"DLSSG.OutputInterpolated\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_OutputInterpolated => "DLSSG.OutputInterpolated"u8;

    [NativeTypeName("#define NVSDK_NGX_Parameter_OutputReal \"DLSSG.OutputReal\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_OutputReal => "DLSSG.OutputReal"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_ResourceAlwaysProvided_Flags \"DLSSG.ResourceAlwaysProvidedFlags\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_ResourceAlwaysProvided_Flags => "DLSSG.ResourceAlwaysProvidedFlags"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_ResourceNeverProvided_Flags \"DLSSG.ResourceNeverProvidedFlags\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_ResourceNeverProvided_Flags => "DLSSG.ResourceNeverProvidedFlags"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_Width \"DLSSG.Width\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_Width => "DLSSG.Width"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_Height \"DLSSG.Height\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_Height => "DLSSG.Height"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_OutputDisableInterpolation \"DLSSG.OutputDisableInterpolation\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_OutputDisableInterpolation => "DLSSG.OutputDisableInterpolation"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_CameraViewToClip \"DLSSG.CameraViewToClip\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_CameraViewToClip => "DLSSG.CameraViewToClip"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_ClipToCameraView \"DLSSG.ClipToCameraView\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_ClipToCameraView => "DLSSG.ClipToCameraView"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_ClipToLensClip \"DLSSG.ClipToLensClip\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_ClipToLensClip => "DLSSG.ClipToLensClip"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_ClipToPrevClip \"DLSSG.ClipToPrevClip\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_ClipToPrevClip => "DLSSG.ClipToPrevClip"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_PrevClipToClip \"DLSSG.PrevClipToClip\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_PrevClipToClip => "DLSSG.PrevClipToClip"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_JitterOffsetX \"DLSSG.JitterOffsetX\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_JitterOffsetX => "DLSSG.JitterOffsetX"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_JitterOffsetY \"DLSSG.JitterOffsetY\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_JitterOffsetY => "DLSSG.JitterOffsetY"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_MvecScaleX \"DLSSG.MvecScaleX\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_MvecScaleX => "DLSSG.MvecScaleX"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_MvecScaleY \"DLSSG.MvecScaleY\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_MvecScaleY => "DLSSG.MvecScaleY"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_CameraPinholeOffsetX \"DLSSG.CameraPinholeOffsetX\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_CameraPinholeOffsetX => "DLSSG.CameraPinholeOffsetX"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_CameraPinholeOffsetY \"DLSSG.CameraPinholeOffsetY\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_CameraPinholeOffsetY => "DLSSG.CameraPinholeOffsetY"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_CameraPosX \"DLSSG.CameraPosX\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_CameraPosX => "DLSSG.CameraPosX"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_CameraPosY \"DLSSG.CameraPosY\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_CameraPosY => "DLSSG.CameraPosY"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_CameraPosZ \"DLSSG.CameraPosZ\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_CameraPosZ => "DLSSG.CameraPosZ"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_CameraUpX \"DLSSG.CameraUpX\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_CameraUpX => "DLSSG.CameraUpX"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_CameraUpY \"DLSSG.CameraUpY\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_CameraUpY => "DLSSG.CameraUpY"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_CameraUpZ \"DLSSG.CameraUpZ\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_CameraUpZ => "DLSSG.CameraUpZ"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_CameraRightX \"DLSSG.CameraRightX\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_CameraRightX => "DLSSG.CameraRightX"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_CameraRightY \"DLSSG.CameraRightY\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_CameraRightY => "DLSSG.CameraRightY"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_CameraRightZ \"DLSSG.CameraRightZ\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_CameraRightZ => "DLSSG.CameraRightZ"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_CameraFwdX \"DLSSG.CameraFwdX\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_CameraFwdX => "DLSSG.CameraFwdX"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_CameraFwdY \"DLSSG.CameraFwdY\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_CameraFwdY => "DLSSG.CameraFwdY"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_CameraFwdZ \"DLSSG.CameraFwdZ\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_CameraFwdZ => "DLSSG.CameraFwdZ"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_CameraNear \"DLSSG.CameraNear\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_CameraNear => "DLSSG.CameraNear"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_CameraFar \"DLSSG.CameraFar\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_CameraFar => "DLSSG.CameraFar"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_CameraFOV \"DLSSG.CameraFOV\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_CameraFOV => "DLSSG.CameraFOV"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_CameraAspectRatio \"DLSSG.CameraAspectRatio\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_CameraAspectRatio => "DLSSG.CameraAspectRatio"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_ColorBuffersHDR \"DLSSG.ColorBuffersHDR\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_ColorBuffersHDR => "DLSSG.ColorBuffersHDR"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_DepthInverted \"DLSSG.DepthInverted\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_DepthInverted => "DLSSG.DepthInverted"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_CameraMotionIncluded \"DLSSG.CameraMotionIncluded\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_CameraMotionIncluded => "DLSSG.CameraMotionIncluded"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_Reset \"DLSSG.Reset\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_Reset => "DLSSG.Reset"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_AutomodeOverrideReset \"DLSSG.AutomodeOverrideReset\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_AutomodeOverrideReset => "DLSSG.AutomodeOverrideReset"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_NotRenderingGameFrames \"DLSSG.NotRenderingGameFrames\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_NotRenderingGameFrames => "DLSSG.NotRenderingGameFrames"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_OrthoProjection \"DLSSG.OrthoProjection\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_OrthoProjection => "DLSSG.OrthoProjection"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_EstimateVRAMCallback \"DLSSG.EstimateVRAMCallback\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_EstimateVRAMCallback => "DLSSG.EstimateVRAMCallback"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_InvertXAxis \"DLSSG.InvertXAxis\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_InvertXAxis => "DLSSG.InvertXAxis"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_InvertYAxis \"DLSSG.InvertYAxis\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_InvertYAxis => "DLSSG.InvertYAxis"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_UserDebugText \"DLSSG.UserDebugText\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_UserDebugText => "DLSSG.UserDebugText"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_MvecInvalidValue \"DLSSG.MvecInvalidValue\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_MvecInvalidValue => "DLSSG.MvecInvalidValue"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_MvecDilated \"DLSSG.MvecDilated\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_MvecDilated => "DLSSG.MvecDilated"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_MvecJittered \"DLSSG.MvecJittered\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_MvecJittered => "DLSSG.MvecJittered"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_MenuDetectionEnabled \"DLSSG.MenuDetectionEnabled\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_MenuDetectionEnabled => "DLSSG.MenuDetectionEnabled"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_AsyncCreateEnabled \"DLSSG.AsyncCreateEnabled\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_AsyncCreateEnabled => "DLSSG.AsyncCreateEnabled"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_LinearizedDepth_Scale \"DLSSG.LinearizedDepth_Scale\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_LinearizedDepth_Scale => "DLSSG.LinearizedDepth_Scale"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_LinearizedDepth_NearFarPartition \"DLSSG.LinearizedDepth_NearFarPartition\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_LinearizedDepth_NearFarPartition => "DLSSG.LinearizedDepth_NearFarPartition"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_MinRelativeLinearDepthObjectSeparation \"DLSSG.MinRelativeLinearDepthObjectSeparation\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_MinRelativeLinearDepthObjectSeparation => "DLSSG.MinRelativeLinearDepthObjectSeparation"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_InputBackbufferSubrectBaseX \"DLSSG.InputBackbufferSubrectBaseX\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_InputBackbufferSubrectBaseX => "DLSSG.InputBackbufferSubrectBaseX"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_InputBackbufferSubrectBaseY \"DLSSG.InputBackbufferSubrectBaseY\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_InputBackbufferSubrectBaseY => "DLSSG.InputBackbufferSubrectBaseY"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_InputBackbufferSubrectWidth \"DLSSG.InputBackbufferSubrectWidth\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_InputBackbufferSubrectWidth => "DLSSG.InputBackbufferSubrectWidth"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_InputBackbufferSubrectHeight \"DLSSG.InputBackbufferSubrectHeight\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_InputBackbufferSubrectHeight => "DLSSG.InputBackbufferSubrectHeight"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_MVecsSubrectBaseX \"DLSSG.MVecsSubrectBaseX\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_MVecsSubrectBaseX => "DLSSG.MVecsSubrectBaseX"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_MVecsSubrectBaseY \"DLSSG.MVecsSubrectBaseY\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_MVecsSubrectBaseY => "DLSSG.MVecsSubrectBaseY"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_MVecsSubrectWidth \"DLSSG.MVecsSubrectWidth\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_MVecsSubrectWidth => "DLSSG.MVecsSubrectWidth"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_MVecsSubrectHeight \"DLSSG.MVecsSubrectHeight\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_MVecsSubrectHeight => "DLSSG.MVecsSubrectHeight"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_DepthSubrectBaseX \"DLSSG.DepthSubrectBaseX\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_DepthSubrectBaseX => "DLSSG.DepthSubrectBaseX"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_DepthSubrectBaseY \"DLSSG.DepthSubrectBaseY\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_DepthSubrectBaseY => "DLSSG.DepthSubrectBaseY"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_DepthSubrectWidth \"DLSSG.DepthSubrectWidth\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_DepthSubrectWidth => "DLSSG.DepthSubrectWidth"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_DepthSubrectHeight \"DLSSG.DepthSubrectHeight\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_DepthSubrectHeight => "DLSSG.DepthSubrectHeight"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_HUDLessSubrectBaseX \"DLSSG.HUDLessSubrectBaseX\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_HUDLessSubrectBaseX => "DLSSG.HUDLessSubrectBaseX"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_HUDLessSubrectBaseY \"DLSSG.HUDLessSubrectBaseY\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_HUDLessSubrectBaseY => "DLSSG.HUDLessSubrectBaseY"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_HUDLessSubrectWidth \"DLSSG.HUDLessSubrectWidth\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_HUDLessSubrectWidth => "DLSSG.HUDLessSubrectWidth"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_HUDLessSubrectHeight \"DLSSG.HUDLessSubrectHeight\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_HUDLessSubrectHeight => "DLSSG.HUDLessSubrectHeight"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_UISubrectBaseX \"DLSSG.UISubrectBaseX\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_UISubrectBaseX => "DLSSG.UISubrectBaseX"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_UISubrectBaseY \"DLSSG.UISubrectBaseY\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_UISubrectBaseY => "DLSSG.UISubrectBaseY"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_UISubrectWidth \"DLSSG.UISubrectWidth\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_UISubrectWidth => "DLSSG.UISubrectWidth"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_UISubrectHeight \"DLSSG.UISubrectHeight\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_UISubrectHeight => "DLSSG.UISubrectHeight"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_UIAlphaSubrectBaseX \"DLSSG.UIAlphaSubrectBaseX\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_UIAlphaSubrectBaseX => "DLSSG.UIAlphaSubrectBaseX"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_UIAlphaSubrectBaseY \"DLSSG.UIAlphaSubrectBaseY\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_UIAlphaSubrectBaseY => "DLSSG.UIAlphaSubrectBaseY"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_UIAlphaSubrectWidth \"DLSSG.UIAlphaSubrectWidth\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_UIAlphaSubrectWidth => "DLSSG.UIAlphaSubrectWidth"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_UIAlphaSubrectHeight \"DLSSG.UIAlphaSubrectHeight\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_UIAlphaSubrectHeight => "DLSSG.UIAlphaSubrectHeight"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_BidirectionalDistortionFieldSubrectBaseX \"DLSSG.BidirectionalDistortionFieldSubrectBaseX\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_BidirectionalDistortionFieldSubrectBaseX => "DLSSG.BidirectionalDistortionFieldSubrectBaseX"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_BidirectionalDistortionFieldSubrectBaseY \"DLSSG.BidirectionalDistortionFieldSubrectBaseY\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_BidirectionalDistortionFieldSubrectBaseY => "DLSSG.BidirectionalDistortionFieldSubrectBaseY"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_BidirectionalDistortionFieldSubrectWidth \"DLSSG.BidirectionalDistortionFieldSubrectWidth\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_BidirectionalDistortionFieldSubrectWidth => "DLSSG.BidirectionalDistortionFieldSubrectWidth"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_BidirectionalDistortionFieldSubrectHeight \"DLSSG.BidirectionalDistortionFieldSubrectHeight\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_BidirectionalDistortionFieldSubrectHeight => "DLSSG.BidirectionalDistortionFieldSubrectHeight"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_BidirectionalDistortionField_LowPrecision_IsLowPrecision \"DLSSG.BidirectionalDistortionFieldLowPrecision.IsLowPrecision\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_BidirectionalDistortionField_LowPrecision_IsLowPrecision => "DLSSG.BidirectionalDistortionFieldLowPrecision.IsLowPrecision"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_BidirectionalDistortionField_LowPrecision_Bias \"DLSSG.BidirectionalDistortionFieldLowPrecision.Bias\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_BidirectionalDistortionField_LowPrecision_Bias => "DLSSG.BidirectionalDistortionFieldLowPrecision.Bias"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_BidirectionalDistortionField_LowPrecision_Scale \"DLSSG.BidirectionalDistortionFieldLowPrecision.Scale\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_BidirectionalDistortionField_LowPrecision_Scale => "DLSSG.BidirectionalDistortionFieldLowPrecision.Scale"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_OutputInterpolatedSubrectBaseX \"DLSSG.OutputInterpolatedSubrectBaseX\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_OutputInterpolatedSubrectBaseX => "DLSSG.OutputInterpolatedSubrectBaseX"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_OutputInterpolatedSubrectBaseY \"DLSSG.OutputInterpolatedSubrectBaseY\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_OutputInterpolatedSubrectBaseY => "DLSSG.OutputInterpolatedSubrectBaseY"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_OutputInterpolatedSubrectWidth \"DLSSG.OutputInterpolatedSubrectWidth\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_OutputInterpolatedSubrectWidth => "DLSSG.OutputInterpolatedSubrectWidth"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_OutputInterpolatedSubrectHeight \"DLSSG.OutputInterpolatedSubrectHeight\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_OutputInterpolatedSubrectHeight => "DLSSG.OutputInterpolatedSubrectHeight"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_OutputRealSubrectBaseX \"DLSSG.OutputRealSubrectBaseX\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_OutputRealSubrectBaseX => "DLSSG.OutputRealSubrectBaseX"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_OutputRealSubrectBaseY \"DLSSG.OutputRealSubrectBaseY\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_OutputRealSubrectBaseY => "DLSSG.OutputRealSubrectBaseY"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_OutputRealSubrectWidth \"DLSSG.OutputRealSubrectWidth\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_OutputRealSubrectWidth => "DLSSG.OutputRealSubrectWidth"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_OutputRealSubrectHeight \"DLSSG.OutputRealSubrectHeight\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_OutputRealSubrectHeight => "DLSSG.OutputRealSubrectHeight"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_BackbufferSubrectBaseX \"DLSSG.BackbufferSubrectBaseX\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_BackbufferSubrectBaseX => "DLSSG.BackbufferSubrectBaseX"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_BackbufferSubrectBaseY \"DLSSG.BackbufferSubrectBaseY\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_BackbufferSubrectBaseY => "DLSSG.BackbufferSubrectBaseY"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_BackbufferSubrectWidth \"DLSSG.BackbufferSubrectWidth\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_BackbufferSubrectWidth => "DLSSG.BackbufferSubrectWidth"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_BackbufferSubrectHeight \"DLSSG.BackbufferSubrectHeight\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_BackbufferSubrectHeight => "DLSSG.BackbufferSubrectHeight"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_InternalWidth \"DLSSG.InternalWidth\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_InternalWidth => "DLSSG.InternalWidth"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_InternalHeight \"DLSSG.InternalHeight\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_InternalHeight => "DLSSG.InternalHeight"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_DynamicResolution \"DLSSG.DynamicResolution\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_DynamicResolution => "DLSSG.DynamicResolution"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_EvalFlags \"DLSSG.EvalFlags\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_EvalFlags => "DLSSG.EvalFlags"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_FullscreenMode \"DLSSG.FullscreenMode\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_FullscreenMode => "DLSSG.FullscreenMode"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_TargetFrameRate \"DLSSG.TargetFrameRate\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_TargetFrameRate => "DLSSG.TargetFrameRate"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_MultiFrameCountMax \"DLSSG.MultiFrameCountMax\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_MultiFrameCountMax => "DLSSG.MultiFrameCountMax"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_MultiFrameCount \"DLSSG.MultiFrameCount\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_MultiFrameCount => "DLSSG.MultiFrameCount"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_MultiFrameIndex \"DLSSG.MultiFrameIndex\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_MultiFrameIndex => "DLSSG.MultiFrameIndex"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_BackbufferFrameID \"DLSSG.BackbufferFrameID\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_BackbufferFrameID => "DLSSG.BackbufferFrameID"u8;

    [NativeTypeName("#define NVSDK_NGX_DLSSG_Parameter_UserInterfaceRecompositionEnabled \"DLSSG.UserInterfaceRecompositionEnabled\"")]
    public static ReadOnlySpan<byte> NVSDK_NGX_DLSSG_Parameter_UserInterfaceRecompositionEnabled => "DLSSG.UserInterfaceRecompositionEnabled"u8;
}
