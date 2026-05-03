using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineColorBlendStateCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipelineColorBlendStateCreateFlags")]
    public uint flags;

    [NativeTypeName("VkBool32")]
    public uint logicOpEnable;

    public VkLogicOp logicOp;

    [NativeTypeName("uint32_t")]
    public uint attachmentCount;

    [NativeTypeName("const VkPipelineColorBlendAttachmentState *")]
    public VkPipelineColorBlendAttachmentState* pAttachments;

    [NativeTypeName("float[4]")]
    public _blendConstants_e__FixedBuffer blendConstants;

    [InlineArray(4)]
    public partial struct _blendConstants_e__FixedBuffer
    {
        public float e0;
    }
}
