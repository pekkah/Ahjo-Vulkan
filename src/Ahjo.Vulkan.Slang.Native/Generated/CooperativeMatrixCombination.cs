namespace Ahjo.Vulkan.Slang.Native;

public partial struct CooperativeMatrixCombination
{
    [NativeTypeName("uint32_t")]
    public uint m;

    [NativeTypeName("uint32_t")]
    public uint n;

    [NativeTypeName("uint32_t")]
    public uint k;

    public SlangScalarType componentTypeA;

    public SlangScalarType componentTypeB;

    public SlangScalarType componentTypeC;

    public SlangScalarType componentTypeResult;

    [NativeTypeName("SlangBool")]
    public bool saturate;

    public SlangScope scope;
}
