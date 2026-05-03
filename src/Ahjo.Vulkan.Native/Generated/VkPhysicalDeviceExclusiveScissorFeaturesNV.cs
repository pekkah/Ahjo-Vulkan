namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceExclusiveScissorFeaturesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint exclusiveScissor;
}
