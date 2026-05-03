namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderTerminateInvocationFeatures
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderTerminateInvocation;
}
