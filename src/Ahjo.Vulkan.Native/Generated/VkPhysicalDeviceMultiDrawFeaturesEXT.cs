namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceMultiDrawFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint multiDraw;
}
