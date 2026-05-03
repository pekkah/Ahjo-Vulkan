namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineBinaryKeysAndDataKHR
{
    [NativeTypeName("uint32_t")]
    public uint binaryCount;

    [NativeTypeName("const VkPipelineBinaryKeyKHR *")]
    public VkPipelineBinaryKeyKHR* pPipelineBinaryKeys;

    [NativeTypeName("const VkPipelineBinaryDataKHR *")]
    public VkPipelineBinaryDataKHR* pPipelineBinaryData;
}
