namespace Ahjo.Vulkan.Vma.Native;

public unsafe partial struct VmaDeviceMemoryCallbacks
{
    [NativeTypeName("PFN_vmaAllocateDeviceMemoryFunction _Nullable")]
    public delegate* unmanaged[Stdcall]<VmaAllocator_T*, uint, Ahjo.Vulkan.Native.VkDeviceMemory_T*, ulong, void*, void> pfnAllocate;

    [NativeTypeName("PFN_vmaFreeDeviceMemoryFunction _Nullable")]
    public delegate* unmanaged[Stdcall]<VmaAllocator_T*, uint, Ahjo.Vulkan.Native.VkDeviceMemory_T*, ulong, void*, void> pfnFree;

    [NativeTypeName("void * _Nullable")]
    public void* pUserData;
}
