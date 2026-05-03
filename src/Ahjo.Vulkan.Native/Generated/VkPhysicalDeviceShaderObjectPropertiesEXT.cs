using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderObjectPropertiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint8_t[16]")]
    public _shaderBinaryUUID_e__FixedBuffer shaderBinaryUUID;

    [NativeTypeName("uint32_t")]
    public uint shaderBinaryVersion;

    [InlineArray(16)]
    public partial struct _shaderBinaryUUID_e__FixedBuffer
    {
        public byte e0;
    }
}
