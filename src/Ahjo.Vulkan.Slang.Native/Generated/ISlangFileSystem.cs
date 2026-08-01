using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("struct ISlangFileSystem : ISlangCastable")]
[NativeInheritance("ISlangCastable")]
public unsafe partial struct ISlangFileSystem
{
    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(0)]
    [return: NativeTypeName("SlangResult")]
    public int queryInterface([NativeTypeName("const SlangUUID &")] SlangUUID* uuid, void** outObject)
    {
        return ((delegate* unmanaged[MemberFunction]<ISlangFileSystem*, SlangUUID*, void**, int>)(lpVtbl[0]))((ISlangFileSystem*)Unsafe.AsPointer(ref this), uuid, outObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(1)]
    [return: NativeTypeName("uint32_t")]
    public uint addRef()
    {
        return ((delegate* unmanaged[MemberFunction]<ISlangFileSystem*, uint>)(lpVtbl[1]))((ISlangFileSystem*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(2)]
    [return: NativeTypeName("uint32_t")]
    public uint release()
    {
        return ((delegate* unmanaged[MemberFunction]<ISlangFileSystem*, uint>)(lpVtbl[2]))((ISlangFileSystem*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(3)]
    public void* castAs([NativeTypeName("const SlangUUID &")] SlangUUID* guid)
    {
        return ((delegate* unmanaged[MemberFunction]<ISlangFileSystem*, SlangUUID*, void*>)(lpVtbl[3]))((ISlangFileSystem*)Unsafe.AsPointer(ref this), guid);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(4)]
    [return: NativeTypeName("SlangResult")]
    public int loadFile([NativeTypeName("const char *")] sbyte* path, ISlangBlob** outBlob)
    {
        return ((delegate* unmanaged[MemberFunction]<ISlangFileSystem*, sbyte*, ISlangBlob**, int>)(lpVtbl[4]))((ISlangFileSystem*)Unsafe.AsPointer(ref this), path, outBlob);
    }
}
