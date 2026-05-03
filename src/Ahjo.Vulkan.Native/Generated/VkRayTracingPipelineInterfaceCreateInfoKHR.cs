namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkRayTracingPipelineInterfaceCreateInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint maxPipelineRayPayloadSize;

    [NativeTypeName("uint32_t")]
    public uint maxPipelineRayHitAttributeSize;
}
