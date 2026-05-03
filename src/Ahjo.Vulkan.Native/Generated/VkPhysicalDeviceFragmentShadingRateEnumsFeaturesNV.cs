namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceFragmentShadingRateEnumsFeaturesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint fragmentShadingRateEnums;

    [NativeTypeName("VkBool32")]
    public uint supersampleFragmentShadingRates;

    [NativeTypeName("VkBool32")]
    public uint noInvocationFragmentShadingRates;
}
