namespace Ahjo.Vulkan.Ngx.Native;

public unsafe partial struct NVSDK_NGX_BufferInfo_VK
{
    [NativeTypeName("VkBuffer")]
    public Ahjo.Vulkan.Native.VkBuffer_T* Buffer;

    [NativeTypeName("unsigned int")]
    public uint SizeInBytes;
}
