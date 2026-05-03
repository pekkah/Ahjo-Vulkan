namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceTileMemoryHeapPropertiesQCOM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint queueSubmitBoundary;

    [NativeTypeName("VkBool32")]
    public uint tileBufferTransfers;
}
