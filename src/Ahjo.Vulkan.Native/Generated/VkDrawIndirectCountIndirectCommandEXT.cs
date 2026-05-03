namespace Ahjo.Vulkan.Native;

public partial struct VkDrawIndirectCountIndirectCommandEXT
{
    [NativeTypeName("VkDeviceAddress")]
    public ulong bufferAddress;

    [NativeTypeName("uint32_t")]
    public uint stride;

    [NativeTypeName("uint32_t")]
    public uint commandCount;
}
