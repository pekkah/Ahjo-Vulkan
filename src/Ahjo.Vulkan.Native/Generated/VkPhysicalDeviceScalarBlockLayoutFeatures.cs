namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceScalarBlockLayoutFeatures
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint scalarBlockLayout;
}
