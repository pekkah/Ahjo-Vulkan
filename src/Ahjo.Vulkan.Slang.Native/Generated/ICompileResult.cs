using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("struct ICompileResult : ISlangCastable")]
[NativeInheritance("ISlangCastable")]
public unsafe partial struct ICompileResult
{
    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(0)]
    [return: NativeTypeName("SlangResult")]
    public int queryInterface([NativeTypeName("const SlangUUID &")] SlangUUID* uuid, void** outObject)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileResult*, SlangUUID*, void**, int>)(lpVtbl[0]))((ICompileResult*)Unsafe.AsPointer(ref this), uuid, outObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(1)]
    [return: NativeTypeName("uint32_t")]
    public uint addRef()
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileResult*, uint>)(lpVtbl[1]))((ICompileResult*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(2)]
    [return: NativeTypeName("uint32_t")]
    public uint release()
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileResult*, uint>)(lpVtbl[2]))((ICompileResult*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(3)]
    public void* castAs([NativeTypeName("const SlangUUID &")] SlangUUID* guid)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileResult*, SlangUUID*, void*>)(lpVtbl[3]))((ICompileResult*)Unsafe.AsPointer(ref this), guid);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(4)]
    [return: NativeTypeName("uint32_t")]
    public uint getItemCount()
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileResult*, uint>)(lpVtbl[4]))((ICompileResult*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(5)]
    [return: NativeTypeName("SlangResult")]
    public int getItemData([NativeTypeName("uint32_t")] uint index, [NativeTypeName("IBlob **")] ISlangBlob** outblob)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileResult*, uint, ISlangBlob**, int>)(lpVtbl[5]))((ICompileResult*)Unsafe.AsPointer(ref this), index, outblob);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(6)]
    [return: NativeTypeName("SlangResult")]
    public int getMetadata(IMetadata** outMetadata)
    {
        return ((delegate* unmanaged[MemberFunction]<ICompileResult*, IMetadata**, int>)(lpVtbl[6]))((ICompileResult*)Unsafe.AsPointer(ref this), outMetadata);
    }
}
