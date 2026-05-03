using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct StdVideoEncodeH265ReferenceListsInfo
{
    public StdVideoEncodeH265ReferenceListsInfoFlags flags;

    [NativeTypeName("uint8_t")]
    public byte num_ref_idx_l0_active_minus1;

    [NativeTypeName("uint8_t")]
    public byte num_ref_idx_l1_active_minus1;

    [NativeTypeName("uint8_t[15]")]
    public _RefPicList0_e__FixedBuffer RefPicList0;

    [NativeTypeName("uint8_t[15]")]
    public _RefPicList1_e__FixedBuffer RefPicList1;

    [NativeTypeName("uint8_t[15]")]
    public _list_entry_l0_e__FixedBuffer list_entry_l0;

    [NativeTypeName("uint8_t[15]")]
    public _list_entry_l1_e__FixedBuffer list_entry_l1;

    [InlineArray(15)]
    public partial struct _RefPicList0_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(15)]
    public partial struct _RefPicList1_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(15)]
    public partial struct _list_entry_l0_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(15)]
    public partial struct _list_entry_l1_e__FixedBuffer
    {
        public byte e0;
    }
}
