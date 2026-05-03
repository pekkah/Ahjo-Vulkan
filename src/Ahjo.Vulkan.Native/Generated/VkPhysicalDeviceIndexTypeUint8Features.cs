namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceIndexTypeUint8Features
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint indexTypeUint8;
}
