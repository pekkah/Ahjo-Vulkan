namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDevice16BitStorageFeatures
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint storageBuffer16BitAccess;

    [NativeTypeName("VkBool32")]
    public uint uniformAndStorageBuffer16BitAccess;

    [NativeTypeName("VkBool32")]
    public uint storagePushConstant16;

    [NativeTypeName("VkBool32")]
    public uint storageInputOutput16;
}
