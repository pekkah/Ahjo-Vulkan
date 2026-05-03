namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceMultiviewPerViewRenderAreasFeaturesQCOM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint multiviewPerViewRenderAreas;
}
