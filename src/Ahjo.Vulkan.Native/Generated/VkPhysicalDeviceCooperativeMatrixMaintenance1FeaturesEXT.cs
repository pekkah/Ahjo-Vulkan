namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceCooperativeMatrixMaintenance1FeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint cooperativeMatrixProperties2;

    [NativeTypeName("VkBool32")]
    public uint cooperativeMatrixReductions;

    [NativeTypeName("VkBool32")]
    public uint cooperativeMatrixConversions;

    [NativeTypeName("VkBool32")]
    public uint cooperativeMatrixPerElementOperations;

    [NativeTypeName("VkBool32")]
    public uint cooperativeMatrixGetCoordinate;
}
