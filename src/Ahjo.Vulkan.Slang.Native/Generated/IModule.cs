using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("struct IModule : slang::IComponentType")]
[NativeInheritance("IComponentType")]
public unsafe partial struct IModule
{
    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(0)]
    [return: NativeTypeName("SlangResult")]
    public int queryInterface([NativeTypeName("const SlangUUID &")] SlangUUID* uuid, void** outObject)
    {
        return ((delegate* unmanaged[MemberFunction]<IModule*, SlangUUID*, void**, int>)(lpVtbl[0]))((IModule*)Unsafe.AsPointer(ref this), uuid, outObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(1)]
    [return: NativeTypeName("uint32_t")]
    public uint addRef()
    {
        return ((delegate* unmanaged[MemberFunction]<IModule*, uint>)(lpVtbl[1]))((IModule*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(2)]
    [return: NativeTypeName("uint32_t")]
    public uint release()
    {
        return ((delegate* unmanaged[MemberFunction]<IModule*, uint>)(lpVtbl[2]))((IModule*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(3)]
    [return: NativeTypeName("slang::ISession *")]
    public ISession* getSession()
    {
        return ((delegate* unmanaged[MemberFunction]<IModule*, ISession*>)(lpVtbl[3]))((IModule*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(4)]
    [return: NativeTypeName("slang::ProgramLayout *")]
    public ShaderReflection* getLayout([NativeTypeName("SlangInt")] long targetIndex = 0, [NativeTypeName("IBlob **")] ISlangBlob** outDiagnostics = null)
    {
        return ((delegate* unmanaged[MemberFunction]<IModule*, long, ISlangBlob**, ShaderReflection*>)(lpVtbl[4]))((IModule*)Unsafe.AsPointer(ref this), targetIndex, outDiagnostics);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(5)]
    [return: NativeTypeName("SlangInt")]
    public long getSpecializationParamCount()
    {
        return ((delegate* unmanaged[MemberFunction]<IModule*, long>)(lpVtbl[5]))((IModule*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(6)]
    [return: NativeTypeName("SlangResult")]
    public int getEntryPointCode([NativeTypeName("SlangInt")] long entryPointIndex, [NativeTypeName("SlangInt")] long targetIndex, [NativeTypeName("IBlob **")] ISlangBlob** outCode, [NativeTypeName("IBlob **")] ISlangBlob** outDiagnostics = null)
    {
        return ((delegate* unmanaged[MemberFunction]<IModule*, long, long, ISlangBlob**, ISlangBlob**, int>)(lpVtbl[6]))((IModule*)Unsafe.AsPointer(ref this), entryPointIndex, targetIndex, outCode, outDiagnostics);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(7)]
    [return: NativeTypeName("SlangResult")]
    public int getResultAsFileSystem([NativeTypeName("SlangInt")] long entryPointIndex, [NativeTypeName("SlangInt")] long targetIndex, ISlangMutableFileSystem** outFileSystem)
    {
        return ((delegate* unmanaged[MemberFunction]<IModule*, long, long, ISlangMutableFileSystem**, int>)(lpVtbl[7]))((IModule*)Unsafe.AsPointer(ref this), entryPointIndex, targetIndex, outFileSystem);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(8)]
    public void getEntryPointHash([NativeTypeName("SlangInt")] long entryPointIndex, [NativeTypeName("SlangInt")] long targetIndex, [NativeTypeName("IBlob **")] ISlangBlob** outHash)
    {
        ((delegate* unmanaged[MemberFunction]<IModule*, long, long, ISlangBlob**, void>)(lpVtbl[8]))((IModule*)Unsafe.AsPointer(ref this), entryPointIndex, targetIndex, outHash);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(9)]
    [return: NativeTypeName("SlangResult")]
    public int specialize([NativeTypeName("const SpecializationArg *")] SpecializationArg* specializationArgs, [NativeTypeName("SlangInt")] long specializationArgCount, IComponentType** outSpecializedComponentType, ISlangBlob** outDiagnostics = null)
    {
        return ((delegate* unmanaged[MemberFunction]<IModule*, SpecializationArg*, long, IComponentType**, ISlangBlob**, int>)(lpVtbl[9]))((IModule*)Unsafe.AsPointer(ref this), specializationArgs, specializationArgCount, outSpecializedComponentType, outDiagnostics);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(10)]
    [return: NativeTypeName("SlangResult")]
    public int link(IComponentType** outLinkedComponentType, ISlangBlob** outDiagnostics = null)
    {
        return ((delegate* unmanaged[MemberFunction]<IModule*, IComponentType**, ISlangBlob**, int>)(lpVtbl[10]))((IModule*)Unsafe.AsPointer(ref this), outLinkedComponentType, outDiagnostics);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(11)]
    [return: NativeTypeName("SlangResult")]
    public int getEntryPointHostCallable(int entryPointIndex, int targetIndex, ISlangSharedLibrary** outSharedLibrary, [NativeTypeName("slang::IBlob **")] ISlangBlob** outDiagnostics = null)
    {
        return ((delegate* unmanaged[MemberFunction]<IModule*, int, int, ISlangSharedLibrary**, ISlangBlob**, int>)(lpVtbl[11]))((IModule*)Unsafe.AsPointer(ref this), entryPointIndex, targetIndex, outSharedLibrary, outDiagnostics);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(12)]
    [return: NativeTypeName("SlangResult")]
    public int renameEntryPoint([NativeTypeName("const char *")] sbyte* newName, IComponentType** outEntryPoint)
    {
        return ((delegate* unmanaged[MemberFunction]<IModule*, sbyte*, IComponentType**, int>)(lpVtbl[12]))((IModule*)Unsafe.AsPointer(ref this), newName, outEntryPoint);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(13)]
    [return: NativeTypeName("SlangResult")]
    public int linkWithOptions(IComponentType** outLinkedComponentType, [NativeTypeName("uint32_t")] uint compilerOptionEntryCount, [NativeTypeName("const CompilerOptionEntry *")] CompilerOptionEntry* compilerOptionEntries, ISlangBlob** outDiagnostics = null)
    {
        return ((delegate* unmanaged[MemberFunction]<IModule*, IComponentType**, uint, CompilerOptionEntry*, ISlangBlob**, int>)(lpVtbl[13]))((IModule*)Unsafe.AsPointer(ref this), outLinkedComponentType, compilerOptionEntryCount, compilerOptionEntries, outDiagnostics);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(14)]
    [return: NativeTypeName("SlangResult")]
    public int getTargetCode([NativeTypeName("SlangInt")] long targetIndex, [NativeTypeName("IBlob **")] ISlangBlob** outCode, [NativeTypeName("IBlob **")] ISlangBlob** outDiagnostics = null)
    {
        return ((delegate* unmanaged[MemberFunction]<IModule*, long, ISlangBlob**, ISlangBlob**, int>)(lpVtbl[14]))((IModule*)Unsafe.AsPointer(ref this), targetIndex, outCode, outDiagnostics);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(15)]
    [return: NativeTypeName("SlangResult")]
    public int getTargetMetadata([NativeTypeName("SlangInt")] long targetIndex, IMetadata** outMetadata, [NativeTypeName("IBlob **")] ISlangBlob** outDiagnostics = null)
    {
        return ((delegate* unmanaged[MemberFunction]<IModule*, long, IMetadata**, ISlangBlob**, int>)(lpVtbl[15]))((IModule*)Unsafe.AsPointer(ref this), targetIndex, outMetadata, outDiagnostics);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(16)]
    [return: NativeTypeName("SlangResult")]
    public int getEntryPointMetadata([NativeTypeName("SlangInt")] long entryPointIndex, [NativeTypeName("SlangInt")] long targetIndex, IMetadata** outMetadata, [NativeTypeName("IBlob **")] ISlangBlob** outDiagnostics = null)
    {
        return ((delegate* unmanaged[MemberFunction]<IModule*, long, long, IMetadata**, ISlangBlob**, int>)(lpVtbl[16]))((IModule*)Unsafe.AsPointer(ref this), entryPointIndex, targetIndex, outMetadata, outDiagnostics);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(17)]
    [return: NativeTypeName("SlangResult")]
    public int findEntryPointByName([NativeTypeName("const char *")] sbyte* name, IEntryPoint** outEntryPoint)
    {
        return ((delegate* unmanaged[MemberFunction]<IModule*, sbyte*, IEntryPoint**, int>)(lpVtbl[17]))((IModule*)Unsafe.AsPointer(ref this), name, outEntryPoint);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(18)]
    [return: NativeTypeName("SlangInt32")]
    public int getDefinedEntryPointCount()
    {
        return ((delegate* unmanaged[MemberFunction]<IModule*, int>)(lpVtbl[18]))((IModule*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(19)]
    [return: NativeTypeName("SlangResult")]
    public int getDefinedEntryPoint([NativeTypeName("SlangInt32")] int index, IEntryPoint** outEntryPoint)
    {
        return ((delegate* unmanaged[MemberFunction]<IModule*, int, IEntryPoint**, int>)(lpVtbl[19]))((IModule*)Unsafe.AsPointer(ref this), index, outEntryPoint);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(20)]
    [return: NativeTypeName("SlangResult")]
    public int serialize(ISlangBlob** outSerializedBlob)
    {
        return ((delegate* unmanaged[MemberFunction]<IModule*, ISlangBlob**, int>)(lpVtbl[20]))((IModule*)Unsafe.AsPointer(ref this), outSerializedBlob);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(21)]
    [return: NativeTypeName("SlangResult")]
    public int writeToFile([NativeTypeName("const char *")] sbyte* fileName)
    {
        return ((delegate* unmanaged[MemberFunction]<IModule*, sbyte*, int>)(lpVtbl[21]))((IModule*)Unsafe.AsPointer(ref this), fileName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(22)]
    [return: NativeTypeName("const char *")]
    public sbyte* getName()
    {
        return ((delegate* unmanaged[MemberFunction]<IModule*, sbyte*>)(lpVtbl[22]))((IModule*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(23)]
    [return: NativeTypeName("const char *")]
    public sbyte* getFilePath()
    {
        return ((delegate* unmanaged[MemberFunction]<IModule*, sbyte*>)(lpVtbl[23]))((IModule*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(24)]
    [return: NativeTypeName("const char *")]
    public sbyte* getUniqueIdentity()
    {
        return ((delegate* unmanaged[MemberFunction]<IModule*, sbyte*>)(lpVtbl[24]))((IModule*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(25)]
    [return: NativeTypeName("SlangResult")]
    public int findAndCheckEntryPoint([NativeTypeName("const char *")] sbyte* name, SlangStage stage, IEntryPoint** outEntryPoint, ISlangBlob** outDiagnostics)
    {
        return ((delegate* unmanaged[MemberFunction]<IModule*, sbyte*, SlangStage, IEntryPoint**, ISlangBlob**, int>)(lpVtbl[25]))((IModule*)Unsafe.AsPointer(ref this), name, stage, outEntryPoint, outDiagnostics);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(26)]
    [return: NativeTypeName("SlangInt32")]
    public int getDependencyFileCount()
    {
        return ((delegate* unmanaged[MemberFunction]<IModule*, int>)(lpVtbl[26]))((IModule*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(27)]
    [return: NativeTypeName("const char *")]
    public sbyte* getDependencyFilePath([NativeTypeName("SlangInt32")] int index)
    {
        return ((delegate* unmanaged[MemberFunction]<IModule*, int, sbyte*>)(lpVtbl[27]))((IModule*)Unsafe.AsPointer(ref this), index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(28)]
    [return: NativeTypeName("slang::DeclReflection *")]
    public DeclReflection* getModuleReflection()
    {
        return ((delegate* unmanaged[MemberFunction]<IModule*, DeclReflection*>)(lpVtbl[28]))((IModule*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(29)]
    [return: NativeTypeName("SlangResult")]
    public int disassemble([NativeTypeName("slang::IBlob **")] ISlangBlob** outDisassembledBlob)
    {
        return ((delegate* unmanaged[MemberFunction]<IModule*, ISlangBlob**, int>)(lpVtbl[29]))((IModule*)Unsafe.AsPointer(ref this), outDisassembledBlob);
    }
}
