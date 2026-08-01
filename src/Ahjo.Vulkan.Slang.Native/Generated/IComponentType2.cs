using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("struct IComponentType2 : ISlangUnknown")]
[NativeInheritance("ISlangUnknown")]
public unsafe partial struct IComponentType2
{
    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(0)]
    [return: NativeTypeName("SlangResult")]
    public int queryInterface([NativeTypeName("const SlangUUID &")] SlangUUID* uuid, void** outObject)
    {
        return ((delegate* unmanaged[MemberFunction]<IComponentType2*, SlangUUID*, void**, int>)(lpVtbl[0]))((IComponentType2*)Unsafe.AsPointer(ref this), uuid, outObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(1)]
    [return: NativeTypeName("uint32_t")]
    public uint addRef()
    {
        return ((delegate* unmanaged[MemberFunction]<IComponentType2*, uint>)(lpVtbl[1]))((IComponentType2*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(2)]
    [return: NativeTypeName("uint32_t")]
    public uint release()
    {
        return ((delegate* unmanaged[MemberFunction]<IComponentType2*, uint>)(lpVtbl[2]))((IComponentType2*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(3)]
    [return: NativeTypeName("SlangResult")]
    public int getTargetCompileResult([NativeTypeName("SlangInt")] long targetIndex, ICompileResult** outCompileResult, [NativeTypeName("IBlob **")] ISlangBlob** outDiagnostics = null)
    {
        return ((delegate* unmanaged[MemberFunction]<IComponentType2*, long, ICompileResult**, ISlangBlob**, int>)(lpVtbl[3]))((IComponentType2*)Unsafe.AsPointer(ref this), targetIndex, outCompileResult, outDiagnostics);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(4)]
    [return: NativeTypeName("SlangResult")]
    public int getEntryPointCompileResult([NativeTypeName("SlangInt")] long entryPointIndex, [NativeTypeName("SlangInt")] long targetIndex, ICompileResult** outCompileResult, [NativeTypeName("IBlob **")] ISlangBlob** outDiagnostics = null)
    {
        return ((delegate* unmanaged[MemberFunction]<IComponentType2*, long, long, ICompileResult**, ISlangBlob**, int>)(lpVtbl[4]))((IComponentType2*)Unsafe.AsPointer(ref this), entryPointIndex, targetIndex, outCompileResult, outDiagnostics);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(5)]
    [return: NativeTypeName("SlangResult")]
    public int getTargetHostCallable(int targetIndex, ISlangSharedLibrary** outSharedLibrary, [NativeTypeName("slang::IBlob **")] ISlangBlob** outDiagnostics = null)
    {
        return ((delegate* unmanaged[MemberFunction]<IComponentType2*, int, ISlangSharedLibrary**, ISlangBlob**, int>)(lpVtbl[5]))((IComponentType2*)Unsafe.AsPointer(ref this), targetIndex, outSharedLibrary, outDiagnostics);
    }
}
