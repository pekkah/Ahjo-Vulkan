namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDirectDriverLoadingInfoLUNARG
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkDirectDriverLoadingFlagsLUNARG")]
    public uint flags;

    [NativeTypeName("PFN_vkGetInstanceProcAddrLUNARG")]
    public delegate* unmanaged[Stdcall]<VkInstance_T*, sbyte*, delegate* unmanaged[Stdcall]<void>> pfnGetInstanceProcAddr;
}
