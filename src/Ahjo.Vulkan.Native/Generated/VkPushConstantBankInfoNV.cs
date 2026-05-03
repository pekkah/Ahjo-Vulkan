namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPushConstantBankInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint bank;
}
