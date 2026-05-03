namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkValidationCacheCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkValidationCacheCreateFlagsEXT")]
    public uint flags;

    [NativeTypeName("size_t")]
    public nuint initialDataSize;

    [NativeTypeName("const void *")]
    public void* pInitialData;
}
