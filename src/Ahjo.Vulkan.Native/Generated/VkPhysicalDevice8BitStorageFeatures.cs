namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDevice8BitStorageFeatures
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint storageBuffer8BitAccess;

    [NativeTypeName("VkBool32")]
    public uint uniformAndStorageBuffer8BitAccess;

    [NativeTypeName("VkBool32")]
    public uint storagePushConstant8;
}
