using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceMeshShaderPropertiesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint maxDrawMeshTasksCount;

    [NativeTypeName("uint32_t")]
    public uint maxTaskWorkGroupInvocations;

    [NativeTypeName("uint32_t[3]")]
    public _maxTaskWorkGroupSize_e__FixedBuffer maxTaskWorkGroupSize;

    [NativeTypeName("uint32_t")]
    public uint maxTaskTotalMemorySize;

    [NativeTypeName("uint32_t")]
    public uint maxTaskOutputCount;

    [NativeTypeName("uint32_t")]
    public uint maxMeshWorkGroupInvocations;

    [NativeTypeName("uint32_t[3]")]
    public _maxMeshWorkGroupSize_e__FixedBuffer maxMeshWorkGroupSize;

    [NativeTypeName("uint32_t")]
    public uint maxMeshTotalMemorySize;

    [NativeTypeName("uint32_t")]
    public uint maxMeshOutputVertices;

    [NativeTypeName("uint32_t")]
    public uint maxMeshOutputPrimitives;

    [NativeTypeName("uint32_t")]
    public uint maxMeshMultiviewViewCount;

    [NativeTypeName("uint32_t")]
    public uint meshOutputPerVertexGranularity;

    [NativeTypeName("uint32_t")]
    public uint meshOutputPerPrimitiveGranularity;

    [InlineArray(3)]
    public partial struct _maxTaskWorkGroupSize_e__FixedBuffer
    {
        public uint e0;
    }

    [InlineArray(3)]
    public partial struct _maxMeshWorkGroupSize_e__FixedBuffer
    {
        public uint e0;
    }
}
