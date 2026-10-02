namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSwapchainFlagsSurfaceCapabilitiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkSwapchainCreateFlagsKHR")]
    public uint swapchainSupportedFlags;
}
