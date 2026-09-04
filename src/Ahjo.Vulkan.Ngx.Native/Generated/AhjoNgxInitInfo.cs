namespace Ahjo.Vulkan.Ngx.Native;

public unsafe partial struct AhjoNgxInitInfo
{
    [NativeTypeName("unsigned int")]
    public uint StructSize;

    public NVSDK_NGX_Application_Identifier_Type IdentifierType;

    [NativeTypeName("unsigned long long")]
    public ulong ApplicationId;

    [NativeTypeName("const char *")]
    public sbyte* ProjectId;

    public NVSDK_NGX_EngineType EngineType;

    [NativeTypeName("const char *")]
    public sbyte* EngineVersion;

    [NativeTypeName("const char *")]
    public sbyte* ApplicationDataPath;

    [NativeTypeName("const char *const *")]
    public sbyte** FeatureSearchPaths;

    [NativeTypeName("unsigned int")]
    public uint FeatureSearchPathCount;

    [NativeTypeName("NVSDK_NGX_AppLogCallback")]
    public delegate* unmanaged[Cdecl]<sbyte*, NVSDK_NGX_Logging_Level, NVSDK_NGX_Feature, void> LogCallback;

    public NVSDK_NGX_Logging_Level MinimumLoggingLevel;

    [NativeTypeName("unsigned char")]
    public byte DisableOtherLoggingSinks;
}
