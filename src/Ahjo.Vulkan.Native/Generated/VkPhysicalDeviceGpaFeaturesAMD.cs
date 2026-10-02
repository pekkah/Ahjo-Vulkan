namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceGpaFeaturesAMD
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint perfCounters;

    [NativeTypeName("VkBool32")]
    public uint streamingPerfCounters;

    [NativeTypeName("VkBool32")]
    public uint sqThreadTracing;

    [NativeTypeName("VkBool32")]
    public uint clockModes;
}
