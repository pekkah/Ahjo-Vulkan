namespace Ahjo.Vulkan.Native;

public partial struct VkBindVertexBufferIndirectCommandNV
{
    [NativeTypeName("VkDeviceAddress")]
    public ulong bufferAddress;

    [NativeTypeName("uint32_t")]
    public uint size;

    [NativeTypeName("uint32_t")]
    public uint stride;
}
