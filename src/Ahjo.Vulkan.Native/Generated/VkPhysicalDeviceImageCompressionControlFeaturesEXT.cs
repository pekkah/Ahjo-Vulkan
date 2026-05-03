namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceImageCompressionControlFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint imageCompressionControl;
}
