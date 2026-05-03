using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkShaderModuleIdentifierEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint identifierSize;

    [NativeTypeName("uint8_t[32]")]
    public _identifier_e__FixedBuffer identifier;

    [InlineArray(32)]
    public partial struct _identifier_e__FixedBuffer
    {
        public byte e0;
    }
}
