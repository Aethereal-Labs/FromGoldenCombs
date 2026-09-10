using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace FromGoldenCombs.RoamingBees
{
    public enum EnumRoamingHiveType
    {
        Skep,
        Ceramic,
        Langstroth,
        /// <summary>Vanilla wild hive hanging under a block (wildbeehive-medium / -large).</summary>
        Wild,
        /// <summary>Vanilla wild hive inside a log (wildbeehive-inlog-*).</summary>
        WildLog
    }

    /// <summary>
    /// Implemented by the hive block entities that can send out roaming bees.
    /// The server-side bee manager polls these properties; the block entity only has to
    /// register itself on the server (Initialize) and unregister when removed or unloaded.
    /// </summary>
    public interface IRoamingBeeHive
    {
        /// <summary>Position of the block that owns the bees (for Langstroth stacks: the bottom stack).</summary>
        BlockPos HivePos { get; }

        EnumRoamingHiveType RoamingHiveType { get; }

        /// <summary>True while this block is an active, populated hive that is allowed to work (room rules included).</summary>
        bool RoamingBeesActive { get; }

        EnumHivePopSize RoamingPopSize { get; }

        /// <summary>FGC activity level, 0..1, derived from the current temperature.</summary>
        float RoamingActivityLevel { get; }

        /// <summary>1 when the hive sits in a closed greenhouse-like room (worth +5 degrees), else 0.</summary>
        int RoamingRoomness { get; }

        /// <summary>Facing variant ("north", "east", "south", "west") or null when the block has no facing.</summary>
        string RoamingFacing { get; }
    }
}
