namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceFaultFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint deviceFault;

    [NativeTypeName("VkBool32")]
    public uint deviceFaultVendorBinary;
}
