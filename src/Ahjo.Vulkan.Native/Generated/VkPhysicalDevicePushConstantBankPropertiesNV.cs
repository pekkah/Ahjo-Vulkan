namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDevicePushConstantBankPropertiesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint maxGraphicsPushConstantBanks;

    [NativeTypeName("uint32_t")]
    public uint maxComputePushConstantBanks;

    [NativeTypeName("uint32_t")]
    public uint maxGraphicsPushDataBanks;

    [NativeTypeName("uint32_t")]
    public uint maxComputePushDataBanks;
}
