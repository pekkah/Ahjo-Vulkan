using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPerformanceCounterKHR
{
    public VkStructureType sType;

    public void* pNext;

    public VkPerformanceCounterUnitKHR unit;

    public VkPerformanceCounterScopeKHR scope;

    public VkPerformanceCounterStorageKHR storage;

    [NativeTypeName("uint8_t[16]")]
    public _uuid_e__FixedBuffer uuid;

    [InlineArray(16)]
    public partial struct _uuid_e__FixedBuffer
    {
        public byte e0;
    }
}
