namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkOpticalFlowSessionCreatePrivateDataInfoNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint id;

    [NativeTypeName("uint32_t")]
    public uint size;

    [NativeTypeName("const void *")]
    public void* pPrivateData;
}
