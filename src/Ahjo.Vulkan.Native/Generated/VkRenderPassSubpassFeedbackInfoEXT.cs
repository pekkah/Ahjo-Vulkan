using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct VkRenderPassSubpassFeedbackInfoEXT
{
    public VkSubpassMergeStatusEXT subpassMergeStatus;

    [NativeTypeName("char[256]")]
    public _description_e__FixedBuffer description;

    [NativeTypeName("uint32_t")]
    public uint postMergeIndex;

    [InlineArray(256)]
    public partial struct _description_e__FixedBuffer
    {
        public sbyte e0;
    }
}
