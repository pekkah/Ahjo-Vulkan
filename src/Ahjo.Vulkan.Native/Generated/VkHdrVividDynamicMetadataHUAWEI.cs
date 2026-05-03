namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkHdrVividDynamicMetadataHUAWEI
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("size_t")]
    public nuint dynamicMetadataSize;

    [NativeTypeName("const void *")]
    public void* pDynamicMetadata;
}
