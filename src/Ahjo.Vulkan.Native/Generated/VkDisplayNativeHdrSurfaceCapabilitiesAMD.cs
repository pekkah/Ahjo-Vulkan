namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDisplayNativeHdrSurfaceCapabilitiesAMD
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint localDimmingSupport;
}
