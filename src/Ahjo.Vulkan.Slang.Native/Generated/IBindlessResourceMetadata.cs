using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("struct IBindlessResourceMetadata : ISlangCastable")]
[NativeInheritance("ISlangCastable")]
public unsafe partial struct IBindlessResourceMetadata
{
    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(0)]
    [return: NativeTypeName("SlangResult")]
    public int queryInterface([NativeTypeName("const SlangUUID &")] SlangUUID* uuid, void** outObject)
    {
        return ((delegate* unmanaged[MemberFunction]<IBindlessResourceMetadata*, SlangUUID*, void**, int>)(lpVtbl[0]))((IBindlessResourceMetadata*)Unsafe.AsPointer(ref this), uuid, outObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(1)]
    [return: NativeTypeName("uint32_t")]
    public uint addRef()
    {
        return ((delegate* unmanaged[MemberFunction]<IBindlessResourceMetadata*, uint>)(lpVtbl[1]))((IBindlessResourceMetadata*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(2)]
    [return: NativeTypeName("uint32_t")]
    public uint release()
    {
        return ((delegate* unmanaged[MemberFunction]<IBindlessResourceMetadata*, uint>)(lpVtbl[2]))((IBindlessResourceMetadata*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(3)]
    public void* castAs([NativeTypeName("const SlangUUID &")] SlangUUID* guid)
    {
        return ((delegate* unmanaged[MemberFunction]<IBindlessResourceMetadata*, SlangUUID*, void*>)(lpVtbl[3]))((IBindlessResourceMetadata*)Unsafe.AsPointer(ref this), guid);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(4)]
    public bool usesBindlessResourceHeap()
    {
        return ((delegate* unmanaged[MemberFunction]<IBindlessResourceMetadata*, bool>)(lpVtbl[4]))((IBindlessResourceMetadata*)Unsafe.AsPointer(ref this));
    }
}
