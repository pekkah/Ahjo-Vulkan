using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceSampleLocationsPropertiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkSampleCountFlags")]
    public uint sampleLocationSampleCounts;

    public VkExtent2D maxSampleLocationGridSize;

    [NativeTypeName("float[2]")]
    public _sampleLocationCoordinateRange_e__FixedBuffer sampleLocationCoordinateRange;

    [NativeTypeName("uint32_t")]
    public uint sampleLocationSubPixelBits;

    [NativeTypeName("VkBool32")]
    public uint variableSampleLocations;

    [InlineArray(2)]
    public partial struct _sampleLocationCoordinateRange_e__FixedBuffer
    {
        public float e0;
    }
}
