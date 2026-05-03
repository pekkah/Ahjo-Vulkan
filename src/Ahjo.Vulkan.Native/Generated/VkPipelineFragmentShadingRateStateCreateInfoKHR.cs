using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineFragmentShadingRateStateCreateInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkExtent2D fragmentSize;

    [NativeTypeName("VkFragmentShadingRateCombinerOpKHR[2]")]
    public _combinerOps_e__FixedBuffer combinerOps;

    [InlineArray(2)]
    public partial struct _combinerOps_e__FixedBuffer
    {
        public VkFragmentShadingRateCombinerOpKHR e0;
    }
}
