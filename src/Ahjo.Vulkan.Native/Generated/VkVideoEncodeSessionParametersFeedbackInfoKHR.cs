namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeSessionParametersFeedbackInfoKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint hasOverrides;
}
