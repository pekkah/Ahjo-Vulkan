namespace Ahjo.Vulkan.Slang.Native;

public unsafe partial struct SyntheticResourceInfo
{
    [NativeTypeName("size_t")]
    public nuint structSize;

    [NativeTypeName("uint32_t")]
    public uint id;

    [NativeTypeName("slang::BindingType")]
    public BindingType bindingType;

    [NativeTypeName("uint32_t")]
    public uint arraySize;

    [NativeTypeName("slang::SyntheticResourceScope")]
    public SyntheticResourceScope scope;

    [NativeTypeName("slang::SyntheticResourceAccess")]
    public SyntheticResourceAccess access;

    [NativeTypeName("int32_t")]
    public int entryPointIndex;

    [NativeTypeName("int32_t")]
    public int space;

    [NativeTypeName("int32_t")]
    public int binding;

    [NativeTypeName("int32_t")]
    public int uniformOffset;

    [NativeTypeName("int32_t")]
    public int uniformStride;

    [NativeTypeName("const char *")]
    public sbyte* debugName;
}
