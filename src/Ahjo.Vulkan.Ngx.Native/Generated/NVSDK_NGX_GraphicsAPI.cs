namespace Ahjo.Vulkan.Ngx.Native;

[NativeTypeName("unsigned int")]
public enum NVSDK_NGX_GraphicsAPI : uint
{
    NVSDK_NGX_GRAPHICS_API_CUDA = 0,
    NVSDK_NGX_GRAPHICS_API_D3D11 = 1,
    NVSDK_NGX_GRAPHICS_API_D3D12 = 2,
    NVSDK_NGX_GRAPHICS_API_VULKAN = 3,
    NVSDK_NGX_GRAPHICS_API_COUNT,
}
