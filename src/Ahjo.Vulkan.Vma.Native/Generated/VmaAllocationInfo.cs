namespace Ahjo.Vulkan.Vma.Native;

public unsafe partial struct VmaAllocationInfo
{
    [NativeTypeName("uint32_t")]
    public uint memoryType;

    [NativeTypeName("VkDeviceMemory _Nullable")]
    public Ahjo.Vulkan.Native.VkDeviceMemory_T* deviceMemory;

    [NativeTypeName("VkDeviceSize")]
    public ulong offset;

    [NativeTypeName("VkDeviceSize")]
    public ulong size;

    [NativeTypeName("void * _Nullable")]
    public void* pMappedData;

    [NativeTypeName("void * _Nullable")]
    public void* pUserData;

    [NativeTypeName("const char * _Nullable")]
    public sbyte* pName;
}
