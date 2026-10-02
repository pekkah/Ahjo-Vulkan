namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDeviceFaultShaderAbortMessageInfoKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint64_t")]
    public ulong messageDataSize;

    public void* pMessageData;
}
