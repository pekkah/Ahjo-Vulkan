namespace Ahjo.Vulkan.Slang.Native;

public unsafe partial struct CoverageEntryInfo
{
    [NativeTypeName("size_t")]
    public nuint structSize;

    [NativeTypeName("const char *")]
    public sbyte* file;

    [NativeTypeName("uint32_t")]
    public uint line;

    [NativeTypeName("uint32_t")]
    public uint counterIndex;

    [NativeTypeName("slang::CoverageEntryKind")]
    public CoverageEntryKind kind;

    [NativeTypeName("slang::CoverageCounterMode")]
    public CoverageCounterMode counterMode;

    [NativeTypeName("uint32_t")]
    public uint startColumn;

    [NativeTypeName("uint32_t")]
    public uint endLine;

    [NativeTypeName("uint32_t")]
    public uint endColumn;

    [NativeTypeName("const char *")]
    public sbyte* functionName;

    [NativeTypeName("const char *")]
    public sbyte* functionMangledName;

    [NativeTypeName("uint32_t")]
    public uint branchSiteID;

    [NativeTypeName("uint32_t")]
    public uint branchArmID;

    [NativeTypeName("slang::CoverageBranchArmKind")]
    public CoverageBranchArmKind branchArmKind;
}
