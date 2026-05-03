using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineBinaryKeyKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint keySize;

    [NativeTypeName("uint8_t[32]")]
    public _key_e__FixedBuffer key;

    [InlineArray(32)]
    public partial struct _key_e__FixedBuffer
    {
        public byte e0;
    }
}
