namespace Ahjo.Vulkan.Native;

/// <summary>
/// Hand-authored convenience constructor for <see cref="VkExtent2D"/>.
/// ClangSharp doesn't emit constructors for C structs — only the field
/// layout in <c>Generated/VkExtent2D.cs</c>. Adding the positional ctor
/// here keeps call sites tight without fighting the regen pipeline.
/// </summary>
public partial struct VkExtent2D
{
    public VkExtent2D(uint width, uint height)
    {
        this.width  = width;
        this.height = height;
    }
}
