namespace Ahjo.Vulkan.Ngx.Native;

[NativeTypeName("unsigned int")]
public enum NVSDK_NGX_EngineType : uint
{
    NVSDK_NGX_ENGINE_TYPE_CUSTOM = 0,
    NVSDK_NGX_ENGINE_TYPE_UNREAL,
    NVSDK_NGX_ENGINE_TYPE_UNITY,
    NVSDK_NGX_ENGINE_TYPE_OMNIVERSE,
    NVSDK_NGX_ENGINE_COUNT,
}
