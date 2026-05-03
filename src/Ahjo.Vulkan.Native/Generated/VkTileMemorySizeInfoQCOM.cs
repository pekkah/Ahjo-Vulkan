namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkTileMemorySizeInfoQCOM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDeviceSize")]
    public ulong size;
}
