namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkHeadlessSurfaceCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkHeadlessSurfaceCreateFlagsEXT")]
    public uint flags;
}
