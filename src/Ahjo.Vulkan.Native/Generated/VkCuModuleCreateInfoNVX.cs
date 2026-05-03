namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCuModuleCreateInfoNVX
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("size_t")]
    public nuint dataSize;

    [NativeTypeName("const void *")]
    public void* pData;
}
