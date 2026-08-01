using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("struct ISlangProfiler : ISlangUnknown")]
[NativeInheritance("ISlangUnknown")]
public unsafe partial struct ISlangProfiler
{
    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(0)]
    [return: NativeTypeName("SlangResult")]
    public int queryInterface([NativeTypeName("const SlangUUID &")] SlangUUID* uuid, void** outObject)
    {
        return ((delegate* unmanaged[MemberFunction]<ISlangProfiler*, SlangUUID*, void**, int>)(lpVtbl[0]))((ISlangProfiler*)Unsafe.AsPointer(ref this), uuid, outObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(1)]
    [return: NativeTypeName("uint32_t")]
    public uint addRef()
    {
        return ((delegate* unmanaged[MemberFunction]<ISlangProfiler*, uint>)(lpVtbl[1]))((ISlangProfiler*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(2)]
    [return: NativeTypeName("uint32_t")]
    public uint release()
    {
        return ((delegate* unmanaged[MemberFunction]<ISlangProfiler*, uint>)(lpVtbl[2]))((ISlangProfiler*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(3)]
    [return: NativeTypeName("size_t")]
    public nuint getEntryCount()
    {
        return ((delegate* unmanaged[MemberFunction]<ISlangProfiler*, nuint>)(lpVtbl[3]))((ISlangProfiler*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(4)]
    [return: NativeTypeName("const char *")]
    public sbyte* getEntryName([NativeTypeName("uint32_t")] uint index)
    {
        return ((delegate* unmanaged[MemberFunction]<ISlangProfiler*, uint, sbyte*>)(lpVtbl[4]))((ISlangProfiler*)Unsafe.AsPointer(ref this), index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(5)]
    [return: NativeTypeName("long")]
    public nint getEntryTimeMS([NativeTypeName("uint32_t")] uint index)
    {
        return ((delegate* unmanaged[MemberFunction]<ISlangProfiler*, uint, nint>)(lpVtbl[5]))((ISlangProfiler*)Unsafe.AsPointer(ref this), index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(6)]
    [return: NativeTypeName("uint32_t")]
    public uint getEntryInvocationTimes([NativeTypeName("uint32_t")] uint index)
    {
        return ((delegate* unmanaged[MemberFunction]<ISlangProfiler*, uint, uint>)(lpVtbl[6]))((ISlangProfiler*)Unsafe.AsPointer(ref this), index);
    }
}
