namespace Ahjo.Vulkan.Slang.Native;

public partial struct VMExecInstHeader
{
    [NativeTypeName("slang::VMExtFunction")]
    public nint functionPtr;

    [NativeTypeName("uint32_t")]
    public uint opcodeExtension;

    [NativeTypeName("uint32_t")]
    public uint operandCount;
}
