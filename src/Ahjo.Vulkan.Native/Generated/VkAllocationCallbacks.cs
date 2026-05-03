namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkAllocationCallbacks
{
    public void* pUserData;

    [NativeTypeName("PFN_vkAllocationFunction")]
    public delegate* unmanaged[Stdcall]<void*, nuint, nuint, VkSystemAllocationScope, void*> pfnAllocation;

    [NativeTypeName("PFN_vkReallocationFunction")]
    public delegate* unmanaged[Stdcall]<void*, void*, nuint, nuint, VkSystemAllocationScope, void*> pfnReallocation;

    [NativeTypeName("PFN_vkFreeFunction")]
    public delegate* unmanaged[Stdcall]<void*, void*, void> pfnFree;

    [NativeTypeName("PFN_vkInternalAllocationNotification")]
    public delegate* unmanaged[Stdcall]<void*, nuint, VkInternalAllocationType, VkSystemAllocationScope, void> pfnInternalAllocation;

    [NativeTypeName("PFN_vkInternalFreeNotification")]
    public delegate* unmanaged[Stdcall]<void*, nuint, VkInternalAllocationType, VkSystemAllocationScope, void> pfnInternalFree;
}
