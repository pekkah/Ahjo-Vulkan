namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceDeviceGeneratedCommandsPropertiesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint maxGraphicsShaderGroupCount;

    [NativeTypeName("uint32_t")]
    public uint maxIndirectSequenceCount;

    [NativeTypeName("uint32_t")]
    public uint maxIndirectCommandsTokenCount;

    [NativeTypeName("uint32_t")]
    public uint maxIndirectCommandsStreamCount;

    [NativeTypeName("uint32_t")]
    public uint maxIndirectCommandsTokenOffset;

    [NativeTypeName("uint32_t")]
    public uint maxIndirectCommandsStreamStride;

    [NativeTypeName("uint32_t")]
    public uint minSequencesCountBufferOffsetAlignment;

    [NativeTypeName("uint32_t")]
    public uint minSequencesIndexBufferOffsetAlignment;

    [NativeTypeName("uint32_t")]
    public uint minIndirectCommandsBufferOffsetAlignment;
}
