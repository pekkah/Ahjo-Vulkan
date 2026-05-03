using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceMemoryBudgetPropertiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkDeviceSize[16]")]
    public _heapBudget_e__FixedBuffer heapBudget;

    [NativeTypeName("VkDeviceSize[16]")]
    public _heapUsage_e__FixedBuffer heapUsage;

    [InlineArray(16)]
    public partial struct _heapBudget_e__FixedBuffer
    {
        public ulong e0;
    }

    [InlineArray(16)]
    public partial struct _heapUsage_e__FixedBuffer
    {
        public ulong e0;
    }
}
