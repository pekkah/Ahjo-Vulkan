namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSurfaceProtectedCapabilitiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint supportsProtected;
}
