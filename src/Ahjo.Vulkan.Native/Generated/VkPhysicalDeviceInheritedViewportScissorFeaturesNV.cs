namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceInheritedViewportScissorFeaturesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint inheritedViewportScissor2D;
}
