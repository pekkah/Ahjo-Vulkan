namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkResolveImageModeInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkResolveImageFlagsKHR")]
    public uint flags;

    public VkResolveModeFlagBits resolveMode;

    public VkResolveModeFlagBits stencilResolveMode;
}
