namespace Ahjo.Vulkan.Ngx.Native;

public partial struct NVSDK_NGX_Feature_Create_Params
{
    [NativeTypeName("unsigned int")]
    public uint InWidth;

    [NativeTypeName("unsigned int")]
    public uint InHeight;

    [NativeTypeName("unsigned int")]
    public uint InTargetWidth;

    [NativeTypeName("unsigned int")]
    public uint InTargetHeight;

    public NVSDK_NGX_PerfQuality_Value InPerfQualityValue;
}
