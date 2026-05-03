namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDisplayModeCreateInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDisplayModeCreateFlagsKHR")]
    public uint flags;

    public VkDisplayModeParametersKHR parameters;
}
