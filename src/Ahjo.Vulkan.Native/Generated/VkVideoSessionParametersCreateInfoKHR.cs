namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoSessionParametersCreateInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkVideoSessionParametersCreateFlagsKHR")]
    public uint flags;

    [NativeTypeName("VkVideoSessionParametersKHR")]
    public VkVideoSessionParametersKHR_T* videoSessionParametersTemplate;

    [NativeTypeName("VkVideoSessionKHR")]
    public VkVideoSessionKHR_T* videoSession;
}
