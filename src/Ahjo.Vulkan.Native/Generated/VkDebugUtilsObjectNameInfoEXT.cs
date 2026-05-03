namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDebugUtilsObjectNameInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkObjectType objectType;

    [NativeTypeName("uint64_t")]
    public ulong objectHandle;

    [NativeTypeName("const char *")]
    public sbyte* pObjectName;
}
