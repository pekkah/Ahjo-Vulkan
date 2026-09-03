namespace Ahjo.Vulkan.Ngx.Native;

[NativeTypeName("unsigned int")]
public enum NVSDK_NGX_ToneMapperType : uint
{
    NVSDK_NGX_TONEMAPPER_STRING = 0,
    NVSDK_NGX_TONEMAPPER_REINHARD,
    NVSDK_NGX_TONEMAPPER_ONEOVERLUMA,
    NVSDK_NGX_TONEMAPPER_ACES,
    NVSDK_NGX_TONEMAPPERTYPE_NUM,
}
