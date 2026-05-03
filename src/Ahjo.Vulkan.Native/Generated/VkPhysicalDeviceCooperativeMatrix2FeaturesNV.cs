namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceCooperativeMatrix2FeaturesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint cooperativeMatrixWorkgroupScope;

    [NativeTypeName("VkBool32")]
    public uint cooperativeMatrixFlexibleDimensions;

    [NativeTypeName("VkBool32")]
    public uint cooperativeMatrixReductions;

    [NativeTypeName("VkBool32")]
    public uint cooperativeMatrixConversions;

    [NativeTypeName("VkBool32")]
    public uint cooperativeMatrixPerElementOperations;

    [NativeTypeName("VkBool32")]
    public uint cooperativeMatrixTensorAddressing;

    [NativeTypeName("VkBool32")]
    public uint cooperativeMatrixBlockLoads;
}
