namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkMicromapCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkMicromapCreateFlagsEXT")]
    public uint createFlags;

    [NativeTypeName("VkBuffer")]
    public VkBuffer_T* buffer;

    [NativeTypeName("VkDeviceSize")]
    public ulong offset;

    [NativeTypeName("VkDeviceSize")]
    public ulong size;

    public VkMicromapTypeEXT type;

    [NativeTypeName("VkDeviceAddress")]
    public ulong deviceAddress;
}
