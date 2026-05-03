namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderUntypedPointersFeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderUntypedPointers;
}
