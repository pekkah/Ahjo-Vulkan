using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("struct ICompileRequest : ISlangUnknown")]
[NativeInheritance("ISlangUnknown")]
public unsafe partial struct ICompileRequest
{
    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(0)]
    [return: NativeTypeName("SlangResult")]
    public int queryInterface([NativeTypeName("const SlangUUID &")] SlangUUID* uuid, void** outObject)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, SlangUUID*, void**, int>)(lpVtbl[0]))((ICompileRequest*)Unsafe.AsPointer(ref this), uuid, outObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(1)]
    [return: NativeTypeName("uint32_t")]
    public uint addRef()
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, uint>)(lpVtbl[1]))((ICompileRequest*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(2)]
    [return: NativeTypeName("uint32_t")]
    public uint release()
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, uint>)(lpVtbl[2]))((ICompileRequest*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(3)]
    public void setFileSystem(ISlangFileSystem* fileSystem)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, ISlangFileSystem*, void>)(lpVtbl[3]))((ICompileRequest*)Unsafe.AsPointer(ref this), fileSystem);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(4)]
    public void setCompileFlags([NativeTypeName("SlangCompileFlags")] uint flags)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, uint, void>)(lpVtbl[4]))((ICompileRequest*)Unsafe.AsPointer(ref this), flags);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(5)]
    [return: NativeTypeName("SlangCompileFlags")]
    public uint getCompileFlags()
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, uint>)(lpVtbl[5]))((ICompileRequest*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(6)]
    public void setDumpIntermediates(int enable)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int, void>)(lpVtbl[6]))((ICompileRequest*)Unsafe.AsPointer(ref this), enable);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(7)]
    public void setDumpIntermediatePrefix([NativeTypeName("const char *")] sbyte* prefix)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, sbyte*, void>)(lpVtbl[7]))((ICompileRequest*)Unsafe.AsPointer(ref this), prefix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(8)]
    public void setLineDirectiveMode(SlangLineDirectiveMode mode)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, SlangLineDirectiveMode, void>)(lpVtbl[8]))((ICompileRequest*)Unsafe.AsPointer(ref this), mode);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(9)]
    public void setCodeGenTarget(SlangCompileTarget target)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, SlangCompileTarget, void>)(lpVtbl[9]))((ICompileRequest*)Unsafe.AsPointer(ref this), target);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(10)]
    public int addCodeGenTarget(SlangCompileTarget target)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, SlangCompileTarget, int>)(lpVtbl[10]))((ICompileRequest*)Unsafe.AsPointer(ref this), target);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(11)]
    public void setTargetProfile(int targetIndex, SlangProfileID profile)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int, SlangProfileID, void>)(lpVtbl[11]))((ICompileRequest*)Unsafe.AsPointer(ref this), targetIndex, profile);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(12)]
    public void setTargetFlags(int targetIndex, [NativeTypeName("SlangTargetFlags")] uint flags)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int, uint, void>)(lpVtbl[12]))((ICompileRequest*)Unsafe.AsPointer(ref this), targetIndex, flags);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(13)]
    public void setTargetFloatingPointMode(int targetIndex, SlangFloatingPointMode mode)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int, SlangFloatingPointMode, void>)(lpVtbl[13]))((ICompileRequest*)Unsafe.AsPointer(ref this), targetIndex, mode);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(14)]
    public void setTargetMatrixLayoutMode(int targetIndex, SlangMatrixLayoutMode mode)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int, SlangMatrixLayoutMode, void>)(lpVtbl[14]))((ICompileRequest*)Unsafe.AsPointer(ref this), targetIndex, mode);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(15)]
    public void setMatrixLayoutMode(SlangMatrixLayoutMode mode)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, SlangMatrixLayoutMode, void>)(lpVtbl[15]))((ICompileRequest*)Unsafe.AsPointer(ref this), mode);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(16)]
    public void setDebugInfoLevel(SlangDebugInfoLevel level)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, SlangDebugInfoLevel, void>)(lpVtbl[16]))((ICompileRequest*)Unsafe.AsPointer(ref this), level);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(17)]
    public void setOptimizationLevel(SlangOptimizationLevel level)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, SlangOptimizationLevel, void>)(lpVtbl[17]))((ICompileRequest*)Unsafe.AsPointer(ref this), level);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(18)]
    public void setOutputContainerFormat(SlangContainerFormat format)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, SlangContainerFormat, void>)(lpVtbl[18]))((ICompileRequest*)Unsafe.AsPointer(ref this), format);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(19)]
    public void setPassThrough(SlangPassThrough passThrough)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, SlangPassThrough, void>)(lpVtbl[19]))((ICompileRequest*)Unsafe.AsPointer(ref this), passThrough);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(20)]
    public void setDiagnosticCallback([NativeTypeName("SlangDiagnosticCallback")] nint callback, [NativeTypeName("const void *")] void* userData)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, nint, void*, void>)(lpVtbl[20]))((ICompileRequest*)Unsafe.AsPointer(ref this), callback, userData);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(21)]
    public void setWriter(SlangWriterChannel channel, ISlangWriter* writer)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, SlangWriterChannel, ISlangWriter*, void>)(lpVtbl[21]))((ICompileRequest*)Unsafe.AsPointer(ref this), channel, writer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(22)]
    public ISlangWriter* getWriter(SlangWriterChannel channel)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, SlangWriterChannel, ISlangWriter*>)(lpVtbl[22]))((ICompileRequest*)Unsafe.AsPointer(ref this), channel);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(23)]
    public void addSearchPath([NativeTypeName("const char *")] sbyte* searchDir)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, sbyte*, void>)(lpVtbl[23]))((ICompileRequest*)Unsafe.AsPointer(ref this), searchDir);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(24)]
    public void addPreprocessorDefine([NativeTypeName("const char *")] sbyte* key, [NativeTypeName("const char *")] sbyte* value)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, sbyte*, sbyte*, void>)(lpVtbl[24]))((ICompileRequest*)Unsafe.AsPointer(ref this), key, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(25)]
    [return: NativeTypeName("SlangResult")]
    public int processCommandLineArguments([NativeTypeName("const char *const *")] sbyte** args, int argCount)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, sbyte**, int, int>)(lpVtbl[25]))((ICompileRequest*)Unsafe.AsPointer(ref this), args, argCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(26)]
    public int addTranslationUnit(SlangSourceLanguage language, [NativeTypeName("const char *")] sbyte* name)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, SlangSourceLanguage, sbyte*, int>)(lpVtbl[26]))((ICompileRequest*)Unsafe.AsPointer(ref this), language, name);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(27)]
    public void setDefaultModuleName([NativeTypeName("const char *")] sbyte* defaultModuleName)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, sbyte*, void>)(lpVtbl[27]))((ICompileRequest*)Unsafe.AsPointer(ref this), defaultModuleName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(28)]
    public void addTranslationUnitPreprocessorDefine(int translationUnitIndex, [NativeTypeName("const char *")] sbyte* key, [NativeTypeName("const char *")] sbyte* value)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int, sbyte*, sbyte*, void>)(lpVtbl[28]))((ICompileRequest*)Unsafe.AsPointer(ref this), translationUnitIndex, key, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(29)]
    public void addTranslationUnitSourceFile(int translationUnitIndex, [NativeTypeName("const char *")] sbyte* path)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int, sbyte*, void>)(lpVtbl[29]))((ICompileRequest*)Unsafe.AsPointer(ref this), translationUnitIndex, path);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(30)]
    public void addTranslationUnitSourceString(int translationUnitIndex, [NativeTypeName("const char *")] sbyte* path, [NativeTypeName("const char *")] sbyte* source)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int, sbyte*, sbyte*, void>)(lpVtbl[30]))((ICompileRequest*)Unsafe.AsPointer(ref this), translationUnitIndex, path, source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(31)]
    [return: NativeTypeName("SlangResult")]
    public int addLibraryReference([NativeTypeName("const char *")] sbyte* basePath, [NativeTypeName("const void *")] void* libData, [NativeTypeName("size_t")] nuint libDataSize)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, sbyte*, void*, nuint, int>)(lpVtbl[31]))((ICompileRequest*)Unsafe.AsPointer(ref this), basePath, libData, libDataSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(32)]
    public void addTranslationUnitSourceStringSpan(int translationUnitIndex, [NativeTypeName("const char *")] sbyte* path, [NativeTypeName("const char *")] sbyte* sourceBegin, [NativeTypeName("const char *")] sbyte* sourceEnd)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int, sbyte*, sbyte*, sbyte*, void>)(lpVtbl[32]))((ICompileRequest*)Unsafe.AsPointer(ref this), translationUnitIndex, path, sourceBegin, sourceEnd);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(33)]
    public void addTranslationUnitSourceBlob(int translationUnitIndex, [NativeTypeName("const char *")] sbyte* path, ISlangBlob* sourceBlob)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int, sbyte*, ISlangBlob*, void>)(lpVtbl[33]))((ICompileRequest*)Unsafe.AsPointer(ref this), translationUnitIndex, path, sourceBlob);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(34)]
    public int addEntryPoint(int translationUnitIndex, [NativeTypeName("const char *")] sbyte* name, SlangStage stage)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int, sbyte*, SlangStage, int>)(lpVtbl[34]))((ICompileRequest*)Unsafe.AsPointer(ref this), translationUnitIndex, name, stage);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(35)]
    public int addEntryPointEx(int translationUnitIndex, [NativeTypeName("const char *")] sbyte* name, SlangStage stage, int genericArgCount, [NativeTypeName("const char **")] sbyte** genericArgs)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int, sbyte*, SlangStage, int, sbyte**, int>)(lpVtbl[35]))((ICompileRequest*)Unsafe.AsPointer(ref this), translationUnitIndex, name, stage, genericArgCount, genericArgs);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(36)]
    [return: NativeTypeName("SlangResult")]
    public int setGlobalGenericArgs(int genericArgCount, [NativeTypeName("const char **")] sbyte** genericArgs)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int, sbyte**, int>)(lpVtbl[36]))((ICompileRequest*)Unsafe.AsPointer(ref this), genericArgCount, genericArgs);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(37)]
    [return: NativeTypeName("SlangResult")]
    public int setTypeNameForGlobalExistentialTypeParam(int slotIndex, [NativeTypeName("const char *")] sbyte* typeName)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int, sbyte*, int>)(lpVtbl[37]))((ICompileRequest*)Unsafe.AsPointer(ref this), slotIndex, typeName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(38)]
    [return: NativeTypeName("SlangResult")]
    public int setTypeNameForEntryPointExistentialTypeParam(int entryPointIndex, int slotIndex, [NativeTypeName("const char *")] sbyte* typeName)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int, int, sbyte*, int>)(lpVtbl[38]))((ICompileRequest*)Unsafe.AsPointer(ref this), entryPointIndex, slotIndex, typeName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(39)]
    public void setAllowGLSLInput(bool value)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, bool, void>)(lpVtbl[39]))((ICompileRequest*)Unsafe.AsPointer(ref this), value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(40)]
    [return: NativeTypeName("SlangResult")]
    public int compile()
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int>)(lpVtbl[40]))((ICompileRequest*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(41)]
    [return: NativeTypeName("const char *")]
    public sbyte* getDiagnosticOutput()
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, sbyte*>)(lpVtbl[41]))((ICompileRequest*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(42)]
    [return: NativeTypeName("SlangResult")]
    public int getDiagnosticOutputBlob(ISlangBlob** outBlob)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, ISlangBlob**, int>)(lpVtbl[42]))((ICompileRequest*)Unsafe.AsPointer(ref this), outBlob);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(43)]
    public int getDependencyFileCount()
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int>)(lpVtbl[43]))((ICompileRequest*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(44)]
    [return: NativeTypeName("const char *")]
    public sbyte* getDependencyFilePath(int index)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int, sbyte*>)(lpVtbl[44]))((ICompileRequest*)Unsafe.AsPointer(ref this), index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(45)]
    public int getTranslationUnitCount()
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int>)(lpVtbl[45]))((ICompileRequest*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(46)]
    [return: NativeTypeName("const char *")]
    public sbyte* getEntryPointSource(int entryPointIndex)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int, sbyte*>)(lpVtbl[46]))((ICompileRequest*)Unsafe.AsPointer(ref this), entryPointIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(47)]
    [return: NativeTypeName("const void *")]
    public void* getEntryPointCode(int entryPointIndex, [NativeTypeName("size_t *")] nuint* outSize)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int, nuint*, void*>)(lpVtbl[47]))((ICompileRequest*)Unsafe.AsPointer(ref this), entryPointIndex, outSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(48)]
    [return: NativeTypeName("SlangResult")]
    public int getEntryPointCodeBlob(int entryPointIndex, int targetIndex, ISlangBlob** outBlob)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int, int, ISlangBlob**, int>)(lpVtbl[48]))((ICompileRequest*)Unsafe.AsPointer(ref this), entryPointIndex, targetIndex, outBlob);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(49)]
    [return: NativeTypeName("SlangResult")]
    public int getEntryPointHostCallable(int entryPointIndex, int targetIndex, ISlangSharedLibrary** outSharedLibrary)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int, int, ISlangSharedLibrary**, int>)(lpVtbl[49]))((ICompileRequest*)Unsafe.AsPointer(ref this), entryPointIndex, targetIndex, outSharedLibrary);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(50)]
    [return: NativeTypeName("SlangResult")]
    public int getTargetCodeBlob(int targetIndex, ISlangBlob** outBlob)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int, ISlangBlob**, int>)(lpVtbl[50]))((ICompileRequest*)Unsafe.AsPointer(ref this), targetIndex, outBlob);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(51)]
    [return: NativeTypeName("SlangResult")]
    public int getTargetHostCallable(int targetIndex, ISlangSharedLibrary** outSharedLibrary)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int, ISlangSharedLibrary**, int>)(lpVtbl[51]))((ICompileRequest*)Unsafe.AsPointer(ref this), targetIndex, outSharedLibrary);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(52)]
    [return: NativeTypeName("const void *")]
    public void* getCompileRequestCode([NativeTypeName("size_t *")] nuint* outSize)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, nuint*, void*>)(lpVtbl[52]))((ICompileRequest*)Unsafe.AsPointer(ref this), outSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(53)]
    public ISlangMutableFileSystem* getCompileRequestResultAsFileSystem()
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, ISlangMutableFileSystem*>)(lpVtbl[53]))((ICompileRequest*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(54)]
    [return: NativeTypeName("SlangResult")]
    public int getContainerCode(ISlangBlob** outBlob)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, ISlangBlob**, int>)(lpVtbl[54]))((ICompileRequest*)Unsafe.AsPointer(ref this), outBlob);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(55)]
    [return: NativeTypeName("SlangResult")]
    public int loadRepro(ISlangFileSystem* fileSystem, [NativeTypeName("const void *")] void* data, [NativeTypeName("size_t")] nuint size)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, ISlangFileSystem*, void*, nuint, int>)(lpVtbl[55]))((ICompileRequest*)Unsafe.AsPointer(ref this), fileSystem, data, size);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(56)]
    [return: NativeTypeName("SlangResult")]
    public int saveRepro(ISlangBlob** outBlob)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, ISlangBlob**, int>)(lpVtbl[56]))((ICompileRequest*)Unsafe.AsPointer(ref this), outBlob);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(57)]
    [return: NativeTypeName("SlangResult")]
    public int enableReproCapture()
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int>)(lpVtbl[57]))((ICompileRequest*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(58)]
    [return: NativeTypeName("SlangResult")]
    public int getProgram([NativeTypeName("slang::IComponentType **")] IComponentType** outProgram)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, IComponentType**, int>)(lpVtbl[58]))((ICompileRequest*)Unsafe.AsPointer(ref this), outProgram);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(59)]
    [return: NativeTypeName("SlangResult")]
    public int getEntryPoint([NativeTypeName("SlangInt")] long entryPointIndex, [NativeTypeName("slang::IComponentType **")] IComponentType** outEntryPoint)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, long, IComponentType**, int>)(lpVtbl[59]))((ICompileRequest*)Unsafe.AsPointer(ref this), entryPointIndex, outEntryPoint);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(60)]
    [return: NativeTypeName("SlangResult")]
    public int getModule([NativeTypeName("SlangInt")] long translationUnitIndex, [NativeTypeName("slang::IModule **")] IModule** outModule)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, long, IModule**, int>)(lpVtbl[60]))((ICompileRequest*)Unsafe.AsPointer(ref this), translationUnitIndex, outModule);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(61)]
    [return: NativeTypeName("SlangResult")]
    public int getSession([NativeTypeName("slang::ISession **")] ISession** outSession)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, ISession**, int>)(lpVtbl[61]))((ICompileRequest*)Unsafe.AsPointer(ref this), outSession);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(62)]
    [return: NativeTypeName("SlangReflection *")]
    public SlangProgramLayout* getReflection()
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, SlangProgramLayout*>)(lpVtbl[62]))((ICompileRequest*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(63)]
    public void setCommandLineCompilerMode()
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, void>)(lpVtbl[63]))((ICompileRequest*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(64)]
    [return: NativeTypeName("SlangResult")]
    public int addTargetCapability([NativeTypeName("SlangInt")] long targetIndex, SlangCapabilityID capability)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, long, SlangCapabilityID, int>)(lpVtbl[64]))((ICompileRequest*)Unsafe.AsPointer(ref this), targetIndex, capability);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(65)]
    [return: NativeTypeName("SlangResult")]
    public int getProgramWithEntryPoints([NativeTypeName("slang::IComponentType **")] IComponentType** outProgram)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, IComponentType**, int>)(lpVtbl[65]))((ICompileRequest*)Unsafe.AsPointer(ref this), outProgram);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(66)]
    [return: NativeTypeName("SlangResult")]
    public int isParameterLocationUsed([NativeTypeName("SlangInt")] long entryPointIndex, [NativeTypeName("SlangInt")] long targetIndex, SlangParameterCategory category, [NativeTypeName("SlangUInt")] ulong spaceIndex, [NativeTypeName("SlangUInt")] ulong registerIndex, [NativeTypeName("bool &")] bool* outUsed)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, long, long, SlangParameterCategory, ulong, ulong, bool*, int>)(lpVtbl[66]))((ICompileRequest*)Unsafe.AsPointer(ref this), entryPointIndex, targetIndex, category, spaceIndex, registerIndex, outUsed);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(67)]
    public void setTargetLineDirectiveMode([NativeTypeName("SlangInt")] long targetIndex, SlangLineDirectiveMode mode)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, long, SlangLineDirectiveMode, void>)(lpVtbl[67]))((ICompileRequest*)Unsafe.AsPointer(ref this), targetIndex, mode);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(68)]
    public void setTargetForceGLSLScalarBufferLayout(int targetIndex, bool forceScalarLayout)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int, bool, void>)(lpVtbl[68]))((ICompileRequest*)Unsafe.AsPointer(ref this), targetIndex, forceScalarLayout);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(69)]
    public void overrideDiagnosticSeverity([NativeTypeName("SlangInt")] long messageID, SlangSeverity overrideSeverity)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, long, SlangSeverity, void>)(lpVtbl[69]))((ICompileRequest*)Unsafe.AsPointer(ref this), messageID, overrideSeverity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(70)]
    [return: NativeTypeName("SlangDiagnosticFlags")]
    public int getDiagnosticFlags()
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int>)(lpVtbl[70]))((ICompileRequest*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(71)]
    public void setDiagnosticFlags([NativeTypeName("SlangDiagnosticFlags")] int flags)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int, void>)(lpVtbl[71]))((ICompileRequest*)Unsafe.AsPointer(ref this), flags);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(72)]
    public void setDebugInfoFormat(SlangDebugInfoFormat debugFormat)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, SlangDebugInfoFormat, void>)(lpVtbl[72]))((ICompileRequest*)Unsafe.AsPointer(ref this), debugFormat);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(73)]
    public void setEnableEffectAnnotations(bool value)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, bool, void>)(lpVtbl[73]))((ICompileRequest*)Unsafe.AsPointer(ref this), value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(74)]
    public void setReportDownstreamTime(bool value)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, bool, void>)(lpVtbl[74]))((ICompileRequest*)Unsafe.AsPointer(ref this), value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(75)]
    public void setReportPerfBenchmark(bool value)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, bool, void>)(lpVtbl[75]))((ICompileRequest*)Unsafe.AsPointer(ref this), value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(76)]
    public void setSkipSPIRVValidation(bool value)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, bool, void>)(lpVtbl[76]))((ICompileRequest*)Unsafe.AsPointer(ref this), value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(77)]
    public void setTargetUseMinimumSlangOptimization(int targetIndex, bool value)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int, bool, void>)(lpVtbl[77]))((ICompileRequest*)Unsafe.AsPointer(ref this), targetIndex, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(78)]
    public void setIgnoreCapabilityCheck(bool value)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, bool, void>)(lpVtbl[78]))((ICompileRequest*)Unsafe.AsPointer(ref this), value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(79)]
    [return: NativeTypeName("SlangResult")]
    public int getCompileTimeProfile(ISlangProfiler** compileTimeProfile, bool shouldClear)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileRequest*, ISlangProfiler**, bool, int>)(lpVtbl[79]))((ICompileRequest*)Unsafe.AsPointer(ref this), compileTimeProfile, shouldClear);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(80)]
    public void setTargetGenerateWholeProgram(int targetIndex, bool value)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int, bool, void>)(lpVtbl[80]))((ICompileRequest*)Unsafe.AsPointer(ref this), targetIndex, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(81)]
    public void setTargetForceDXLayout(int targetIndex, bool value)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int, bool, void>)(lpVtbl[81]))((ICompileRequest*)Unsafe.AsPointer(ref this), targetIndex, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(82)]
    public void setTargetEmbedDownstreamIR(int targetIndex, bool value)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int, bool, void>)(lpVtbl[82]))((ICompileRequest*)Unsafe.AsPointer(ref this), targetIndex, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(83)]
    public void setTargetForceCLayout(int targetIndex, bool value)
    {
        ((delegate* unmanaged[MemberFunction]<ICompileRequest*, int, bool, void>)(lpVtbl[83]))((ICompileRequest*)Unsafe.AsPointer(ref this), targetIndex, value);
    }
}
