namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkGpaDeviceClockModeInfoAMD
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkGpaDeviceClockModeAMD clockMode;

    public float memoryClockRatioToPeak;

    public float engineClockRatioToPeak;
}
