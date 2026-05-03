namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceAccelerationStructureFeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint accelerationStructure;

    [NativeTypeName("VkBool32")]
    public uint accelerationStructureCaptureReplay;

    [NativeTypeName("VkBool32")]
    public uint accelerationStructureIndirectBuild;

    [NativeTypeName("VkBool32")]
    public uint accelerationStructureHostCommands;

    [NativeTypeName("VkBool32")]
    public uint descriptorBindingAccelerationStructureUpdateAfterBind;
}
