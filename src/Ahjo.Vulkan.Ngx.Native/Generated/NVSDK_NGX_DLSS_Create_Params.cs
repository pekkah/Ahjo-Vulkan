namespace Ahjo.Vulkan.Ngx.Native;

public partial struct NVSDK_NGX_DLSS_Create_Params
{
    public NVSDK_NGX_Feature_Create_Params Feature;

    public int InFeatureCreateFlags;

    [NativeTypeName("_Bool")]
    public bool InEnableOutputSubrects;
}
