using System;
using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("struct IGlobalSession : ISlangUnknown")]
[NativeInheritance("ISlangUnknown")]
public unsafe partial struct IGlobalSession
{
    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(0)]
    [return: NativeTypeName("SlangResult")]
    public int queryInterface([NativeTypeName("const SlangUUID &")] SlangUUID* uuid, void** outObject)
    {
        return ((delegate* unmanaged[MemberFunction]<IGlobalSession*, SlangUUID*, void**, int>)(lpVtbl[0]))((IGlobalSession*)Unsafe.AsPointer(ref this), uuid, outObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(1)]
    [return: NativeTypeName("uint32_t")]
    public uint addRef()
    {
        return ((delegate* unmanaged[MemberFunction]<IGlobalSession*, uint>)(lpVtbl[1]))((IGlobalSession*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(2)]
    [return: NativeTypeName("uint32_t")]
    public uint release()
    {
        return ((delegate* unmanaged[MemberFunction]<IGlobalSession*, uint>)(lpVtbl[2]))((IGlobalSession*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(3)]
    [return: NativeTypeName("SlangResult")]
    public int createSession([NativeTypeName("const SessionDesc &")] SessionDesc* desc, ISession** outSession)
    {
        return ((delegate* unmanaged[MemberFunction]<IGlobalSession*, SessionDesc*, ISession**, int>)(lpVtbl[3]))((IGlobalSession*)Unsafe.AsPointer(ref this), desc, outSession);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(4)]
    public SlangProfileID findProfile([NativeTypeName("const char *")] sbyte* name)
    {
        return ((delegate* unmanaged[MemberFunction]<IGlobalSession*, sbyte*, SlangProfileID>)(lpVtbl[4]))((IGlobalSession*)Unsafe.AsPointer(ref this), name);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(5)]
    public void setDownstreamCompilerPath(SlangPassThrough passThrough, [NativeTypeName("const char *")] sbyte* path)
    {
        ((delegate* unmanaged[MemberFunction]<IGlobalSession*, SlangPassThrough, sbyte*, void>)(lpVtbl[5]))((IGlobalSession*)Unsafe.AsPointer(ref this), passThrough, path);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(6)]
    public void setDownstreamCompilerPrelude(SlangPassThrough passThrough, [NativeTypeName("const char *")] sbyte* preludeText)
    {
        ((delegate* unmanaged[MemberFunction]<IGlobalSession*, SlangPassThrough, sbyte*, void>)(lpVtbl[6]))((IGlobalSession*)Unsafe.AsPointer(ref this), passThrough, preludeText);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(7)]
    public void getDownstreamCompilerPrelude(SlangPassThrough passThrough, ISlangBlob** outPrelude)
    {
        ((delegate* unmanaged[MemberFunction]<IGlobalSession*, SlangPassThrough, ISlangBlob**, void>)(lpVtbl[7]))((IGlobalSession*)Unsafe.AsPointer(ref this), passThrough, outPrelude);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(8)]
    [return: NativeTypeName("const char *")]
    public sbyte* getBuildTagString()
    {
        return ((delegate* unmanaged[MemberFunction]<IGlobalSession*, sbyte*>)(lpVtbl[8]))((IGlobalSession*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(9)]
    [return: NativeTypeName("SlangResult")]
    public int setDefaultDownstreamCompiler(SlangSourceLanguage sourceLanguage, SlangPassThrough defaultCompiler)
    {
        return ((delegate* unmanaged[MemberFunction]<IGlobalSession*, SlangSourceLanguage, SlangPassThrough, int>)(lpVtbl[9]))((IGlobalSession*)Unsafe.AsPointer(ref this), sourceLanguage, defaultCompiler);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(10)]
    public SlangPassThrough getDefaultDownstreamCompiler(SlangSourceLanguage sourceLanguage)
    {
        return ((delegate* unmanaged[MemberFunction]<IGlobalSession*, SlangSourceLanguage, SlangPassThrough>)(lpVtbl[10]))((IGlobalSession*)Unsafe.AsPointer(ref this), sourceLanguage);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(11)]
    public void setLanguagePrelude(SlangSourceLanguage sourceLanguage, [NativeTypeName("const char *")] sbyte* preludeText)
    {
        ((delegate* unmanaged[MemberFunction]<IGlobalSession*, SlangSourceLanguage, sbyte*, void>)(lpVtbl[11]))((IGlobalSession*)Unsafe.AsPointer(ref this), sourceLanguage, preludeText);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(12)]
    public void getLanguagePrelude(SlangSourceLanguage sourceLanguage, ISlangBlob** outPrelude)
    {
        ((delegate* unmanaged[MemberFunction]<IGlobalSession*, SlangSourceLanguage, ISlangBlob**, void>)(lpVtbl[12]))((IGlobalSession*)Unsafe.AsPointer(ref this), sourceLanguage, outPrelude);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(13)]
    [return: NativeTypeName("SlangResult")]
    [Obsolete]
    public int createCompileRequest([NativeTypeName("slang::ICompileRequest **")] ICompileRequest** outCompileRequest)
    {
        return ((delegate* unmanaged[MemberFunction]<IGlobalSession*, ICompileRequest**, int>)(lpVtbl[13]))((IGlobalSession*)Unsafe.AsPointer(ref this), outCompileRequest);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(14)]
    public void addBuiltins([NativeTypeName("const char *")] sbyte* sourcePath, [NativeTypeName("const char *")] sbyte* sourceString)
    {
        ((delegate* unmanaged[MemberFunction]<IGlobalSession*, sbyte*, sbyte*, void>)(lpVtbl[14]))((IGlobalSession*)Unsafe.AsPointer(ref this), sourcePath, sourceString);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(15)]
    public void setSharedLibraryLoader(ISlangSharedLibraryLoader* loader)
    {
        ((delegate* unmanaged[MemberFunction]<IGlobalSession*, ISlangSharedLibraryLoader*, void>)(lpVtbl[15]))((IGlobalSession*)Unsafe.AsPointer(ref this), loader);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(16)]
    public ISlangSharedLibraryLoader* getSharedLibraryLoader()
    {
        return ((delegate* unmanaged[MemberFunction]<IGlobalSession*, ISlangSharedLibraryLoader*>)(lpVtbl[16]))((IGlobalSession*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(17)]
    [return: NativeTypeName("SlangResult")]
    public int checkCompileTargetSupport(SlangCompileTarget target)
    {
        return ((delegate* unmanaged[MemberFunction]<IGlobalSession*, SlangCompileTarget, int>)(lpVtbl[17]))((IGlobalSession*)Unsafe.AsPointer(ref this), target);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(18)]
    [return: NativeTypeName("SlangResult")]
    public int checkPassThroughSupport(SlangPassThrough passThrough)
    {
        return ((delegate* unmanaged[MemberFunction]<IGlobalSession*, SlangPassThrough, int>)(lpVtbl[18]))((IGlobalSession*)Unsafe.AsPointer(ref this), passThrough);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(19)]
    [return: NativeTypeName("SlangResult")]
    public int compileCoreModule([NativeTypeName("slang::CompileCoreModuleFlags")] uint flags)
    {
        return ((delegate* unmanaged[MemberFunction]<IGlobalSession*, uint, int>)(lpVtbl[19]))((IGlobalSession*)Unsafe.AsPointer(ref this), flags);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(20)]
    [return: NativeTypeName("SlangResult")]
    public int loadCoreModule([NativeTypeName("const void *")] void* coreModule, [NativeTypeName("size_t")] nuint coreModuleSizeInBytes)
    {
        return ((delegate* unmanaged[MemberFunction]<IGlobalSession*, void*, nuint, int>)(lpVtbl[20]))((IGlobalSession*)Unsafe.AsPointer(ref this), coreModule, coreModuleSizeInBytes);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(21)]
    [return: NativeTypeName("SlangResult")]
    public int saveCoreModule(SlangArchiveType archiveType, ISlangBlob** outBlob)
    {
        return ((delegate* unmanaged[MemberFunction]<IGlobalSession*, SlangArchiveType, ISlangBlob**, int>)(lpVtbl[21]))((IGlobalSession*)Unsafe.AsPointer(ref this), archiveType, outBlob);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(22)]
    public SlangCapabilityID findCapability([NativeTypeName("const char *")] sbyte* name)
    {
        return ((delegate* unmanaged[MemberFunction]<IGlobalSession*, sbyte*, SlangCapabilityID>)(lpVtbl[22]))((IGlobalSession*)Unsafe.AsPointer(ref this), name);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(23)]
    public void setDownstreamCompilerForTransition(SlangCompileTarget source, SlangCompileTarget target, SlangPassThrough compiler)
    {
        ((delegate* unmanaged[MemberFunction]<IGlobalSession*, SlangCompileTarget, SlangCompileTarget, SlangPassThrough, void>)(lpVtbl[23]))((IGlobalSession*)Unsafe.AsPointer(ref this), source, target, compiler);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(24)]
    public SlangPassThrough getDownstreamCompilerForTransition(SlangCompileTarget source, SlangCompileTarget target)
    {
        return ((delegate* unmanaged[MemberFunction]<IGlobalSession*, SlangCompileTarget, SlangCompileTarget, SlangPassThrough>)(lpVtbl[24]))((IGlobalSession*)Unsafe.AsPointer(ref this), source, target);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(25)]
    public void getCompilerElapsedTime(double* outTotalTime, double* outDownstreamTime)
    {
        ((delegate* unmanaged[MemberFunction]<IGlobalSession*, double*, double*, void>)(lpVtbl[25]))((IGlobalSession*)Unsafe.AsPointer(ref this), outTotalTime, outDownstreamTime);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(26)]
    [return: NativeTypeName("SlangResult")]
    public int setSPIRVCoreGrammar([NativeTypeName("const char *")] sbyte* jsonPath)
    {
        return ((delegate* unmanaged[MemberFunction]<IGlobalSession*, sbyte*, int>)(lpVtbl[26]))((IGlobalSession*)Unsafe.AsPointer(ref this), jsonPath);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(27)]
    [return: NativeTypeName("SlangResult")]
    public int parseCommandLineArguments(int argc, [NativeTypeName("const char *const *")] sbyte** argv, [NativeTypeName("slang::SessionDesc *")] SessionDesc* outSessionDesc, ISlangUnknown** outAuxAllocation)
    {
        return ((delegate* unmanaged[MemberFunction]<IGlobalSession*, int, sbyte**, SessionDesc*, ISlangUnknown**, int>)(lpVtbl[27]))((IGlobalSession*)Unsafe.AsPointer(ref this), argc, argv, outSessionDesc, outAuxAllocation);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(28)]
    [return: NativeTypeName("SlangResult")]
    public int getSessionDescDigest([NativeTypeName("slang::SessionDesc *")] SessionDesc* sessionDesc, ISlangBlob** outBlob)
    {
        return ((delegate* unmanaged[MemberFunction]<IGlobalSession*, SessionDesc*, ISlangBlob**, int>)(lpVtbl[28]))((IGlobalSession*)Unsafe.AsPointer(ref this), sessionDesc, outBlob);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(29)]
    [return: NativeTypeName("SlangResult")]
    public int compileBuiltinModule([NativeTypeName("slang::BuiltinModuleName")] BuiltinModuleName module, [NativeTypeName("slang::CompileCoreModuleFlags")] uint flags)
    {
        return ((delegate* unmanaged[MemberFunction]<IGlobalSession*, BuiltinModuleName, uint, int>)(lpVtbl[29]))((IGlobalSession*)Unsafe.AsPointer(ref this), module, flags);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(30)]
    [return: NativeTypeName("SlangResult")]
    public int loadBuiltinModule([NativeTypeName("slang::BuiltinModuleName")] BuiltinModuleName module, [NativeTypeName("const void *")] void* moduleData, [NativeTypeName("size_t")] nuint sizeInBytes)
    {
        return ((delegate* unmanaged[MemberFunction]<IGlobalSession*, BuiltinModuleName, void*, nuint, int>)(lpVtbl[30]))((IGlobalSession*)Unsafe.AsPointer(ref this), module, moduleData, sizeInBytes);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(31)]
    [return: NativeTypeName("SlangResult")]
    public int saveBuiltinModule([NativeTypeName("slang::BuiltinModuleName")] BuiltinModuleName module, SlangArchiveType archiveType, ISlangBlob** outBlob)
    {
        return ((delegate* unmanaged[MemberFunction]<IGlobalSession*, BuiltinModuleName, SlangArchiveType, ISlangBlob**, int>)(lpVtbl[31]))((IGlobalSession*)Unsafe.AsPointer(ref this), module, archiveType, outBlob);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(32)]
    [return: NativeTypeName("SlangResult")]
    public int getDownstreamCompilerVersion(SlangPassThrough passThrough, int* outMajor, int* outMinor)
    {
        return ((delegate* unmanaged[MemberFunction]<IGlobalSession*, SlangPassThrough, int*, int*, int>)(lpVtbl[32]))((IGlobalSession*)Unsafe.AsPointer(ref this), passThrough, outMajor, outMinor);
    }
}
