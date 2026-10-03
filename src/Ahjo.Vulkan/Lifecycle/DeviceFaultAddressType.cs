namespace Ahjo.Vulkan;

/// <summary>
/// What a <see cref="DeviceFaultAddressInfo"/> describes, mirroring
/// <c>VkDeviceFaultAddressTypeKHR</c> (whose <c>_EXT</c> spellings are
/// aliases of the same values).
/// </summary>
/// <remarks>The <c>Read/Write/ExecuteInvalid</c> members describe a memory
/// access; the <c>InstructionPointer*</c> members describe an instruction
/// pointer. Unknown future values pass through numerically.</remarks>
public enum DeviceFaultAddressType
{
    /// <summary>No address is available.</summary>
    None = 0,

    /// <summary>An invalid read access.</summary>
    ReadInvalid = 1,

    /// <summary>An invalid write access.</summary>
    WriteInvalid = 2,

    /// <summary>An attempt to execute non-executable memory.</summary>
    ExecuteInvalid = 3,

    /// <summary>An instruction pointer value at the time of the fault, whose
    /// relation to the fault is unknown.</summary>
    InstructionPointerUnknown = 4,

    /// <summary>An instruction pointer associated with an invalid-instruction
    /// fault.</summary>
    InstructionPointerInvalid = 5,

    /// <summary>An instruction pointer associated with the fault.</summary>
    InstructionPointerFault = 6,
}
