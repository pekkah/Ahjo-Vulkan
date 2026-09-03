using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Ngx.Native;

public partial struct NVSDK_NGX_FeatureRequirement
{
    public NVSDK_NGX_Feature_Support_Result FeatureSupported;

    [NativeTypeName("unsigned int")]
    public uint MinHWArchitecture;

    [NativeTypeName("char[255]")]
    public _MinOSVersion_e__FixedBuffer MinOSVersion;

    [InlineArray(255)]
    public partial struct _MinOSVersion_e__FixedBuffer
    {
        public sbyte e0;
    }
}
