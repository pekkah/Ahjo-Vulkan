using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("struct ICooperativeTypesMetadata : ISlangCastable")]
[NativeInheritance("ISlangCastable")]
public unsafe partial struct ICooperativeTypesMetadata
{
    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(0)]
    [return: NativeTypeName("SlangResult")]
    public int queryInterface([NativeTypeName("const SlangUUID &")] SlangUUID* uuid, void** outObject)
    {
        return ((delegate* unmanaged[MemberFunction]<ICooperativeTypesMetadata*, SlangUUID*, void**, int>)(lpVtbl[0]))((ICooperativeTypesMetadata*)Unsafe.AsPointer(ref this), uuid, outObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(1)]
    [return: NativeTypeName("uint32_t")]
    public uint addRef()
    {
        return ((delegate* unmanaged[MemberFunction]<ICooperativeTypesMetadata*, uint>)(lpVtbl[1]))((ICooperativeTypesMetadata*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(2)]
    [return: NativeTypeName("uint32_t")]
    public uint release()
    {
        return ((delegate* unmanaged[MemberFunction]<ICooperativeTypesMetadata*, uint>)(lpVtbl[2]))((ICooperativeTypesMetadata*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(3)]
    public void* castAs([NativeTypeName("const SlangUUID &")] SlangUUID* guid)
    {
        return ((delegate* unmanaged[MemberFunction]<ICooperativeTypesMetadata*, SlangUUID*, void*>)(lpVtbl[3]))((ICooperativeTypesMetadata*)Unsafe.AsPointer(ref this), guid);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(4)]
    [return: NativeTypeName("SlangUInt")]
    public ulong getCooperativeMatrixTypeCount()
    {
        return ((delegate* unmanaged[MemberFunction]<ICooperativeTypesMetadata*, ulong>)(lpVtbl[4]))((ICooperativeTypesMetadata*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(5)]
    [return: NativeTypeName("SlangResult")]
    public int getCooperativeMatrixTypeByIndex([NativeTypeName("SlangUInt")] ulong index, [NativeTypeName("slang::CooperativeMatrixType *")] CooperativeMatrixType* outType)
    {
        return ((delegate* unmanaged[MemberFunction]<ICooperativeTypesMetadata*, ulong, CooperativeMatrixType*, int>)(lpVtbl[5]))((ICooperativeTypesMetadata*)Unsafe.AsPointer(ref this), index, outType);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(6)]
    [return: NativeTypeName("SlangUInt")]
    public ulong getCooperativeMatrixCombinationCount()
    {
        return ((delegate* unmanaged[MemberFunction]<ICooperativeTypesMetadata*, ulong>)(lpVtbl[6]))((ICooperativeTypesMetadata*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(7)]
    [return: NativeTypeName("SlangResult")]
    public int getCooperativeMatrixCombinationByIndex([NativeTypeName("SlangUInt")] ulong index, [NativeTypeName("slang::CooperativeMatrixCombination *")] CooperativeMatrixCombination* outCombination)
    {
        return ((delegate* unmanaged[MemberFunction]<ICooperativeTypesMetadata*, ulong, CooperativeMatrixCombination*, int>)(lpVtbl[7]))((ICooperativeTypesMetadata*)Unsafe.AsPointer(ref this), index, outCombination);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(8)]
    [return: NativeTypeName("SlangUInt")]
    public ulong getCooperativeVectorTypeCount()
    {
        return ((delegate* unmanaged[MemberFunction]<ICooperativeTypesMetadata*, ulong>)(lpVtbl[8]))((ICooperativeTypesMetadata*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(9)]
    [return: NativeTypeName("SlangResult")]
    public int getCooperativeVectorTypeByIndex([NativeTypeName("SlangUInt")] ulong index, [NativeTypeName("slang::CooperativeVectorTypeUsageInfo *")] CooperativeVectorTypeUsageInfo* outType)
    {
        return ((delegate* unmanaged[MemberFunction]<ICooperativeTypesMetadata*, ulong, CooperativeVectorTypeUsageInfo*, int>)(lpVtbl[9]))((ICooperativeTypesMetadata*)Unsafe.AsPointer(ref this), index, outType);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(10)]
    [return: NativeTypeName("SlangUInt")]
    public ulong getCooperativeVectorCombinationCount()
    {
        return ((delegate* unmanaged[MemberFunction]<ICooperativeTypesMetadata*, ulong>)(lpVtbl[10]))((ICooperativeTypesMetadata*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(11)]
    [return: NativeTypeName("SlangResult")]
    public int getCooperativeVectorCombinationByIndex([NativeTypeName("SlangUInt")] ulong index, [NativeTypeName("slang::CooperativeVectorCombination *")] CooperativeVectorCombination* outCombination)
    {
        return ((delegate* unmanaged[MemberFunction]<ICooperativeTypesMetadata*, ulong, CooperativeVectorCombination*, int>)(lpVtbl[11]))((ICooperativeTypesMetadata*)Unsafe.AsPointer(ref this), index, outCombination);
    }
}
