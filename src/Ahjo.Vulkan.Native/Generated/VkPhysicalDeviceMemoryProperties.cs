using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct VkPhysicalDeviceMemoryProperties
{
    [NativeTypeName("uint32_t")]
    public uint memoryTypeCount;

    [NativeTypeName("VkMemoryType[32]")]
    public _memoryTypes_e__FixedBuffer memoryTypes;

    [NativeTypeName("uint32_t")]
    public uint memoryHeapCount;

    [NativeTypeName("VkMemoryHeap[16]")]
    public _memoryHeaps_e__FixedBuffer memoryHeaps;

    [InlineArray(32)]
    public partial struct _memoryTypes_e__FixedBuffer
    {
        public VkMemoryType e0;
    }

    [InlineArray(16)]
    public partial struct _memoryHeaps_e__FixedBuffer
    {
        public VkMemoryHeap e0;
    }
}
