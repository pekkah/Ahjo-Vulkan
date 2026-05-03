namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderAtomicInt64Features
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderBufferInt64Atomics;

    [NativeTypeName("VkBool32")]
    public uint shaderSharedInt64Atomics;
}
