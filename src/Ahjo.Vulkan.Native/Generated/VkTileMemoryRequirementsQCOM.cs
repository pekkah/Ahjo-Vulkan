namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkTileMemoryRequirementsQCOM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkDeviceSize")]
    public ulong size;

    [NativeTypeName("VkDeviceSize")]
    public ulong alignment;
}
