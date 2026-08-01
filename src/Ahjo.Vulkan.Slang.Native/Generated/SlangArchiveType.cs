namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("SlangArchiveTypeIntegral")]
public enum SlangArchiveType
{
    SLANG_ARCHIVE_TYPE_UNDEFINED = 0,
    SLANG_ARCHIVE_TYPE_ZIP = 1,
    SLANG_ARCHIVE_TYPE_RIFF = 2,
    SLANG_ARCHIVE_TYPE_RIFF_DEFLATE = 3,
    SLANG_ARCHIVE_TYPE_RIFF_LZ4 = 4,
    SLANG_ARCHIVE_TYPE_COUNT_OF,
}
