using System.Runtime.InteropServices;

namespace Ahjo.Vulkan.Native;

[StructLayout(LayoutKind.Explicit)]
public unsafe partial struct VkIndirectCommandsTokenDataEXT
{
    [FieldOffset(0)]
    [NativeTypeName("const VkIndirectCommandsPushConstantTokenEXT *")]
    public VkIndirectCommandsPushConstantTokenEXT* pPushConstant;

    [FieldOffset(0)]
    [NativeTypeName("const VkIndirectCommandsVertexBufferTokenEXT *")]
    public VkIndirectCommandsVertexBufferTokenEXT* pVertexBuffer;

    [FieldOffset(0)]
    [NativeTypeName("const VkIndirectCommandsIndexBufferTokenEXT *")]
    public VkIndirectCommandsIndexBufferTokenEXT* pIndexBuffer;

    [FieldOffset(0)]
    [NativeTypeName("const VkIndirectCommandsExecutionSetTokenEXT *")]
    public VkIndirectCommandsExecutionSetTokenEXT* pExecutionSet;
}
