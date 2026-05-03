namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCuLaunchInfoNVX
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkCuFunctionNVX")]
    public VkCuFunctionNVX_T* function;

    [NativeTypeName("uint32_t")]
    public uint gridDimX;

    [NativeTypeName("uint32_t")]
    public uint gridDimY;

    [NativeTypeName("uint32_t")]
    public uint gridDimZ;

    [NativeTypeName("uint32_t")]
    public uint blockDimX;

    [NativeTypeName("uint32_t")]
    public uint blockDimY;

    [NativeTypeName("uint32_t")]
    public uint blockDimZ;

    [NativeTypeName("uint32_t")]
    public uint sharedMemBytes;

    [NativeTypeName("size_t")]
    public nuint paramCount;

    [NativeTypeName("const void *const *")]
    public void** pParams;

    [NativeTypeName("size_t")]
    public nuint extraCount;

    [NativeTypeName("const void *const *")]
    public void** pExtras;
}
