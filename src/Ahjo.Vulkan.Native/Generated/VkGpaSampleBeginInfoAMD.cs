namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkGpaSampleBeginInfoAMD
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkGpaSampleTypeAMD sampleType;

    [NativeTypeName("VkBool32")]
    public uint sampleInternalOperations;

    [NativeTypeName("VkBool32")]
    public uint cacheFlushOnCounterCollection;

    [NativeTypeName("VkBool32")]
    public uint sqShaderMaskEnable;

    [NativeTypeName("VkGpaSqShaderStageFlagsAMD")]
    public uint sqShaderMask;

    [NativeTypeName("uint32_t")]
    public uint perfCounterCount;

    [NativeTypeName("const VkGpaPerfCounterAMD *")]
    public VkGpaPerfCounterAMD* pPerfCounters;

    [NativeTypeName("uint32_t")]
    public uint streamingPerfTraceSampleInterval;

    [NativeTypeName("VkDeviceSize")]
    public ulong perfCounterDeviceMemoryLimit;

    [NativeTypeName("VkBool32")]
    public uint sqThreadTraceEnable;

    [NativeTypeName("VkBool32")]
    public uint sqThreadTraceSuppressInstructionTokens;

    [NativeTypeName("VkDeviceSize")]
    public ulong sqThreadTraceDeviceMemoryLimit;

    [NativeTypeName("VkPipelineStageFlags")]
    public uint timingPreSample;

    [NativeTypeName("VkPipelineStageFlags")]
    public uint timingPostSample;
}
