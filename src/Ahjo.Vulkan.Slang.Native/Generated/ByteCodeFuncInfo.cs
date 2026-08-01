namespace Ahjo.Vulkan.Slang.Native;

public partial struct ByteCodeFuncInfo
{
    [NativeTypeName("uint32_t")]
    public uint parameterCount;

    [NativeTypeName("uint32_t")]
    public uint returnValueSize;
}
