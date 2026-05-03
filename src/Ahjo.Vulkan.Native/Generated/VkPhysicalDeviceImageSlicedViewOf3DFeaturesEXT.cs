namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceImageSlicedViewOf3DFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint imageSlicedViewOf3D;
}
