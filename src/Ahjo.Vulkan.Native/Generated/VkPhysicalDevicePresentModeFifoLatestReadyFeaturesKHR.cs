namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDevicePresentModeFifoLatestReadyFeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint presentModeFifoLatestReady;
}
