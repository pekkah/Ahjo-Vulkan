namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkExternalFenceProperties
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkExternalFenceHandleTypeFlags")]
    public uint exportFromImportedHandleTypes;

    [NativeTypeName("VkExternalFenceHandleTypeFlags")]
    public uint compatibleHandleTypes;

    [NativeTypeName("VkExternalFenceFeatureFlags")]
    public uint externalFenceFeatures;
}
