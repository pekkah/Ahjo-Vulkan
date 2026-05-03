namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineIndirectDeviceAddressInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkPipelineBindPoint pipelineBindPoint;

    [NativeTypeName("VkPipeline")]
    public VkPipeline_T* pipeline;
}
