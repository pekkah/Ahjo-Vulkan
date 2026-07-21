using System.Runtime.InteropServices;

namespace Ahjo.Vulkan.Ktx.Native;

public unsafe partial struct ktxStream
{
    [NativeTypeName("ktxStream_read")]
    public delegate* unmanaged[Cdecl]<ktxStream*, void*, nuint, ktx_error_code_e> read;

    [NativeTypeName("ktxStream_skip")]
    public delegate* unmanaged[Cdecl]<ktxStream*, nuint, ktx_error_code_e> skip;

    [NativeTypeName("ktxStream_write")]
    public delegate* unmanaged[Cdecl]<ktxStream*, void*, nuint, nuint, ktx_error_code_e> write;

    [NativeTypeName("ktxStream_getpos")]
    public delegate* unmanaged[Cdecl]<ktxStream*, int*, ktx_error_code_e> getpos;

    [NativeTypeName("ktxStream_setpos")]
    public delegate* unmanaged[Cdecl]<ktxStream*, int, ktx_error_code_e> setpos;

    [NativeTypeName("ktxStream_getsize")]
    public delegate* unmanaged[Cdecl]<ktxStream*, nuint*, ktx_error_code_e> getsize;

    [NativeTypeName("ktxStream_destruct")]
    public delegate* unmanaged[Cdecl]<ktxStream*, void> destruct;

    [NativeTypeName("enum streamType")]
    public streamType type;

    [NativeTypeName("__AnonymousRecord_ktx_L908_C5")]
    public _data_e__Union data;

    [NativeTypeName("ktx_off_t")]
    public int readpos;

    [NativeTypeName("ktx_bool_t")]
    public bool closeOnDestruct;

    [StructLayout(LayoutKind.Explicit)]
    public unsafe partial struct _data_e__Union
    {
        [FieldOffset(0)]
        [NativeTypeName("FILE *")]
        public void* file;

        [FieldOffset(0)]
        public ktxMem* mem;

        [FieldOffset(0)]
        [NativeTypeName("__AnonymousRecord_ktx_L911_C9")]
        public _custom_ptr_e__Struct custom_ptr;

        public unsafe partial struct _custom_ptr_e__Struct
        {
            public void* address;

            public void* allocatorAddress;

            [NativeTypeName("ktx_size_t")]
            public nuint size;
        }
    }
}
