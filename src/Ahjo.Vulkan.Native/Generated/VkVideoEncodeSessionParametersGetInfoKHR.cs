namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeSessionParametersGetInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkVideoSessionParametersKHR")]
    public VkVideoSessionParametersKHR_T* videoSessionParameters;
}
