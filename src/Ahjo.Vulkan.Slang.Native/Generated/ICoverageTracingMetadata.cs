using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("struct ICoverageTracingMetadata : ISlangCastable")]
[NativeInheritance("ISlangCastable")]
public unsafe partial struct ICoverageTracingMetadata
{
    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(0)]
    [return: NativeTypeName("SlangResult")]
    public int queryInterface([NativeTypeName("const SlangUUID &")] SlangUUID* uuid, void** outObject)
    {
        return ((delegate* unmanaged[MemberFunction]<ICoverageTracingMetadata*, SlangUUID*, void**, int>)(lpVtbl[0]))((ICoverageTracingMetadata*)Unsafe.AsPointer(ref this), uuid, outObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(1)]
    [return: NativeTypeName("uint32_t")]
    public uint addRef()
    {
        return ((delegate* unmanaged[MemberFunction]<ICoverageTracingMetadata*, uint>)(lpVtbl[1]))((ICoverageTracingMetadata*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(2)]
    [return: NativeTypeName("uint32_t")]
    public uint release()
    {
        return ((delegate* unmanaged[MemberFunction]<ICoverageTracingMetadata*, uint>)(lpVtbl[2]))((ICoverageTracingMetadata*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(3)]
    public void* castAs([NativeTypeName("const SlangUUID &")] SlangUUID* guid)
    {
        return ((delegate* unmanaged[MemberFunction]<ICoverageTracingMetadata*, SlangUUID*, void*>)(lpVtbl[3]))((ICoverageTracingMetadata*)Unsafe.AsPointer(ref this), guid);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(4)]
    [return: NativeTypeName("uint32_t")]
    public uint getCounterCount()
    {
        return ((delegate* unmanaged[MemberFunction]<ICoverageTracingMetadata*, uint>)(lpVtbl[4]))((ICoverageTracingMetadata*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(5)]
    [return: NativeTypeName("SlangResult")]
    public int getEntryInfo([NativeTypeName("uint32_t")] uint index, [NativeTypeName("slang::CoverageEntryInfo *")] CoverageEntryInfo* outInfo)
    {
        return ((delegate* unmanaged[MemberFunction]<ICoverageTracingMetadata*, uint, CoverageEntryInfo*, int>)(lpVtbl[5]))((ICoverageTracingMetadata*)Unsafe.AsPointer(ref this), index, outInfo);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(6)]
    [return: NativeTypeName("SlangResult")]
    public int getBufferInfo([NativeTypeName("slang::CoverageBufferInfo *")] CoverageBufferInfo* outInfo)
    {
        return ((delegate* unmanaged[MemberFunction]<ICoverageTracingMetadata*, CoverageBufferInfo*, int>)(lpVtbl[6]))((ICoverageTracingMetadata*)Unsafe.AsPointer(ref this), outInfo);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(7)]
    [return: NativeTypeName("uint32_t")]
    public uint getEntryCount()
    {
        return ((delegate* unmanaged[MemberFunction]<ICoverageTracingMetadata*, uint>)(lpVtbl[7]))((ICoverageTracingMetadata*)Unsafe.AsPointer(ref this));
    }
}
