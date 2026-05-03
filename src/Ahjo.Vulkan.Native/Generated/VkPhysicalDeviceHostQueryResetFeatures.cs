namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceHostQueryResetFeatures
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint hostQueryReset;
}
