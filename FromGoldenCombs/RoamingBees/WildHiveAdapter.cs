using HarmonyLib;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace FromGoldenCombs.RoamingBees
{
    /// <summary>
    /// Lets the vanilla wild hive block entity (which this mod cannot edit) take part in the roaming
    /// bee system. Created and registered by a Harmony postfix on BlockEntityBeehive.Initialize
    /// (see FGCHarmonySystem); reads the vanilla population and activity fields by reflection.
    /// </summary>
    public class WildHiveAdapter : IRoamingBeeHive
    {
        static readonly AccessTools.FieldRef<BlockEntityBeehive, EnumHivePopSize> PopRef = TryField<EnumHivePopSize>("hivePopSize");
        static readonly AccessTools.FieldRef<BlockEntityBeehive, float> ActivityRef = TryField<float>("activityLevel");

        readonly BlockEntityBeehive be;

        public WildHiveAdapter(BlockEntityBeehive be)
        {
            this.be = be;
        }

        static AccessTools.FieldRef<BlockEntityBeehive, T> TryField<T>(string name)
        {
            try { return AccessTools.FieldRefAccess<BlockEntityBeehive, T>(name); }
            catch { return null; }
        }

        public BlockPos HivePos => be.Pos;

        public EnumRoamingHiveType RoamingHiveType => be.Block?.Variant?["type"] == "inlog" ? EnumRoamingHiveType.WildLog : EnumRoamingHiveType.Wild;

        /// <summary>A wild hive that exists is a living colony.</summary>
        public bool RoamingBeesActive => be.Api != null && be.Block is BlockBeehive;

        /// <summary>Vanilla counts flowers minus three per nearby hive; forests are often "Poor", so wild colonies are treated as at least Decent.</summary>
        public EnumHivePopSize RoamingPopSize
        {
            get
            {
                EnumHivePopSize pop = PopRef != null ? PopRef(be) : EnumHivePopSize.Decent;
                return pop < EnumHivePopSize.Decent ? EnumHivePopSize.Decent : pop;
            }
        }

        public float RoamingActivityLevel => ActivityRef != null ? ActivityRef(be) : 1f;

        public int RoamingRoomness => 0;

        public string RoamingFacing => null;
    }
}
