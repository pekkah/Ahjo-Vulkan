namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDeviceAddressBindingCallbackDataEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkDeviceAddressBindingFlagsEXT")]
    public uint flags;

    [NativeTypeName("VkDeviceAddress")]
    public ulong baseAddress;

    [NativeTypeName("VkDeviceSize")]
    public ulong size;

    public VkDeviceAddressBindingTypeEXT bindingType;
}
