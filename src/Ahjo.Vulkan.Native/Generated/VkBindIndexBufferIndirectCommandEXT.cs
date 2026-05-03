namespace Ahjo.Vulkan.Native;

public partial struct VkBindIndexBufferIndirectCommandEXT
{
    [NativeTypeName("VkDeviceAddress")]
    public ulong bufferAddress;

    [NativeTypeName("uint32_t")]
    public uint size;

    public VkIndexType indexType;
}
