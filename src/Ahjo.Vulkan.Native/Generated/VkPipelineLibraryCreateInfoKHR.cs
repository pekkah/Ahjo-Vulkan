namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineLibraryCreateInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint libraryCount;

    [NativeTypeName("const VkPipeline *")]
    public VkPipeline_T** pLibraries;
}
