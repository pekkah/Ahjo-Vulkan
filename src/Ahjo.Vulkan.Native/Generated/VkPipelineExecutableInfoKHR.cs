namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineExecutableInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipeline")]
    public VkPipeline_T* pipeline;

    [NativeTypeName("uint32_t")]
    public uint executableIndex;
}
