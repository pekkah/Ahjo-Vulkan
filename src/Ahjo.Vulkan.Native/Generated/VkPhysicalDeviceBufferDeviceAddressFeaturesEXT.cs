namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceBufferDeviceAddressFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint bufferDeviceAddress;

    [NativeTypeName("VkBool32")]
    public uint bufferDeviceAddressCaptureReplay;

    [NativeTypeName("VkBool32")]
    public uint bufferDeviceAddressMultiDevice;
}
