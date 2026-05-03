namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkRenderPassTileShadingCreateInfoQCOM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkTileShadingRenderPassFlagsQCOM")]
    public uint flags;

    public VkExtent2D tileApronSize;
}
