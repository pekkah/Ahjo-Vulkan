using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineExecutableInternalRepresentationKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("char[256]")]
    public _name_e__FixedBuffer name;

    [NativeTypeName("char[256]")]
    public _description_e__FixedBuffer description;

    [NativeTypeName("VkBool32")]
    public uint isText;

    [NativeTypeName("size_t")]
    public nuint dataSize;

    public void* pData;

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
