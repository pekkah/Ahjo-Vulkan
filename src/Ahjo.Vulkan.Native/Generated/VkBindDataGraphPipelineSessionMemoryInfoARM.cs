namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkBindDataGraphPipelineSessionMemoryInfoARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDataGraphPipelineSessionARM")]
    public VkDataGraphPipelineSessionARM_T* session;

    public VkDataGraphPipelineSessionBindPointARM bindPoint;

    [NativeTypeName("uint32_t")]
    public uint objectIndex;

    [NativeTypeName("VkDeviceMemory")]
    public VkDeviceMemory_T* memory;

    [NativeTypeName("VkDeviceSize")]
    public ulong memoryOffset;
}
