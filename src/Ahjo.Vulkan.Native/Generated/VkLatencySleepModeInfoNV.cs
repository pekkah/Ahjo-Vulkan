namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkLatencySleepModeInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint lowLatencyMode;

    [NativeTypeName("VkBool32")]
    public uint lowLatencyBoost;

    [NativeTypeName("uint32_t")]
    public uint minimumIntervalUs;
}
