namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderAbortPropertiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint64_t")]
    public ulong maxShaderAbortMessageSize;
}
