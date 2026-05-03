namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineBinaryCreateInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("const VkPipelineBinaryKeysAndDataKHR *")]
    public VkPipelineBinaryKeysAndDataKHR* pKeysAndDataInfo;

    [NativeTypeName("VkPipeline")]
    public VkPipeline_T* pipeline;

    [NativeTypeName("const VkPipelineCreateInfoKHR *")]
    public VkPipelineCreateInfoKHR* pPipelineCreateInfo;
}
