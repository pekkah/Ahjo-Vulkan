using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct VkShaderStatisticsInfoAMD
{
    [NativeTypeName("VkShaderStageFlags")]
    public uint shaderStageMask;

    public VkShaderResourceUsageAMD resourceUsage;

    [NativeTypeName("uint32_t")]
    public uint numPhysicalVgprs;

    [NativeTypeName("uint32_t")]
    public uint numPhysicalSgprs;

    [NativeTypeName("uint32_t")]
    public uint numAvailableVgprs;

    [NativeTypeName("uint32_t")]
    public uint numAvailableSgprs;

    [NativeTypeName("uint32_t[3]")]
    public _computeWorkGroupSize_e__FixedBuffer computeWorkGroupSize;

    [InlineArray(3)]
    public partial struct _computeWorkGroupSize_e__FixedBuffer
    {
        public uint e0;
    }
}
