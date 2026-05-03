namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceMultisampledRenderToSingleSampledFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint multisampledRenderToSingleSampled;
}
