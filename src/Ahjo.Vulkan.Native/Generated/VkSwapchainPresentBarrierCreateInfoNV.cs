namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSwapchainPresentBarrierCreateInfoNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint presentBarrierEnable;
}
