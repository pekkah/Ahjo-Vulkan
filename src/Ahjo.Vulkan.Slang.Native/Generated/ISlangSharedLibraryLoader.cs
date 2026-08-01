using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("struct ISlangSharedLibraryLoader : ISlangUnknown")]
[NativeInheritance("ISlangUnknown")]
public unsafe partial struct ISlangSharedLibraryLoader
{
    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(0)]
    [return: NativeTypeName("SlangResult")]
    public int queryInterface([NativeTypeName("const SlangUUID &")] SlangUUID* uuid, void** outObject)
    {
        return ((delegate* unmanaged[MemberFunction]<ISlangSharedLibraryLoader*, SlangUUID*, void**, int>)(lpVtbl[0]))((ISlangSharedLibraryLoader*)Unsafe.AsPointer(ref this), uuid, outObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(1)]
    [return: NativeTypeName("uint32_t")]
    public uint addRef()
    {
        return ((delegate* unmanaged[MemberFunction]<ISlangSharedLibraryLoader*, uint>)(lpVtbl[1]))((ISlangSharedLibraryLoader*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(2)]
    [return: NativeTypeName("uint32_t")]
    public uint release()
    {
        return ((delegate* unmanaged[MemberFunction]<ISlangSharedLibraryLoader*, uint>)(lpVtbl[2]))((ISlangSharedLibraryLoader*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(3)]
    [return: NativeTypeName("SlangResult")]
    public int loadSharedLibrary([NativeTypeName("const char *")] sbyte* path, ISlangSharedLibrary** sharedLibraryOut)
    {
        return ((delegate* unmanaged[MemberFunction]<ISlangSharedLibraryLoader*, sbyte*, ISlangSharedLibrary**, int>)(lpVtbl[3]))((ISlangSharedLibraryLoader*)Unsafe.AsPointer(ref this), path, sharedLibraryOut);
    }
}
