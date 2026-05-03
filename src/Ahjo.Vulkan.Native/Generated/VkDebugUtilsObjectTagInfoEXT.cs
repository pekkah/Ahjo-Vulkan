namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDebugUtilsObjectTagInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkObjectType objectType;

    [NativeTypeName("uint64_t")]
    public ulong objectHandle;

    [NativeTypeName("uint64_t")]
    public ulong tagName;

    [NativeTypeName("size_t")]
    public nuint tagSize;

    [NativeTypeName("const void *")]
    public void* pTag;
}
