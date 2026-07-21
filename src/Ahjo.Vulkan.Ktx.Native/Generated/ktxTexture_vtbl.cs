namespace Ahjo.Vulkan.Ktx.Native;

public unsafe partial struct ktxTexture_vtbl
{
    [NativeTypeName("PFNKTEXDESTROY")]
    public delegate* unmanaged[Cdecl]<ktxTexture*, void> Destroy;

    [NativeTypeName("PFNKTEXGETIMAGEOFFSET")]
    public delegate* unmanaged[Cdecl]<ktxTexture*, uint, uint, uint, nuint*, ktx_error_code_e> GetImageOffset;

    [NativeTypeName("PFNKTEXGETDATASIZEUNCOMPRESSED")]
    public delegate* unmanaged[Cdecl]<ktxTexture*, nuint> GetDataSizeUncompressed;

    [NativeTypeName("PFNKTEXGETIMAGESIZE")]
    public delegate* unmanaged[Cdecl]<ktxTexture*, uint, nuint> GetImageSize;

    [NativeTypeName("PFNKTEXGETLEVELSIZE")]
    public delegate* unmanaged[Cdecl]<ktxTexture*, uint, nuint> GetLevelSize;

    [NativeTypeName("PFNKTEXITERATELEVELS")]
    public delegate* unmanaged[Cdecl]<ktxTexture*, delegate* unmanaged[Cdecl]<int, int, int, int, int, ulong, void*, void*, ktx_error_code_e>, void*, ktx_error_code_e> IterateLevels;

    [NativeTypeName("PFNKTEXITERATELOADLEVELFACES")]
    public delegate* unmanaged[Cdecl]<ktxTexture*, delegate* unmanaged[Cdecl]<int, int, int, int, int, ulong, void*, void*, ktx_error_code_e>, void*, ktx_error_code_e> IterateLoadLevelFaces;

    [NativeTypeName("PFNKTEXNEEDSTRANSCODING")]
    public delegate* unmanaged[Cdecl]<ktxTexture*, bool> NeedsTranscoding;

    [NativeTypeName("PFNKTEXLOADIMAGEDATA")]
    public delegate* unmanaged[Cdecl]<ktxTexture*, byte*, nuint, ktx_error_code_e> LoadImageData;

    [NativeTypeName("PFNKTEXSETIMAGEFROMMEMORY")]
    public delegate* unmanaged[Cdecl]<ktxTexture*, uint, uint, uint, byte*, nuint, ktx_error_code_e> SetImageFromMemory;

    [NativeTypeName("PFNKTEXSETIMAGEFROMSTDIOSTREAM")]
    public delegate* unmanaged[Cdecl]<ktxTexture*, uint, uint, uint, void*, nuint, ktx_error_code_e> SetImageFromStdioStream;

    [NativeTypeName("PFNKTEXWRITETOSTDIOSTREAM")]
    public delegate* unmanaged[Cdecl]<ktxTexture*, void*, ktx_error_code_e> WriteToStdioStream;

    [NativeTypeName("PFNKTEXWRITETONAMEDFILE")]
    public delegate* unmanaged[Cdecl]<ktxTexture*, sbyte*, ktx_error_code_e> WriteToNamedFile;

    [NativeTypeName("PFNKTEXWRITETOMEMORY")]
    public delegate* unmanaged[Cdecl]<ktxTexture*, byte**, nuint*, ktx_error_code_e> WriteToMemory;

    [NativeTypeName("PFNKTEXWRITETOSTREAM")]
    public delegate* unmanaged[Cdecl]<ktxTexture*, ktxStream*, ktx_error_code_e> WriteToStream;
}
