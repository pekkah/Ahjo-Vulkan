namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDecompressMemoryInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkMemoryDecompressionMethodFlagsEXT")]
    public ulong decompressionMethod;

    [NativeTypeName("uint32_t")]
    public uint regionCount;

    [NativeTypeName("const VkDecompressMemoryRegionEXT *")]
    public VkDecompressMemoryRegionEXT* pRegions;
}
