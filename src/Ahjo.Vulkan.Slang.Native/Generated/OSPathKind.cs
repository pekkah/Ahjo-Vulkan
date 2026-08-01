namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("uint8_t")]
public enum OSPathKind : byte
{
    None = 0,
    Direct = 1,
    OperatingSystem = 2,
}
