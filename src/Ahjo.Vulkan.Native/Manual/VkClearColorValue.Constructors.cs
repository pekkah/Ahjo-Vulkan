namespace Ahjo.Vulkan.Native;

/// <summary>
/// Hand-authored convenience constructors for the three union lanes of
/// <see cref="VkClearColorValue"/>. The UINT and SINT overloads are
/// load-bearing for integer-format render targets (e.g. R16G16B16A16_UINT
/// G-buffers) — the FLOAT ctor would bit-reinterpret to garbage on those.
/// Each ctor chains <c>: this()</c> so the overlapping union memory is
/// zero-initialised before the selected lane is written.
/// </summary>
public partial struct VkClearColorValue
{
    public VkClearColorValue(float r, float g, float b, float a) : this()
    {
        float32[0] = r;
        float32[1] = g;
        float32[2] = b;
        float32[3] = a;
    }

    public VkClearColorValue(uint r, uint g, uint b, uint a) : this()
    {
        uint32[0] = r;
        uint32[1] = g;
        uint32[2] = b;
        uint32[3] = a;
    }

    public VkClearColorValue(int r, int g, int b, int a) : this()
    {
        int32[0] = r;
        int32[1] = g;
        int32[2] = b;
        int32[3] = a;
    }
}
