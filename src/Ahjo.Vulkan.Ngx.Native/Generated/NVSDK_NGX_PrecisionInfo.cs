namespace Ahjo.Vulkan.Ngx.Native;

public partial struct NVSDK_NGX_PrecisionInfo
{
    [NativeTypeName("unsigned int")]
    public uint IsLowPrecision;

    public float Bias;

    public float Scale;
}
