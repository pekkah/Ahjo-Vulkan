namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineBinaryDataKHR
{
    [NativeTypeName("size_t")]
    public nuint dataSize;

    public void* pData;
}
