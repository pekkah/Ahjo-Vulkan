namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceWorkgroupMemoryExplicitLayoutFeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint workgroupMemoryExplicitLayout;

    [NativeTypeName("VkBool32")]
    public uint workgroupMemoryExplicitLayoutScalarBlockLayout;

    [NativeTypeName("VkBool32")]
    public uint workgroupMemoryExplicitLayout8BitAccess;

    [NativeTypeName("VkBool32")]
    public uint workgroupMemoryExplicitLayout16BitAccess;
}
