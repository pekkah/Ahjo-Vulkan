namespace Ahjo.Vulkan.Slang.Native;

public partial struct CooperativeMatrixType
{
    public SlangScalarType componentType;

    public SlangScope scope;

    [NativeTypeName("uint32_t")]
    public uint rowCount;

    [NativeTypeName("uint32_t")]
    public uint columnCount;

    public SlangCooperativeMatrixUse use;
}
