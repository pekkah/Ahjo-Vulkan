namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceCooperativeMatrixFeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint cooperativeMatrix;

    [NativeTypeName("VkBool32")]
    public uint cooperativeMatrixRobustBufferAccess;
}
