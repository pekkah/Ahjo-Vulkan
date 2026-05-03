namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceVariablePointersFeatures
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint variablePointersStorageBuffer;

    [NativeTypeName("VkBool32")]
    public uint variablePointers;
}
