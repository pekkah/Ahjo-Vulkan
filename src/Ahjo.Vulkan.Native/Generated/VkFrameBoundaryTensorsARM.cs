namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkFrameBoundaryTensorsARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint tensorCount;

    [NativeTypeName("const VkTensorARM *")]
    public VkTensorARM_T** pTensors;
}
