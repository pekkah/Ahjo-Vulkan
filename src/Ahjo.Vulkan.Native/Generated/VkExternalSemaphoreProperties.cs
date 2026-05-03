namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkExternalSemaphoreProperties
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkExternalSemaphoreHandleTypeFlags")]
    public uint exportFromImportedHandleTypes;

    [NativeTypeName("VkExternalSemaphoreHandleTypeFlags")]
    public uint compatibleHandleTypes;

    [NativeTypeName("VkExternalSemaphoreFeatureFlags")]
    public uint externalSemaphoreFeatures;
}
