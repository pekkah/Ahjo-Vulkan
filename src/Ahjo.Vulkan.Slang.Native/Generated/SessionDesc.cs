namespace Ahjo.Vulkan.Slang.Native;

public unsafe partial struct SessionDesc
{
    [NativeTypeName("size_t")]
    public nuint structureSize;

    [NativeTypeName("const slang::TargetDesc *")]
    public TargetDesc* targets;

    [NativeTypeName("SlangInt")]
    public long targetCount;

    [NativeTypeName("slang::SessionFlags")]
    public uint flags;

    public SlangMatrixLayoutMode defaultMatrixLayoutMode;

    [NativeTypeName("const char *const *")]
    public sbyte** searchPaths;

    [NativeTypeName("SlangInt")]
    public long searchPathCount;

    [NativeTypeName("const slang::PreprocessorMacroDesc *")]
    public PreprocessorMacroDesc* preprocessorMacros;

    [NativeTypeName("SlangInt")]
    public long preprocessorMacroCount;

    public ISlangFileSystem* fileSystem;

    public bool enableEffectAnnotations;

    public bool allowGLSLSyntax;

    [NativeTypeName("const slang::CompilerOptionEntry *")]
    public CompilerOptionEntry* compilerOptionEntries;

    [NativeTypeName("uint32_t")]
    public uint compilerOptionEntryCount;

    public bool skipSPIRVValidation;
}
