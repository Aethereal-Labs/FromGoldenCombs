using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace FromGoldenCombs.Util.Config
{
    [ProtoContract()]
    class FGCServerConfig
    {
        [ProtoMember(1)]
        public double ConfigVersion = 1.7;
        [ProtoMember(2)]
        public bool retainConfigOnVersionChange = false;
        [ProtoMember(3)]
        public float SkepDaysToHarvestIn30DayMonths = 7;
        [ProtoMember(4)]
        public float ClayPotDaysToHarvestIn30DayMonths = 7;
        [ProtoMember(5)]
        public float LangstrothDaysToHarvestIn30DayMonths = 3.5f;
        [ProtoMember(6)]
        public int MaxStackSize = 6;
        [ProtoMember(7)]
        public int baseframedurability = 32;
        [ProtoMember(8)]
        public int minFramePerCycle = 2;
        [ProtoMember(9)]
        public int maxFramePerCycle = 3;
        [ProtoMember(10)]
        public bool showcombpoptime = true;
        [ProtoMember(11)]
        public int CeramicPotMinYield { get; set; } = 2;
        [ProtoMember(12)]
        public int CeramicPotMaxYield { get; set; } = 5;
        [ProtoMember(13)]
        public int FrameMinYield { get; set; } = 2;
        [ProtoMember(14)]
        public int FrameMaxYield { get; set; } = 5;
        [ProtoMember(15)]
        public int SkepMinYield { get; set; } = 1;
        [ProtoMember(16)]
        public int SkepMaxYield { get; set; } = 3;
        [ProtoMember(17)]
        public float SkepHiveMinTemp { get; set; } = 10f;
        [ProtoMember(18)]
        public float SkepHiveMaxTemp { get; set; } = 37f;
        [ProtoMember(19)]
        public float CeramicHiveMinTemp { get; set; } = 10f;
        [ProtoMember(20)]
        public float CeramicHiveMaxTemp { get; set; } = 37f;
        [ProtoMember(21)]
        public float LangstrothHiveMinTemp { get; set; } = 10f;
        [ProtoMember(22)]
        public float LangstrothHiveMaxTemp { get; set; } = 37f;
        [ProtoMember(23)]
        public double skepBaseChargesPerDay = 2;
        [ProtoMember(24)]
        public int skepMaxCropCharges = 60;
        [ProtoMember(25)]
        public int skepCropRange = 5;
        [ProtoMember(26)]
        public double ceramicBaseChargesPerDay = 4;
        [ProtoMember(27)]
        public int ceramicMaxCropCharges = 100;
        [ProtoMember(28)]
        public int ceramicCropRange = 7;
        [ProtoMember(29)]
        public double langstrothBaseChargesPerDay = 5;
        [ProtoMember(30)]
        public int langstrothMaxCropCharges = 150;
        [ProtoMember(31)]
        public int langstrothCropRange = 8;
        [ProtoMember(32)]
        public bool showExtraBeehiveInfo = true;
        [ProtoMember(33)]
        public bool backpackSlotOnly = true;
        [ProtoMember(34)]
        public float cropBoostPercentage = 0.20f;
        [ProtoMember(35)]
        public int minFlowersPerHive = 3;
        [ProtoMember(36)]
        public bool canPlaceCeramicOnFence = false;
        [ProtoMember(37)]
        public bool canPlaceLangstrothOnFence = false;
        [ProtoMember(38)]
        public float langstrothCropBoostPercentage = 0.30f;
        [ProtoMember(39)]
        public float ceramicCropBoostPercentage = 0.20f;
        [ProtoMember(40)]
        public float skepCropBoostPercentage = 0.10f;
        [ProtoMember(41)]
        public bool enabledInRoomOnly = false;
        [ProtoMember(42)]
        public bool enabledGreenHouse = true;

        // Roaming bees. Bees are purely visual; none of these settings change honey or pollination.
        [ProtoMember(43)]
        public bool roamingBeesEnabled = true;
        [ProtoMember(44)]
        public int roamingBeesPerSkep = 20;
        [ProtoMember(45)]
        public int roamingBeesPerCeramicHive = 30;
        [ProtoMember(46)]
        public int roamingBeesPerLangstrothHive = 40;
        [ProtoMember(47)]
        public int roamingBeesGlobalCap = 400;
        [ProtoMember(48)]
        public int roamingBeesRadius = 10;
        [ProtoMember(49)]
        public int roamingBeesMinFlowerVisits = 2;
        [ProtoMember(50)]
        public int roamingBeesMaxFlowerVisits = 6;
        [ProtoMember(51)]
        public float roamingBeesRainfallStop = 0.1f;
        [ProtoMember(52)]
        public float roamingBeesMinSunAltitudeDeg = -3f;
        [ProtoMember(53)]
        public float roamingBeesFullSunAltitudeDeg = 8f;
        [ProtoMember(54)]
        public float roamingBeesMinTemperature = 8f;
        [ProtoMember(55)]
        public float roamingBeesFullTemperature = 16f;
        [ProtoMember(56)]
        public int roamingBeesSpawnCooldownMs = 600;
        [ProtoMember(57)]
        public bool roamingBeesVisitCrops = true;
        [ProtoMember(58)]
        public bool roamingBeesScoutWithoutFlowers = true;
        /// <summary>Multiplies every hive's bee target. 2.0 doubles all bees, 0.5 halves them.</summary>
        [ProtoMember(59)]
        public float roamingBeesDensity = 1.0f;
        /// <summary>Maximum bees per vanilla wild hive (hanging and in-log).</summary>
        [ProtoMember(60)]
        public int roamingBeesPerWildHive = 20;


        public FGCServerConfig()
        { }

        public static FGCServerConfig Current { get; set; }


        public static FGCServerConfig GetServerDefault()
        {
            FGCServerConfig defaultServerConfig = new();

            defaultServerConfig.ConfigVersion = 1.8;
            defaultServerConfig.retainConfigOnVersionChange = false;
            defaultServerConfig.SkepDaysToHarvestIn30DayMonths = 7;
            defaultServerConfig.ClayPotDaysToHarvestIn30DayMonths = 7;
            defaultServerConfig.LangstrothDaysToHarvestIn30DayMonths = 7f;
            defaultServerConfig.SkepMinYield = 1;
            defaultServerConfig.SkepMaxYield = 3;
            defaultServerConfig.CeramicPotMinYield = 2;
            defaultServerConfig.CeramicPotMaxYield = 4;
            defaultServerConfig.minFramePerCycle = 2;
            defaultServerConfig.maxFramePerCycle = 3;
            defaultServerConfig.FrameMinYield = 2;
            defaultServerConfig.FrameMaxYield = 4;
            defaultServerConfig.MaxStackSize = 6;
            defaultServerConfig.baseframedurability = 32;
            defaultServerConfig.minFramePerCycle = 2;
            defaultServerConfig.maxFramePerCycle = 4;
            defaultServerConfig.showcombpoptime = true;
            defaultServerConfig.SkepHiveMinTemp = 10f;
            defaultServerConfig.SkepHiveMaxTemp = 37f;
            defaultServerConfig.CeramicHiveMinTemp = 10f;
            defaultServerConfig.CeramicHiveMaxTemp = 37f;
            defaultServerConfig.LangstrothHiveMinTemp = 10f;
            defaultServerConfig.LangstrothHiveMaxTemp = 37f;
            defaultServerConfig.skepBaseChargesPerDay = 2;
            defaultServerConfig.skepMaxCropCharges = 60;
            defaultServerConfig.skepCropRange = 5;
            defaultServerConfig.ceramicBaseChargesPerDay = 4;
            defaultServerConfig.ceramicMaxCropCharges = 100;
            defaultServerConfig.ceramicCropRange = 7;
            defaultServerConfig.langstrothBaseChargesPerDay = 5;
            defaultServerConfig.langstrothMaxCropCharges = 150;
            defaultServerConfig.langstrothCropRange = 8;
            defaultServerConfig.showExtraBeehiveInfo = true;
            defaultServerConfig.backpackSlotOnly = false;
            defaultServerConfig.cropBoostPercentage = 0.20f;
            defaultServerConfig.minFlowersPerHive = 3;
            defaultServerConfig.canPlaceCeramicOnFence = false;
            defaultServerConfig.canPlaceLangstrothOnFence = false;
            defaultServerConfig.langstrothCropBoostPercentage = 0.30f;
            defaultServerConfig.ceramicCropBoostPercentage = 0.20f;
            defaultServerConfig.skepCropBoostPercentage = 0.10f;
            defaultServerConfig.roamingBeesEnabled = true;
            defaultServerConfig.roamingBeesPerSkep = 20;
            defaultServerConfig.roamingBeesPerCeramicHive = 30;
            defaultServerConfig.roamingBeesPerLangstrothHive = 40;
            defaultServerConfig.roamingBeesGlobalCap = 400;
            defaultServerConfig.roamingBeesRadius = 10;
            defaultServerConfig.roamingBeesMinFlowerVisits = 2;
            defaultServerConfig.roamingBeesMaxFlowerVisits = 6;
            defaultServerConfig.roamingBeesRainfallStop = 0.1f;
            defaultServerConfig.roamingBeesMinSunAltitudeDeg = -3f;
            defaultServerConfig.roamingBeesFullSunAltitudeDeg = 8f;
            defaultServerConfig.roamingBeesMinTemperature = 8f;
            defaultServerConfig.roamingBeesFullTemperature = 16f;
            defaultServerConfig.roamingBeesSpawnCooldownMs = 600;
            defaultServerConfig.roamingBeesVisitCrops = true;
            defaultServerConfig.roamingBeesScoutWithoutFlowers = true;
            defaultServerConfig.roamingBeesDensity = 1.0f;
            defaultServerConfig.roamingBeesPerWildHive = 20;
            defaultServerConfig.enabledInRoomOnly = true;
            defaultServerConfig.enabledGreenHouse = true;

            return defaultServerConfig;
        }

        internal static void createServerConfig(ICoreAPI api)
        {

            try
            {
                FGCServerConfig ServerConfig = api.LoadModConfig<FGCServerConfig>("fromgoldencombs/fromgoldencombsserver.json");
                if (ServerConfig != null && ServerConfig.ConfigVersion == FGCServerConfig.GetServerDefault().ConfigVersion)
                {
                    api.Logger.Notification(Lang.Get("fromgoldencombs:modserverconfigload"));
                    Current = ServerConfig;
                }
                else
                {
                    if (ServerConfig != null)
                    {
                        if ((ServerConfig?.ConfigVersion != FGCServerConfig.GetServerDefault().ConfigVersion) && ServerConfig.retainConfigOnVersionChange)
                        {
                            api.Logger.Notification(Lang.Get("fromgoldencombs:wrongserverconfigversion"));
                        }
                    }
                    api.Logger.Notification(Lang.Get("fromgoldencombs:nomodserverconfig"));
                    Current = GetServerDefault();
                }
            }
            catch
            {
                Current = GetServerDefault();
                api.Logger.Error(Lang.Get("fromgoldencombs:defaultserverconfigloaded"));
            }
            finally
            {
                api.StoreModConfig(Current, "fromgoldencombs/fromgoldencombsserver.json");
            }
        }
    }
}
