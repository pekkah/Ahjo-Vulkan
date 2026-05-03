namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderImageAtomicInt64FeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderImageInt64Atomics;

    [NativeTypeName("VkBool32")]
    public uint sparseImageInt64Atomics;
}
