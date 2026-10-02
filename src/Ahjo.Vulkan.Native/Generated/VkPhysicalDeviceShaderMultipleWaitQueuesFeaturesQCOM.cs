namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderMultipleWaitQueuesFeaturesQCOM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderMultipleWaitQueues;
}
