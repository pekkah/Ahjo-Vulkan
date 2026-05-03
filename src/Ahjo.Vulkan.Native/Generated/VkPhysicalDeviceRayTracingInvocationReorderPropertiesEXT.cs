namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceRayTracingInvocationReorderPropertiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    public VkRayTracingInvocationReorderModeEXT rayTracingInvocationReorderReorderingHint;

    [NativeTypeName("uint32_t")]
    public uint maxShaderBindingTableRecordIndex;
}
