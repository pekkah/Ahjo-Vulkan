namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceDeviceMemoryReportFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint deviceMemoryReport;
}
