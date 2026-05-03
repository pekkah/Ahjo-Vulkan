namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDevicePresentBarrierFeaturesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint presentBarrier;
}
