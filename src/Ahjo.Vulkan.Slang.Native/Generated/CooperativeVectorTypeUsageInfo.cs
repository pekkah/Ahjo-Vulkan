namespace Ahjo.Vulkan.Slang.Native;

public partial struct CooperativeVectorTypeUsageInfo
{
    public SlangScalarType componentType;

    [NativeTypeName("uint32_t")]
    public uint maxSize;

    [NativeTypeName("SlangBool")]
    public bool usedForTrainingOp;
}
