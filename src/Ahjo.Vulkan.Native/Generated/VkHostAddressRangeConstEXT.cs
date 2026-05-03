namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkHostAddressRangeConstEXT
{
    [NativeTypeName("const void *")]
    public void* address;

    [NativeTypeName("size_t")]
    public nuint size;
}
