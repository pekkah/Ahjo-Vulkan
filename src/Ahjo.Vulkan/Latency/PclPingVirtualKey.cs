namespace Ahjo.Vulkan;

/// <summary>
/// The virtual key <see cref="PclStats"/> may post as its ping, selected with
/// <see cref="PclStatsOptions.PingVirtualKey"/>. See
/// <see cref="PclStatsOptions"/> for why <see cref="None"/> is
/// recommended.
/// </summary>
public enum PclPingVirtualKey : ushort
{
    /// <summary>No virtual key: use the registered message.</summary>
    None = 0,

    /// <summary><c>VK_F13</c>.</summary>
    F13 = 0x7C,

    /// <summary><c>VK_F14</c>.</summary>
    F14 = 0x7D,

    /// <summary><c>VK_F15</c>.</summary>
    F15 = 0x7E,
}
