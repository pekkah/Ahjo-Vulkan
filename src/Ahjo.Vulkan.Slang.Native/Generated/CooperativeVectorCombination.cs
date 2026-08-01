namespace Ahjo.Vulkan.Slang.Native;

public partial struct CooperativeVectorCombination
{
    public SlangScalarType inputType;

    public SlangScalarType inputInterpretation;

    [NativeTypeName("uint32_t")]
    public uint inputPackingFactor;

    public SlangScalarType matrixInterpretation;

    public SlangScalarType biasInterpretation;

    public SlangScalarType resultType;

    [NativeTypeName("SlangBool")]
    public bool transpose;
}
