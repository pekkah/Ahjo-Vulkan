namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDevicePointClippingProperties
{
    public VkStructureType sType;

    public void* pNext;

    public VkPointClippingBehavior pointClippingBehavior;
}
