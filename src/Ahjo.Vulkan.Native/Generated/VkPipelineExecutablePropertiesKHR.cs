using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineExecutablePropertiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkShaderStageFlags")]
    public uint stages;

    [NativeTypeName("char[256]")]
    public _name_e__FixedBuffer name;

    [NativeTypeName("char[256]")]
    public _description_e__FixedBuffer description;

    [NativeTypeName("uint32_t")]
    public uint subgroupSize;

    [InlineArray(256)]
    public partial struct _name_e__FixedBuffer
    {
        public sbyte e0;
    }

    [InlineArray(256)]
    public partial struct _description_e__FixedBuffer
    {
        public sbyte e0;
    }
}
