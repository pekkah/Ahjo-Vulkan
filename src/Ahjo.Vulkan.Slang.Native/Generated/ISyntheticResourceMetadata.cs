using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("struct ISyntheticResourceMetadata : ISlangCastable")]
[NativeInheritance("ISlangCastable")]
public unsafe partial struct ISyntheticResourceMetadata
{
    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(0)]
    [return: NativeTypeName("SlangResult")]
    public int queryInterface([NativeTypeName("const SlangUUID &")] SlangUUID* uuid, void** outObject)
    {
        return ((delegate* unmanaged[MemberFunction]<ISyntheticResourceMetadata*, SlangUUID*, void**, int>)(lpVtbl[0]))((ISyntheticResourceMetadata*)Unsafe.AsPointer(ref this), uuid, outObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(1)]
    [return: NativeTypeName("uint32_t")]
    public uint addRef()
    {
        return ((delegate* unmanaged[MemberFunction]<ISyntheticResourceMetadata*, uint>)(lpVtbl[1]))((ISyntheticResourceMetadata*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(2)]
    [return: NativeTypeName("uint32_t")]
    public uint release()
    {
        return ((delegate* unmanaged[MemberFunction]<ISyntheticResourceMetadata*, uint>)(lpVtbl[2]))((ISyntheticResourceMetadata*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(3)]
    public void* castAs([NativeTypeName("const SlangUUID &")] SlangUUID* guid)
    {
        return ((delegate* unmanaged[MemberFunction]<ISyntheticResourceMetadata*, SlangUUID*, void*>)(lpVtbl[3]))((ISyntheticResourceMetadata*)Unsafe.AsPointer(ref this), guid);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(4)]
    [return: NativeTypeName("uint32_t")]
    public uint getResourceCount()
    {
        return ((delegate* unmanaged[MemberFunction]<ISyntheticResourceMetadata*, uint>)(lpVtbl[4]))((ISyntheticResourceMetadata*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(5)]
    [return: NativeTypeName("SlangResult")]
    public int getResourceInfo([NativeTypeName("uint32_t")] uint index, [NativeTypeName("slang::SyntheticResourceInfo *")] SyntheticResourceInfo* outInfo)
    {
        return ((delegate* unmanaged[MemberFunction]<ISyntheticResourceMetadata*, uint, SyntheticResourceInfo*, int>)(lpVtbl[5]))((ISyntheticResourceMetadata*)Unsafe.AsPointer(ref this), index, outInfo);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(6)]
    [return: NativeTypeName("SlangResult")]
    public int findResourceIndexByID([NativeTypeName("uint32_t")] uint id, [NativeTypeName("uint32_t *")] uint* outIndex)
    {
        return ((delegate* unmanaged[MemberFunction]<ISyntheticResourceMetadata*, uint, uint*, int>)(lpVtbl[6]))((ISyntheticResourceMetadata*)Unsafe.AsPointer(ref this), id, outIndex);
    }
}
