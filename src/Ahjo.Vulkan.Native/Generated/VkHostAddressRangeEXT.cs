namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkHostAddressRangeEXT
{
    public void* address;

    [NativeTypeName("size_t")]
    public nuint size;
}
