using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("struct IMetadata : ISlangCastable")]
[NativeInheritance("ISlangCastable")]
public unsafe partial struct IMetadata
{
    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(0)]
    [return: NativeTypeName("SlangResult")]
    public int queryInterface([NativeTypeName("const SlangUUID &")] SlangUUID* uuid, void** outObject)
    {
        return ((delegate* unmanaged[MemberFunction]<IMetadata*, SlangUUID*, void**, int>)(lpVtbl[0]))((IMetadata*)Unsafe.AsPointer(ref this), uuid, outObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(1)]
    [return: NativeTypeName("uint32_t")]
    public uint addRef()
    {
        return ((delegate* unmanaged[MemberFunction]<IMetadata*, uint>)(lpVtbl[1]))((IMetadata*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(2)]
    [return: NativeTypeName("uint32_t")]
    public uint release()
    {
        return ((delegate* unmanaged[MemberFunction]<IMetadata*, uint>)(lpVtbl[2]))((IMetadata*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(3)]
    public void* castAs([NativeTypeName("const SlangUUID &")] SlangUUID* guid)
    {
        return ((delegate* unmanaged[MemberFunction]<IMetadata*, SlangUUID*, void*>)(lpVtbl[3]))((IMetadata*)Unsafe.AsPointer(ref this), guid);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(4)]
    [return: NativeTypeName("SlangResult")]
    public int isParameterLocationUsed(SlangParameterCategory category, [NativeTypeName("SlangUInt")] ulong spaceIndex, [NativeTypeName("SlangUInt")] ulong registerIndex, [NativeTypeName("bool &")] bool* outUsed)
    {
        return ((delegate* unmanaged[MemberFunction]<IMetadata*, SlangParameterCategory, ulong, ulong, bool*, int>)(lpVtbl[4]))((IMetadata*)Unsafe.AsPointer(ref this), category, spaceIndex, registerIndex, outUsed);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(5)]
    [return: NativeTypeName("const char *")]
    public sbyte* getDebugBuildIdentifier()
    {
        return ((delegate* unmanaged[MemberFunction]<IMetadata*, sbyte*>)(lpVtbl[5]))((IMetadata*)Unsafe.AsPointer(ref this));
    }
}
