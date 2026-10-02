namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkGpaDeviceGetClockInfoAMD
{
    public VkStructureType sType;

    public void* pNext;

    public float memoryClockRatioToPeak;

    public float engineClockRatioToPeak;

    [NativeTypeName("uint32_t")]
    public uint memoryClockFrequency;

    [NativeTypeName("uint32_t")]
    public uint engineClockFrequency;
}
