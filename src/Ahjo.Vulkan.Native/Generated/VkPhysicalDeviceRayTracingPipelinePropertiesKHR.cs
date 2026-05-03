namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceRayTracingPipelinePropertiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint shaderGroupHandleSize;

    [NativeTypeName("uint32_t")]
    public uint maxRayRecursionDepth;

    [NativeTypeName("uint32_t")]
    public uint maxShaderGroupStride;

    [NativeTypeName("uint32_t")]
    public uint shaderGroupBaseAlignment;

    [NativeTypeName("uint32_t")]
    public uint shaderGroupHandleCaptureReplaySize;

    [NativeTypeName("uint32_t")]
    public uint maxRayDispatchInvocationCount;

    [NativeTypeName("uint32_t")]
    public uint shaderGroupHandleAlignment;

    [NativeTypeName("uint32_t")]
    public uint maxRayHitAttributeSize;
}
