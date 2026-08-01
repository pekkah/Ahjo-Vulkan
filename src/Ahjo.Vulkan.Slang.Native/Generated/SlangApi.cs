using System.Runtime.InteropServices;

namespace Ahjo.Vulkan.Slang.Native;

public static unsafe partial class SlangApi
{
    public const uint SLANG_DIAGNOSTIC_FLAG_VERBOSE_PATHS = 0x01;
    public const uint SLANG_DIAGNOSTIC_FLAG_TREAT_WARNINGS_AS_ERRORS = 0x02;

    public const uint SLANG_COMPILE_FLAG_NO_MANGLING = 1 << 3;
    public const uint SLANG_COMPILE_FLAG_NO_CODEGEN = 1 << 4;
    public const uint SLANG_COMPILE_FLAG_OBFUSCATE = 1 << 5;
    public const uint SLANG_COMPILE_FLAG_NO_CHECKING = 0;
    public const uint SLANG_COMPILE_FLAG_SPLIT_MIXED_TYPES = 0;

    public const uint SLANG_TARGET_FLAG_PARAMETER_BLOCKS_USE_REGISTER_SPACES = 1 << 4;
    public const uint SLANG_TARGET_FLAG_GENERATE_WHOLE_PROGRAM = 1 << 8;
    public const uint SLANG_TARGET_FLAG_DUMP_IR = 1 << 9;
    public const uint SLANG_TARGET_FLAG_GENERATE_SPIRV_DIRECTLY = 1 << 10;

    [NativeTypeName("const SlangTargetFlags")]
    public const uint kDefaultTargetFlags = (uint)(SLANG_TARGET_FLAG_GENERATE_SPIRV_DIRECTLY);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* spGetBuildTagString();

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangSession *")]
    public static extern IGlobalSession* spCreateSession([NativeTypeName("const char *")] sbyte* deprecated = null);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spDestroySession([NativeTypeName("SlangSession *")] IGlobalSession* session);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spSessionSetSharedLibraryLoader([NativeTypeName("SlangSession *")] IGlobalSession* session, ISlangSharedLibraryLoader* loader);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ISlangSharedLibraryLoader* spSessionGetSharedLibraryLoader([NativeTypeName("SlangSession *")] IGlobalSession* session);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spSessionCheckCompileTargetSupport([NativeTypeName("SlangSession *")] IGlobalSession* session, SlangCompileTarget target);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spSessionCheckPassThroughSupport([NativeTypeName("SlangSession *")] IGlobalSession* session, SlangPassThrough passThrough);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spAddBuiltins([NativeTypeName("SlangSession *")] IGlobalSession* session, [NativeTypeName("const char *")] sbyte* sourcePath, [NativeTypeName("const char *")] sbyte* sourceString);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangCompileRequest *")]
    public static extern ICompileRequest* spCreateCompileRequest([NativeTypeName("SlangSession *")] IGlobalSession* session);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spDestroyCompileRequest([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spSetFileSystem([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, ISlangFileSystem* fileSystem);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spSetCompileFlags([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, [NativeTypeName("SlangCompileFlags")] uint flags);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangCompileFlags")]
    public static extern uint spGetCompileFlags([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spSetDumpIntermediates([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, int enable);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spSetDumpIntermediatePrefix([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, [NativeTypeName("const char *")] sbyte* prefix);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spSetLineDirectiveMode([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, SlangLineDirectiveMode mode);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spSetTargetLineDirectiveMode([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, int targetIndex, SlangLineDirectiveMode mode);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spSetTargetForceGLSLScalarBufferLayout([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, int targetIndex, bool forceScalarLayout);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spSetTargetUseMinimumSlangOptimization([NativeTypeName("slang::ICompileRequest *")] ICompileRequest* request, int targetIndex, bool val);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spSetIgnoreCapabilityCheck([NativeTypeName("slang::ICompileRequest *")] ICompileRequest* request, bool val);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spSetCodeGenTarget([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, SlangCompileTarget target);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int spAddCodeGenTarget([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, SlangCompileTarget target);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spSetTargetProfile([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, int targetIndex, SlangProfileID profile);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spSetTargetFlags([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, int targetIndex, [NativeTypeName("SlangTargetFlags")] uint flags);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spSetTargetFloatingPointMode([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, int targetIndex, SlangFloatingPointMode mode);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spAddTargetCapability([NativeTypeName("slang::ICompileRequest *")] ICompileRequest* request, int targetIndex, SlangCapabilityID capability);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spSetTargetMatrixLayoutMode([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, int targetIndex, SlangMatrixLayoutMode mode);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spSetMatrixLayoutMode([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, SlangMatrixLayoutMode mode);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spSetDebugInfoLevel([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, SlangDebugInfoLevel level);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spSetDebugInfoFormat([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, SlangDebugInfoFormat format);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spSetOptimizationLevel([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, SlangOptimizationLevel level);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spSetOutputContainerFormat([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, SlangContainerFormat format);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spSetPassThrough([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, SlangPassThrough passThrough);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spSetDiagnosticCallback([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, [NativeTypeName("SlangDiagnosticCallback")] nint callback, [NativeTypeName("const void *")] void* userData);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spSetWriter([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, SlangWriterChannel channel, ISlangWriter* writer);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ISlangWriter* spGetWriter([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, SlangWriterChannel channel);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spAddSearchPath([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, [NativeTypeName("const char *")] sbyte* searchDir);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spAddPreprocessorDefine([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, [NativeTypeName("const char *")] sbyte* key, [NativeTypeName("const char *")] sbyte* value);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spProcessCommandLineArguments([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, [NativeTypeName("const char *const *")] sbyte** args, int argCount);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int spAddTranslationUnit([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, SlangSourceLanguage language, [NativeTypeName("const char *")] sbyte* name);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spSetDefaultModuleName([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, [NativeTypeName("const char *")] sbyte* defaultModuleName);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spTranslationUnit_addPreprocessorDefine([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, int translationUnitIndex, [NativeTypeName("const char *")] sbyte* key, [NativeTypeName("const char *")] sbyte* value);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spAddTranslationUnitSourceFile([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, int translationUnitIndex, [NativeTypeName("const char *")] sbyte* path);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spAddTranslationUnitSourceString([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, int translationUnitIndex, [NativeTypeName("const char *")] sbyte* path, [NativeTypeName("const char *")] sbyte* source);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spAddLibraryReference([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, [NativeTypeName("const char *")] sbyte* basePath, [NativeTypeName("const void *")] void* libData, [NativeTypeName("size_t")] nuint libDataSize);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spAddTranslationUnitSourceStringSpan([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, int translationUnitIndex, [NativeTypeName("const char *")] sbyte* path, [NativeTypeName("const char *")] sbyte* sourceBegin, [NativeTypeName("const char *")] sbyte* sourceEnd);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spAddTranslationUnitSourceBlob([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, int translationUnitIndex, [NativeTypeName("const char *")] sbyte* path, ISlangBlob* sourceBlob);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangProfileID spFindProfile([NativeTypeName("SlangSession *")] IGlobalSession* session, [NativeTypeName("const char *")] sbyte* name);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangCapabilityID spFindCapability([NativeTypeName("SlangSession *")] IGlobalSession* session, [NativeTypeName("const char *")] sbyte* name);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int spAddEntryPoint([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, int translationUnitIndex, [NativeTypeName("const char *")] sbyte* name, SlangStage stage);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int spAddEntryPointEx([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, int translationUnitIndex, [NativeTypeName("const char *")] sbyte* name, SlangStage stage, int genericArgCount, [NativeTypeName("const char **")] sbyte** genericArgs);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spSetGlobalGenericArgs([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, int genericArgCount, [NativeTypeName("const char **")] sbyte** genericArgs);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spSetTypeNameForGlobalExistentialTypeParam([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, int slotIndex, [NativeTypeName("const char *")] sbyte* typeName);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spSetTypeNameForEntryPointExistentialTypeParam([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, int entryPointIndex, int slotIndex, [NativeTypeName("const char *")] sbyte* typeName);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spCompile([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* spGetDiagnosticOutput([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spGetDiagnosticOutputBlob([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, ISlangBlob** outBlob);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int spGetDependencyFileCount([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* spGetDependencyFilePath([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, int index);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int spGetTranslationUnitCount([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* spGetEntryPointSource([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, int entryPointIndex);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const void *")]
    public static extern void* spGetEntryPointCode([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, int entryPointIndex, [NativeTypeName("size_t *")] nuint* outSize);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spGetEntryPointCodeBlob([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, int entryPointIndex, int targetIndex, ISlangBlob** outBlob);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spGetEntryPointHostCallable([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, int entryPointIndex, int targetIndex, ISlangSharedLibrary** outSharedLibrary);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spGetTargetCodeBlob([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, int targetIndex, ISlangBlob** outBlob);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spGetTargetHostCallable([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, int targetIndex, ISlangSharedLibrary** outSharedLibrary);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const void *")]
    public static extern void* spGetCompileRequestCode([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, [NativeTypeName("size_t *")] nuint* outSize);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spGetContainerCode([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, ISlangBlob** outBlob);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spLoadRepro([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, ISlangFileSystem* fileSystem, [NativeTypeName("const void *")] void* data, [NativeTypeName("size_t")] nuint size);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spSaveRepro([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, ISlangBlob** outBlob);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spEnableReproCapture([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spGetCompileTimeProfile([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, ISlangProfiler** compileTimeProfile, bool shouldClear);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spExtractRepro([NativeTypeName("SlangSession *")] IGlobalSession* session, [NativeTypeName("const void *")] void* reproData, [NativeTypeName("size_t")] nuint reproDataSize, ISlangMutableFileSystem* fileSystem);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spLoadReproAsFileSystem([NativeTypeName("SlangSession *")] IGlobalSession* session, [NativeTypeName("const void *")] void* reproData, [NativeTypeName("size_t")] nuint reproDataSize, ISlangFileSystem* replaceFileSystem, ISlangFileSystemExt** outFileSystem);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spOverrideDiagnosticSeverity([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, [NativeTypeName("SlangInt")] long messageID, SlangSeverity overrideSeverity);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangDiagnosticFlags")]
    public static extern int spGetDiagnosticFlags([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spSetDiagnosticFlags([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, [NativeTypeName("SlangDiagnosticFlags")] int flags);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangReflection *")]
    public static extern SlangProgramLayout* spGetReflection([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* spReflectionUserAttribute_GetName(SlangReflectionUserAttribute* attrib);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("unsigned int")]
    public static extern uint spReflectionUserAttribute_GetArgumentCount(SlangReflectionUserAttribute* attrib);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionType* spReflectionUserAttribute_GetArgumentType(SlangReflectionUserAttribute* attrib, [NativeTypeName("unsigned int")] uint index);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spReflectionUserAttribute_GetArgumentValueInt(SlangReflectionUserAttribute* attrib, [NativeTypeName("unsigned int")] uint index, int* rs);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spReflectionUserAttribute_GetArgumentValueFloat(SlangReflectionUserAttribute* attrib, [NativeTypeName("unsigned int")] uint index, float* rs);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* spReflectionUserAttribute_GetArgumentValueString(SlangReflectionUserAttribute* attrib, [NativeTypeName("unsigned int")] uint index, [NativeTypeName("size_t *")] nuint* outSize);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangTypeKind spReflectionType_GetKind(SlangReflectionType* type);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("unsigned int")]
    public static extern uint spReflectionType_GetUserAttributeCount(SlangReflectionType* type);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionUserAttribute* spReflectionType_GetUserAttribute(SlangReflectionType* type, [NativeTypeName("unsigned int")] uint index);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionUserAttribute* spReflectionType_FindUserAttributeByName(SlangReflectionType* type, [NativeTypeName("const char *")] sbyte* name);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionType* spReflectionType_applySpecializations(SlangReflectionType* type, SlangReflectionGeneric* generic);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("unsigned int")]
    public static extern uint spReflectionType_GetFieldCount(SlangReflectionType* type);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionVariable* spReflectionType_GetFieldByIndex(SlangReflectionType* type, [NativeTypeName("unsigned int")] uint index);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint spReflectionType_GetElementCount(SlangReflectionType* type);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint spReflectionType_GetSpecializedElementCount(SlangReflectionType* type, [NativeTypeName("SlangReflection *")] SlangProgramLayout* reflection);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionType* spReflectionType_GetElementType(SlangReflectionType* type);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("unsigned int")]
    public static extern uint spReflectionType_GetRowCount(SlangReflectionType* type);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("unsigned int")]
    public static extern uint spReflectionType_GetColumnCount(SlangReflectionType* type);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangScalarType spReflectionType_GetScalarType(SlangReflectionType* type);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangResourceShape spReflectionType_GetResourceShape(SlangReflectionType* type);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangResourceAccess spReflectionType_GetResourceAccess(SlangReflectionType* type);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionType* spReflectionType_GetResourceResultType(SlangReflectionType* type);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* spReflectionType_GetName(SlangReflectionType* type);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spReflectionType_GetFullName(SlangReflectionType* type, ISlangBlob** outNameBlob);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionGeneric* spReflectionType_GetGenericContainer(SlangReflectionType* type);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionType* spReflectionTypeLayout_GetType(SlangReflectionTypeLayout* type);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangTypeKind spReflectionTypeLayout_getKind(SlangReflectionTypeLayout* type);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint spReflectionTypeLayout_GetSize(SlangReflectionTypeLayout* type, SlangParameterCategory category);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint spReflectionTypeLayout_GetStride(SlangReflectionTypeLayout* type, SlangParameterCategory category);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("int32_t")]
    public static extern int spReflectionTypeLayout_getAlignment(SlangReflectionTypeLayout* type, SlangParameterCategory category);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint32_t")]
    public static extern uint spReflectionTypeLayout_GetFieldCount(SlangReflectionTypeLayout* type);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionVariableLayout* spReflectionTypeLayout_GetFieldByIndex(SlangReflectionTypeLayout* type, [NativeTypeName("unsigned int")] uint index);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangInt")]
    public static extern long spReflectionTypeLayout_findFieldIndexByName(SlangReflectionTypeLayout* typeLayout, [NativeTypeName("const char *")] sbyte* nameBegin, [NativeTypeName("const char *")] sbyte* nameEnd);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionVariableLayout* spReflectionTypeLayout_GetExplicitCounter(SlangReflectionTypeLayout* typeLayout);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint spReflectionTypeLayout_GetElementStride(SlangReflectionTypeLayout* type, SlangParameterCategory category);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionTypeLayout* spReflectionTypeLayout_GetElementTypeLayout(SlangReflectionTypeLayout* type);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionVariableLayout* spReflectionTypeLayout_GetElementVarLayout(SlangReflectionTypeLayout* type);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionVariableLayout* spReflectionTypeLayout_getContainerVarLayout(SlangReflectionTypeLayout* type);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangParameterCategory spReflectionTypeLayout_GetParameterCategory(SlangReflectionTypeLayout* type);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("unsigned int")]
    public static extern uint spReflectionTypeLayout_GetCategoryCount(SlangReflectionTypeLayout* type);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangParameterCategory spReflectionTypeLayout_GetCategoryByIndex(SlangReflectionTypeLayout* type, [NativeTypeName("unsigned int")] uint index);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangMatrixLayoutMode spReflectionTypeLayout_GetMatrixLayoutMode(SlangReflectionTypeLayout* type);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int spReflectionTypeLayout_getGenericParamIndex(SlangReflectionTypeLayout* type);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionTypeLayout* spReflectionTypeLayout_getPendingDataTypeLayout(SlangReflectionTypeLayout* type);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionVariableLayout* spReflectionTypeLayout_getSpecializedTypePendingDataVarLayout(SlangReflectionTypeLayout* type);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangInt")]
    public static extern long spReflectionType_getSpecializedTypeArgCount(SlangReflectionType* type);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionType* spReflectionType_getSpecializedTypeArgType(SlangReflectionType* type, [NativeTypeName("SlangInt")] long index);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangInt")]
    public static extern long spReflectionTypeLayout_getBindingRangeCount(SlangReflectionTypeLayout* typeLayout);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangBindingType spReflectionTypeLayout_getBindingRangeType(SlangReflectionTypeLayout* typeLayout, [NativeTypeName("SlangInt")] long index);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangInt")]
    public static extern long spReflectionTypeLayout_isBindingRangeSpecializable(SlangReflectionTypeLayout* typeLayout, [NativeTypeName("SlangInt")] long index);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangInt")]
    public static extern long spReflectionTypeLayout_getBindingRangeBindingCount(SlangReflectionTypeLayout* typeLayout, [NativeTypeName("SlangInt")] long index);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionTypeLayout* spReflectionTypeLayout_getBindingRangeLeafTypeLayout(SlangReflectionTypeLayout* typeLayout, [NativeTypeName("SlangInt")] long index);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionVariable* spReflectionTypeLayout_getBindingRangeLeafVariable(SlangReflectionTypeLayout* typeLayout, [NativeTypeName("SlangInt")] long index);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangImageFormat spReflectionTypeLayout_getBindingRangeImageFormat(SlangReflectionTypeLayout* typeLayout, [NativeTypeName("SlangInt")] long index);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangInt")]
    public static extern long spReflectionTypeLayout_getFieldBindingRangeOffset(SlangReflectionTypeLayout* typeLayout, [NativeTypeName("SlangInt")] long fieldIndex);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangInt")]
    public static extern long spReflectionTypeLayout_getExplicitCounterBindingRangeOffset(SlangReflectionTypeLayout* inTypeLayout);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangInt")]
    public static extern long spReflectionTypeLayout_getBindingRangeDescriptorSetIndex(SlangReflectionTypeLayout* typeLayout, [NativeTypeName("SlangInt")] long index);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangInt")]
    public static extern long spReflectionTypeLayout_getBindingRangeFirstDescriptorRangeIndex(SlangReflectionTypeLayout* typeLayout, [NativeTypeName("SlangInt")] long index);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangInt")]
    public static extern long spReflectionTypeLayout_getBindingRangeDescriptorRangeCount(SlangReflectionTypeLayout* typeLayout, [NativeTypeName("SlangInt")] long index);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangInt")]
    public static extern long spReflectionTypeLayout_getDescriptorSetCount(SlangReflectionTypeLayout* typeLayout);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangInt")]
    public static extern long spReflectionTypeLayout_getDescriptorSetSpaceOffset(SlangReflectionTypeLayout* typeLayout, [NativeTypeName("SlangInt")] long setIndex);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangInt")]
    public static extern long spReflectionTypeLayout_getDescriptorSetDescriptorRangeCount(SlangReflectionTypeLayout* typeLayout, [NativeTypeName("SlangInt")] long setIndex);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangInt")]
    public static extern long spReflectionTypeLayout_getDescriptorSetDescriptorRangeIndexOffset(SlangReflectionTypeLayout* typeLayout, [NativeTypeName("SlangInt")] long setIndex, [NativeTypeName("SlangInt")] long rangeIndex);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangInt")]
    public static extern long spReflectionTypeLayout_getDescriptorSetDescriptorRangeDescriptorCount(SlangReflectionTypeLayout* typeLayout, [NativeTypeName("SlangInt")] long setIndex, [NativeTypeName("SlangInt")] long rangeIndex);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangBindingType spReflectionTypeLayout_getDescriptorSetDescriptorRangeType(SlangReflectionTypeLayout* typeLayout, [NativeTypeName("SlangInt")] long setIndex, [NativeTypeName("SlangInt")] long rangeIndex);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangParameterCategory spReflectionTypeLayout_getDescriptorSetDescriptorRangeCategory(SlangReflectionTypeLayout* typeLayout, [NativeTypeName("SlangInt")] long setIndex, [NativeTypeName("SlangInt")] long rangeIndex);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangInt")]
    public static extern long spReflectionTypeLayout_getSubObjectRangeCount(SlangReflectionTypeLayout* typeLayout);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangInt")]
    public static extern long spReflectionTypeLayout_getSubObjectRangeBindingRangeIndex(SlangReflectionTypeLayout* typeLayout, [NativeTypeName("SlangInt")] long subObjectRangeIndex);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangInt")]
    public static extern long spReflectionTypeLayout_getSubObjectRangeSpaceOffset(SlangReflectionTypeLayout* typeLayout, [NativeTypeName("SlangInt")] long subObjectRangeIndex);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionVariableLayout* spReflectionTypeLayout_getSubObjectRangeOffset(SlangReflectionTypeLayout* typeLayout, [NativeTypeName("SlangInt")] long subObjectRangeIndex);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* spReflectionVariable_GetName(SlangReflectionVariable* var);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionType* spReflectionVariable_GetType(SlangReflectionVariable* var);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionModifier* spReflectionVariable_FindModifier(SlangReflectionVariable* var, SlangModifierID modifierID);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("unsigned int")]
    public static extern uint spReflectionVariable_GetUserAttributeCount(SlangReflectionVariable* var);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionUserAttribute* spReflectionVariable_GetUserAttribute(SlangReflectionVariable* var, [NativeTypeName("unsigned int")] uint index);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionUserAttribute* spReflectionVariable_FindUserAttributeByName(SlangReflectionVariable* var, [NativeTypeName("SlangSession *")] IGlobalSession* globalSession, [NativeTypeName("const char *")] sbyte* name);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern bool spReflectionVariable_HasDefaultValue(SlangReflectionVariable* inVar);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spReflectionVariable_GetDefaultValueInt(SlangReflectionVariable* inVar, [NativeTypeName("int64_t *")] long* rs);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spReflectionVariable_GetDefaultValueFloat(SlangReflectionVariable* inVar, float* rs);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionGeneric* spReflectionVariable_GetGenericContainer(SlangReflectionVariable* var);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionVariable* spReflectionVariable_applySpecializations(SlangReflectionVariable* var, SlangReflectionGeneric* generic);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionVariable* spReflectionVariableLayout_GetVariable(SlangReflectionVariableLayout* var);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionTypeLayout* spReflectionVariableLayout_GetTypeLayout(SlangReflectionVariableLayout* var);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint spReflectionVariableLayout_GetOffset(SlangReflectionVariableLayout* var, SlangParameterCategory category);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint spReflectionVariableLayout_GetSpace(SlangReflectionVariableLayout* var, SlangParameterCategory category);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangImageFormat spReflectionVariableLayout_GetImageFormat(SlangReflectionVariableLayout* var);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* spReflectionVariableLayout_GetSemanticName(SlangReflectionVariableLayout* var);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint spReflectionVariableLayout_GetSemanticIndex(SlangReflectionVariableLayout* var);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionDecl* spReflectionFunction_asDecl(SlangReflectionFunction* func);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* spReflectionFunction_GetName(SlangReflectionFunction* func);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionModifier* spReflectionFunction_FindModifier(SlangReflectionFunction* var, SlangModifierID modifierID);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("unsigned int")]
    public static extern uint spReflectionFunction_GetUserAttributeCount(SlangReflectionFunction* func);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionUserAttribute* spReflectionFunction_GetUserAttribute(SlangReflectionFunction* func, [NativeTypeName("unsigned int")] uint index);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionUserAttribute* spReflectionFunction_FindUserAttributeByName(SlangReflectionFunction* func, [NativeTypeName("SlangSession *")] IGlobalSession* globalSession, [NativeTypeName("const char *")] sbyte* name);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("unsigned int")]
    public static extern uint spReflectionFunction_GetParameterCount(SlangReflectionFunction* func);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionVariable* spReflectionFunction_GetParameter(SlangReflectionFunction* func, [NativeTypeName("unsigned int")] uint index);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionType* spReflectionFunction_GetResultType(SlangReflectionFunction* func);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionGeneric* spReflectionFunction_GetGenericContainer(SlangReflectionFunction* func);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionFunction* spReflectionFunction_applySpecializations(SlangReflectionFunction* func, SlangReflectionGeneric* generic);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionFunction* spReflectionFunction_specializeWithArgTypes(SlangReflectionFunction* func, [NativeTypeName("SlangInt")] long argTypeCount, [NativeTypeName("SlangReflectionType *const *")] SlangReflectionType** argTypes);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern bool spReflectionFunction_isOverloaded(SlangReflectionFunction* func);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("unsigned int")]
    public static extern uint spReflectionFunction_getOverloadCount(SlangReflectionFunction* func);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionFunction* spReflectionFunction_getOverload(SlangReflectionFunction* func, [NativeTypeName("unsigned int")] uint index);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("unsigned int")]
    public static extern uint spReflectionDecl_getChildrenCount(SlangReflectionDecl* parentDecl);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionDecl* spReflectionDecl_getChild(SlangReflectionDecl* parentDecl, [NativeTypeName("unsigned int")] uint index);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* spReflectionDecl_getName(SlangReflectionDecl* decl);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangDeclKind spReflectionDecl_getKind(SlangReflectionDecl* decl);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionFunction* spReflectionDecl_castToFunction(SlangReflectionDecl* decl);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionVariable* spReflectionDecl_castToVariable(SlangReflectionDecl* decl);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionGeneric* spReflectionDecl_castToGeneric(SlangReflectionDecl* decl);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionType* spReflection_getTypeFromDecl(SlangReflectionDecl* decl);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionDecl* spReflectionDecl_getParent(SlangReflectionDecl* decl);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionModifier* spReflectionDecl_findModifier(SlangReflectionDecl* decl, SlangModifierID modifierID);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionDecl* spReflectionGeneric_asDecl(SlangReflectionGeneric* generic);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* spReflectionGeneric_GetName(SlangReflectionGeneric* generic);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("unsigned int")]
    public static extern uint spReflectionGeneric_GetTypeParameterCount(SlangReflectionGeneric* generic);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionVariable* spReflectionGeneric_GetTypeParameter(SlangReflectionGeneric* generic, [NativeTypeName("unsigned int")] uint index);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("unsigned int")]
    public static extern uint spReflectionGeneric_GetValueParameterCount(SlangReflectionGeneric* generic);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionVariable* spReflectionGeneric_GetValueParameter(SlangReflectionGeneric* generic, [NativeTypeName("unsigned int")] uint index);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("unsigned int")]
    public static extern uint spReflectionGeneric_GetTypeParameterConstraintCount(SlangReflectionGeneric* generic, SlangReflectionVariable* typeParam);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionType* spReflectionGeneric_GetTypeParameterConstraintType(SlangReflectionGeneric* generic, SlangReflectionVariable* typeParam, [NativeTypeName("unsigned int")] uint index);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangDeclKind spReflectionGeneric_GetInnerKind(SlangReflectionGeneric* generic);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionDecl* spReflectionGeneric_GetInnerDecl(SlangReflectionGeneric* generic);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionGeneric* spReflectionGeneric_GetOuterGenericContainer(SlangReflectionGeneric* generic);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionType* spReflectionGeneric_GetConcreteType(SlangReflectionGeneric* generic, SlangReflectionVariable* typeParam);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("int64_t")]
    public static extern long spReflectionGeneric_GetConcreteIntVal(SlangReflectionGeneric* generic, SlangReflectionVariable* valueParam);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionGeneric* spReflectionGeneric_applySpecializations(SlangReflectionGeneric* currGeneric, SlangReflectionGeneric* generic);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangStage spReflectionVariableLayout_getStage(SlangReflectionVariableLayout* var);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionVariableLayout* spReflectionVariableLayout_getPendingDataLayout(SlangReflectionVariableLayout* var);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("unsigned int")]
    public static extern uint spReflectionParameter_GetBindingIndex([NativeTypeName("SlangReflectionParameter *")] SlangReflectionVariableLayout* parameter);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("unsigned int")]
    public static extern uint spReflectionParameter_GetBindingSpace([NativeTypeName("SlangReflectionParameter *")] SlangReflectionVariableLayout* parameter);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spIsParameterLocationUsed([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, [NativeTypeName("SlangInt")] long entryPointIndex, [NativeTypeName("SlangInt")] long targetIndex, SlangParameterCategory category, [NativeTypeName("SlangUInt")] ulong spaceIndex, [NativeTypeName("SlangUInt")] ulong registerIndex, [NativeTypeName("bool &")] bool* outUsed);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* spReflectionEntryPoint_getName([NativeTypeName("SlangReflectionEntryPoint *")] SlangEntryPointLayout* entryPoint);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* spReflectionEntryPoint_getNameOverride([NativeTypeName("SlangReflectionEntryPoint *")] SlangEntryPointLayout* entryPoint);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionFunction* spReflectionEntryPoint_getFunction([NativeTypeName("SlangReflectionEntryPoint *")] SlangEntryPointLayout* entryPoint);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("unsigned int")]
    public static extern uint spReflectionEntryPoint_getParameterCount([NativeTypeName("SlangReflectionEntryPoint *")] SlangEntryPointLayout* entryPoint);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionVariableLayout* spReflectionEntryPoint_getParameterByIndex([NativeTypeName("SlangReflectionEntryPoint *")] SlangEntryPointLayout* entryPoint, [NativeTypeName("unsigned int")] uint index);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangStage spReflectionEntryPoint_getStage([NativeTypeName("SlangReflectionEntryPoint *")] SlangEntryPointLayout* entryPoint);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spReflectionEntryPoint_getComputeThreadGroupSize([NativeTypeName("SlangReflectionEntryPoint *")] SlangEntryPointLayout* entryPoint, [NativeTypeName("SlangUInt")] ulong axisCount, [NativeTypeName("SlangUInt *")] ulong* outSizeAlongAxis);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void spReflectionEntryPoint_getComputeWaveSize([NativeTypeName("SlangReflectionEntryPoint *")] SlangEntryPointLayout* entryPoint, [NativeTypeName("SlangUInt *")] ulong* outWaveSize);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int spReflectionEntryPoint_usesAnySampleRateInput([NativeTypeName("SlangReflectionEntryPoint *")] SlangEntryPointLayout* entryPoint);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionVariableLayout* spReflectionEntryPoint_getVarLayout([NativeTypeName("SlangReflectionEntryPoint *")] SlangEntryPointLayout* entryPoint);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionVariableLayout* spReflectionEntryPoint_getResultVarLayout([NativeTypeName("SlangReflectionEntryPoint *")] SlangEntryPointLayout* entryPoint);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int spReflectionEntryPoint_hasDefaultConstantBuffer([NativeTypeName("SlangReflectionEntryPoint *")] SlangEntryPointLayout* entryPoint);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* spReflectionTypeParameter_GetName(SlangReflectionTypeParameter* typeParam);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("unsigned int")]
    public static extern uint spReflectionTypeParameter_GetIndex(SlangReflectionTypeParameter* typeParam);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("unsigned int")]
    public static extern uint spReflectionTypeParameter_GetConstraintCount(SlangReflectionTypeParameter* typeParam);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionType* spReflectionTypeParameter_GetConstraintByIndex(SlangReflectionTypeParameter* typeParam, [NativeTypeName("unsigned int")] uint index);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spReflection_ToJson([NativeTypeName("SlangReflection *")] SlangProgramLayout* reflection, [NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, ISlangBlob** outBlob);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("unsigned int")]
    public static extern uint spReflection_GetParameterCount([NativeTypeName("SlangReflection *")] SlangProgramLayout* reflection);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangReflectionParameter *")]
    public static extern SlangReflectionVariableLayout* spReflection_GetParameterByIndex([NativeTypeName("SlangReflection *")] SlangProgramLayout* reflection, [NativeTypeName("unsigned int")] uint index);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("unsigned int")]
    public static extern uint spReflection_GetTypeParameterCount([NativeTypeName("SlangReflection *")] SlangProgramLayout* reflection);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionTypeParameter* spReflection_GetTypeParameterByIndex([NativeTypeName("SlangReflection *")] SlangProgramLayout* reflection, [NativeTypeName("unsigned int")] uint index);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionTypeParameter* spReflection_FindTypeParameter([NativeTypeName("SlangReflection *")] SlangProgramLayout* reflection, [NativeTypeName("const char *")] sbyte* name);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionType* spReflection_FindTypeByName([NativeTypeName("SlangReflection *")] SlangProgramLayout* reflection, [NativeTypeName("const char *")] sbyte* name);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionTypeLayout* spReflection_GetTypeLayout([NativeTypeName("SlangReflection *")] SlangProgramLayout* reflection, SlangReflectionType* reflectionType, SlangLayoutRules rules);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionFunction* spReflection_FindFunctionByName([NativeTypeName("SlangReflection *")] SlangProgramLayout* reflection, [NativeTypeName("const char *")] sbyte* name);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionFunction* spReflection_FindFunctionByNameInType([NativeTypeName("SlangReflection *")] SlangProgramLayout* reflection, SlangReflectionType* reflType, [NativeTypeName("const char *")] sbyte* name);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionVariable* spReflection_FindVarByNameInType([NativeTypeName("SlangReflection *")] SlangProgramLayout* reflection, SlangReflectionType* reflType, [NativeTypeName("const char *")] sbyte* name);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionFunction* spReflection_TryResolveOverloadedFunction([NativeTypeName("SlangReflection *")] SlangProgramLayout* reflection, [NativeTypeName("uint32_t")] uint candidateCount, SlangReflectionFunction** candidates);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangUInt")]
    public static extern ulong spReflection_getEntryPointCount([NativeTypeName("SlangReflection *")] SlangProgramLayout* reflection);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangReflectionEntryPoint *")]
    public static extern SlangEntryPointLayout* spReflection_getEntryPointByIndex([NativeTypeName("SlangReflection *")] SlangProgramLayout* reflection, [NativeTypeName("SlangUInt")] ulong index);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangReflectionEntryPoint *")]
    public static extern SlangEntryPointLayout* spReflection_findEntryPointByName([NativeTypeName("SlangReflection *")] SlangProgramLayout* reflection, [NativeTypeName("const char *")] sbyte* name);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangUInt")]
    public static extern ulong spReflection_getGlobalConstantBufferBinding([NativeTypeName("SlangReflection *")] SlangProgramLayout* reflection);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint spReflection_getGlobalConstantBufferSize([NativeTypeName("SlangReflection *")] SlangProgramLayout* reflection);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionType* spReflection_specializeType([NativeTypeName("SlangReflection *")] SlangProgramLayout* reflection, SlangReflectionType* type, [NativeTypeName("SlangInt")] long specializationArgCount, [NativeTypeName("SlangReflectionType *const *")] SlangReflectionType** specializationArgs, ISlangBlob** outDiagnostics);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionGeneric* spReflection_specializeGeneric([NativeTypeName("SlangReflection *")] SlangProgramLayout* inProgramLayout, SlangReflectionGeneric* generic, [NativeTypeName("SlangInt")] long argCount, [NativeTypeName("const SlangReflectionGenericArgType *")] SlangReflectionGenericArgType* argTypes, [NativeTypeName("const SlangReflectionGenericArg *")] SlangReflectionGenericArg* args, ISlangBlob** outDiagnostics);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern bool spReflection_isSubType([NativeTypeName("SlangReflection *")] SlangProgramLayout* reflection, SlangReflectionType* subType, SlangReflectionType* superType);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangUInt")]
    public static extern ulong spReflection_getHashedStringCount([NativeTypeName("SlangReflection *")] SlangProgramLayout* reflection);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* spReflection_getHashedString([NativeTypeName("SlangReflection *")] SlangProgramLayout* reflection, [NativeTypeName("SlangUInt")] ulong index, [NativeTypeName("size_t *")] nuint* outCount);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangUInt32")]
    public static extern uint spComputeStringHash([NativeTypeName("const char *")] sbyte* chars, [NativeTypeName("size_t")] nuint count);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionTypeLayout* spReflection_getGlobalParamsTypeLayout([NativeTypeName("SlangReflection *")] SlangProgramLayout* reflection);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SlangReflectionVariableLayout* spReflection_getGlobalParamsVarLayout([NativeTypeName("SlangReflection *")] SlangProgramLayout* reflection);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* spGetTranslationUnitSource([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, int translationUnitIndex);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangInt")]
    public static extern long spReflection_getBindlessSpaceIndex([NativeTypeName("SlangReflection *")] SlangProgramLayout* reflection);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spCompileRequest_getProgram([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, [NativeTypeName("slang::IComponentType **")] IComponentType** outProgram);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spCompileRequest_getProgramWithEntryPoints([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, [NativeTypeName("slang::IComponentType **")] IComponentType** outProgram);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spCompileRequest_getEntryPoint([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, [NativeTypeName("SlangInt")] long entryPointIndex, [NativeTypeName("slang::IComponentType **")] IComponentType** outEntryPoint);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spCompileRequest_getModule([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, [NativeTypeName("SlangInt")] long translationUnitIndex, [NativeTypeName("slang::IModule **")] IModule** outModule);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int spCompileRequest_getSession([NativeTypeName("SlangCompileRequest *")] ICompileRequest* request, [NativeTypeName("slang::ISession **")] ISession** outSession);

    public const uint kSessionFlags_None = 0;

    [NativeTypeName("const uint32_t")]
    public const uint kInvalidCoverageCounterIndex = 0xffffffffU;

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ISlangBlob* slang_createBlob([NativeTypeName("const void *")] void* data, [NativeTypeName("size_t")] nuint size);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int slang_writeCoverageManifestJson([NativeTypeName("slang::ICoverageTracingMetadata *")] ICoverageTracingMetadata* metadata, ISlangBlob** outBlob);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("slang::IModule *")]
    public static extern IModule* slang_loadModuleFromSource([NativeTypeName("slang::ISession *")] ISession* session, [NativeTypeName("const char *")] sbyte* moduleName, [NativeTypeName("const char *")] sbyte* path, [NativeTypeName("const char *")] sbyte* source, [NativeTypeName("size_t")] nuint sourceSize, ISlangBlob** outDiagnostics = null);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("slang::IModule *")]
    public static extern IModule* slang_loadModuleFromIRBlob([NativeTypeName("slang::ISession *")] ISession* session, [NativeTypeName("const char *")] sbyte* moduleName, [NativeTypeName("const char *")] sbyte* path, [NativeTypeName("const void *")] void* source, [NativeTypeName("size_t")] nuint sourceSize, ISlangBlob** outDiagnostics = null);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int slang_loadModuleInfoFromIRBlob([NativeTypeName("slang::ISession *")] ISession* session, [NativeTypeName("const void *")] void* source, [NativeTypeName("size_t")] nuint sourceSize, [NativeTypeName("SlangInt &")] long* outModuleVersion, [NativeTypeName("const char *&")] sbyte** outModuleCompilerVersion, [NativeTypeName("const char *&")] sbyte** outModuleName);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int slang_createGlobalSession([NativeTypeName("SlangInt")] long apiVersion, [NativeTypeName("slang::IGlobalSession **")] IGlobalSession** outGlobalSession);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int slang_createGlobalSession2([NativeTypeName("const SlangGlobalSessionDesc *")] SlangGlobalSessionDesc* desc, [NativeTypeName("slang::IGlobalSession **")] IGlobalSession** outGlobalSession);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int slang_createGlobalSessionWithoutCoreModule([NativeTypeName("SlangInt")] long apiVersion, [NativeTypeName("slang::IGlobalSession **")] IGlobalSession** outGlobalSession);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void slang_shutdown();

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void slang_enableRecordLayer(bool enable);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern bool slang_isRecordLayerEnabled();

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void slang_setReplayDirectory([NativeTypeName("const char *")] sbyte* path);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* slang_getReplayDirectory();

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* slang_getCurrentReplayPath();

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int slang_loadReplay([NativeTypeName("const char *")] sbyte* folderPath);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int slang_loadLatestReplay();

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void slang_replayMarker([NativeTypeName("const char *")] sbyte* label);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* slang_getLastInternalErrorMessage();

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int slang_createByteCodeRunner([NativeTypeName("const slang::ByteCodeRunnerDesc *")] ByteCodeRunnerDesc* desc, [NativeTypeName("slang::IByteCodeRunner **")] IByteCodeRunner** outByteCodeRunner);

    [DllImport("slang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SlangResult")]
    public static extern int slang_disassembleByteCode([NativeTypeName("slang::IBlob *")] ISlangBlob* moduleBlob, [NativeTypeName("slang::IBlob **")] ISlangBlob** outDisassemblyBlob);
}
