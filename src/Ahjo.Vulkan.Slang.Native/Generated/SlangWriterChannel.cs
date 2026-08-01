namespace Ahjo.Vulkan.Slang.Native;

[NativeTypeName("SlangWriterChannelIntegral")]
public enum SlangWriterChannel : uint
{
    SLANG_WRITER_CHANNEL_DIAGNOSTIC = 0,
    SLANG_WRITER_CHANNEL_STD_OUTPUT = 1,
    SLANG_WRITER_CHANNEL_STD_ERROR = 2,
    SLANG_WRITER_CHANNEL_COUNT_OF,
}
