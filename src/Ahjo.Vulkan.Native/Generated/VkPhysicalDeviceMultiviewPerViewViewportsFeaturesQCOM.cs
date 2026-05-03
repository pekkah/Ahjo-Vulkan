namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceMultiviewPerViewViewportsFeaturesQCOM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint multiviewPerViewViewports;
}
