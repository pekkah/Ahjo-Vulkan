namespace Ahjo.Vulkan;

/// <summary>
/// Strongly-typed shadow of <c>VkPipelineStageFlags2</c> (synchronization2,
/// 1.3 core). 64-bit because sync2 expands the flag space past
/// <c>uint</c>; covers the bits the wrapper exercises today plus the
/// common scopes (transfer, color attachment, compute / fragment shader,
/// host).
/// </summary>
[Flags]
public enum Stage : ulong
{
    None                  = 0,
    TopOfPipe             = 0x00000001,
    DrawIndirect          = 0x00000002,
    VertexInput           = 0x00000004,
    VertexShader          = 0x00000008,
    TessellationControl   = 0x00000010,
    TessellationEval      = 0x00000020,
    GeometryShader        = 0x00000040,
    FragmentShader        = 0x00000080,
    EarlyFragmentTests    = 0x00000100,
    LateFragmentTests     = 0x00000200,
    ColorAttachmentOutput = 0x00000400,
    ComputeShader         = 0x00000800,
    AllTransfer           = 0x00001000,
    BottomOfPipe          = 0x00002000,
    Host                  = 0x00004000,
    AllGraphics           = 0x00008000,
    AllCommands           = 0x00010000,
    Copy                  = 0x100000000,
    Resolve               = 0x200000000,
    Blit                  = 0x400000000,
    Clear                 = 0x800000000,
    IndexInput            = 0x1000000000,
    VertexAttributeInput  = 0x2000000000,
}
