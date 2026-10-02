namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceFaultFeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint deviceFault;

    [NativeTypeName("VkBool32")]
    public uint deviceFaultVendorBinary;

    [NativeTypeName("VkBool32")]
    public uint deviceFaultReportMasked;

    [NativeTypeName("VkBool32")]
    public uint deviceFaultDeviceLostOnMasked;
}
