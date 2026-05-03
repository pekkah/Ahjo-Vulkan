namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSetPresentConfigNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint numFramesPerBatch;

    [NativeTypeName("uint32_t")]
    public uint presentConfigFeedback;
}
