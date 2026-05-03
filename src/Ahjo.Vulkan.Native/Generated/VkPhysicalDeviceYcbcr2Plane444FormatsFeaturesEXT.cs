namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceYcbcr2Plane444FormatsFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint ycbcr2plane444Formats;
}
