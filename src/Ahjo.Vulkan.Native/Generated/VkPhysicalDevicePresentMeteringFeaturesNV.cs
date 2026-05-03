namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDevicePresentMeteringFeaturesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint presentMetering;
}
