using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelinePropertiesIdentifierEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint8_t[16]")]
    public _pipelineIdentifier_e__FixedBuffer pipelineIdentifier;

    [InlineArray(16)]
    public partial struct _pipelineIdentifier_e__FixedBuffer
    {
        public byte e0;
    }
}
