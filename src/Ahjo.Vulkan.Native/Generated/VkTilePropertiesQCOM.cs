namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkTilePropertiesQCOM
{
    public VkStructureType sType;

    public void* pNext;

    public VkExtent3D tileSize;

    public VkExtent2D apronSize;

    public VkOffset2D origin;
}
