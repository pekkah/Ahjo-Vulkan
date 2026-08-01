using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("struct ISlangBlob : ISlangUnknown")]
[NativeInheritance("ISlangUnknown")]
public unsafe partial struct ISlangBlob
{
    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(0)]
    [return: NativeTypeName("SlangResult")]
    public int queryInterface([NativeTypeName("const SlangUUID &")] SlangUUID* uuid, void** outObject)
    {
        return ((delegate* unmanaged[MemberFunction]<ISlangBlob*, SlangUUID*, void**, int>)(lpVtbl[0]))((ISlangBlob*)Unsafe.AsPointer(ref this), uuid, outObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(1)]
    [return: NativeTypeName("uint32_t")]
    public uint addRef()
    {
        return ((delegate* unmanaged[MemberFunction]<ISlangBlob*, uint>)(lpVtbl[1]))((ISlangBlob*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(2)]
    [return: NativeTypeName("uint32_t")]
    public uint release()
    {
        return ((delegate* unmanaged[MemberFunction]<ISlangBlob*, uint>)(lpVtbl[2]))((ISlangBlob*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(3)]
    [return: NativeTypeName("const void *")]
    public void* getBufferPointer()
    {
        return ((delegate* unmanaged[MemberFunction]<ISlangBlob*, void*>)(lpVtbl[3]))((ISlangBlob*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(4)]
    [return: NativeTypeName("size_t")]
    public nuint getBufferSize()
    {
        return ((delegate* unmanaged[MemberFunction]<ISlangBlob*, nuint>)(lpVtbl[4]))((ISlangBlob*)Unsafe.AsPointer(ref this));
    }
}
