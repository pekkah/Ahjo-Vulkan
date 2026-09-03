using System.Runtime.InteropServices;

namespace Ahjo.Vulkan.Ngx.Native;

public partial struct NVSDK_NGX_Resource_VK
{
    [NativeTypeName("__AnonymousRecord_nvsdk_ngx_defs_vk_L94_C5")]
    public _Resource_e__Union Resource;

    public NVSDK_NGX_Resource_VK_Type Type;

    [NativeTypeName("_Bool")]
    public bool ReadWrite;

    [StructLayout(LayoutKind.Explicit)]
    public partial struct _Resource_e__Union
    {
        [FieldOffset(0)]
        public NVSDK_NGX_ImageViewInfo_VK ImageViewInfo;

        [FieldOffset(0)]
        public NVSDK_NGX_BufferInfo_VK BufferInfo;
    }
}
