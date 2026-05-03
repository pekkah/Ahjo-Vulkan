using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceVulkan11Properties
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint8_t[16]")]
    public _deviceUUID_e__FixedBuffer deviceUUID;

    [NativeTypeName("uint8_t[16]")]
    public _driverUUID_e__FixedBuffer driverUUID;

    [NativeTypeName("uint8_t[8]")]
    public _deviceLUID_e__FixedBuffer deviceLUID;

    [NativeTypeName("uint32_t")]
    public uint deviceNodeMask;

    [NativeTypeName("VkBool32")]
    public uint deviceLUIDValid;

    [NativeTypeName("uint32_t")]
    public uint subgroupSize;

    [NativeTypeName("VkShaderStageFlags")]
    public uint subgroupSupportedStages;

    [NativeTypeName("VkSubgroupFeatureFlags")]
    public uint subgroupSupportedOperations;

    [NativeTypeName("VkBool32")]
    public uint subgroupQuadOperationsInAllStages;

    public VkPointClippingBehavior pointClippingBehavior;

    [NativeTypeName("uint32_t")]
    public uint maxMultiviewViewCount;

    [NativeTypeName("uint32_t")]
    public uint maxMultiviewInstanceIndex;

    [NativeTypeName("VkBool32")]
    public uint protectedNoFault;

    [NativeTypeName("uint32_t")]
    public uint maxPerSetDescriptors;

    [NativeTypeName("VkDeviceSize")]
    public ulong maxMemoryAllocationSize;

    [InlineArray(16)]
    public partial struct _deviceUUID_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(16)]
    public partial struct _driverUUID_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(8)]
    public partial struct _deviceLUID_e__FixedBuffer
    {
        public byte e0;
    }
}
