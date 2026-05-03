namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineBinaryInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint binaryCount;

    [NativeTypeName("const VkPipelineBinaryKHR *")]
    public VkPipelineBinaryKHR_T** pPipelineBinaries;
}
