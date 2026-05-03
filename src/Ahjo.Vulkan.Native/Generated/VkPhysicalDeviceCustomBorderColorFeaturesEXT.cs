namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceCustomBorderColorFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint customBorderColors;

    [NativeTypeName("VkBool32")]
    public uint customBorderColorWithoutFormat;
}
