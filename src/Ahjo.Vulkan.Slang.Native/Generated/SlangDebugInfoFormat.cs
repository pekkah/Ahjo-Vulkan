namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("SlangDebugInfoFormatIntegral")]
public enum SlangDebugInfoFormat : uint
{
    SLANG_DEBUG_INFO_FORMAT_DEFAULT = 0,
    SLANG_DEBUG_INFO_FORMAT_C7 = 1,
    SLANG_DEBUG_INFO_FORMAT_PDB = 2,
    SLANG_DEBUG_INFO_FORMAT_STABS = 3,
    SLANG_DEBUG_INFO_FORMAT_COFF = 4,
    SLANG_DEBUG_INFO_FORMAT_DWARF = 5,
    SLANG_DEBUG_INFO_FORMAT_COUNT_OF,
}
