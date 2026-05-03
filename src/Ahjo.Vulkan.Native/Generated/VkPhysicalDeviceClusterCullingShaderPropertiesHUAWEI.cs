using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceClusterCullingShaderPropertiesHUAWEI
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t[3]")]
    public _maxWorkGroupCount_e__FixedBuffer maxWorkGroupCount;

    [NativeTypeName("uint32_t[3]")]
    public _maxWorkGroupSize_e__FixedBuffer maxWorkGroupSize;

    [NativeTypeName("uint32_t")]
    public uint maxOutputClusterCount;

    [NativeTypeName("VkDeviceSize")]
    public ulong indirectBufferOffsetAlignment;

    [InlineArray(3)]
    public partial struct _maxWorkGroupCount_e__FixedBuffer
    {
        public uint e0;
    }

    [InlineArray(3)]
    public partial struct _maxWorkGroupSize_e__FixedBuffer
    {
        public uint e0;
    }
}
