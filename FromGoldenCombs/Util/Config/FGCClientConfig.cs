using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace FromGoldenCombs.Util.Config
{
    class FGCClientConfig
    {
        public double configVersion = 1.8;
        public bool retainConfigOnVersionChange = false;
        //Hive_Sound_Instructions is basically a comment, it does nothing.
        public string Hive_Sound_Instructions = "Changes the base volume of all domestic beehives. Valid settings for hive sounds are off, soft, normal, high (2x), and loud (4x)";
        public string hiveSoundVolume = "normal";

        //Wild_Hive_Sound_Instructions is effectively a comment, it does nothing.
        public string Wild_Hive_Sound_Instructions = "Changes the base volume of all wild beehives. Valid settings for wild hive sounds are normal, high (2x), and loud (4x)";
        public string wildHiveSoundVolume = "normal";
        public string Always_Show_Hive_Info_Instructions = "Hive info includes the three day temp and remaining pollination charges, only works if the server has .";
        public bool alwaysShowExtraBeehiveInfo = false;
        //Roaming_Bees_Instructions is effectively a comment, it does nothing.
        public string Roaming_Bees_Instructions = "showRoamingBees turns the roaming bee visuals on or off on this client. roamingBeesRenderDistance is the maximum distance in blocks at which bees are drawn (8-256). roamingBeesMaxDrawn caps how many bees are drawn per tick, nearest first (10-2000); bees within 12 blocks get full detail, within 24 blocks body/head/tail only, further away a single dot. roamingBeesEyesAndAntennae adds eyes and antennae to nearby bees (more particles). roamingBeesAngrySwarm draws the angry bee swarm that appears when a hive is broken as roaming bees instead of the vanilla shape; roamingBeesPerAngrySwarm is how many (0-200).";
        public bool showRoamingBees = true;
        public int roamingBeesRenderDistance = 48;
        public int roamingBeesMaxDrawn = 300;
        public bool roamingBeesEyesAndAntennae = false;
        public bool roamingBeesAngrySwarm = true;
        public int roamingBeesPerAngrySwarm = 30;

        public static FGCClientConfig Current { get; set; }

        public FGCClientConfig()
        { }

        public static FGCClientConfig GetClientDefault()
        {
            FGCClientConfig defaultClientConfig = new();
            defaultClientConfig.configVersion = 1.8;
            defaultClientConfig.retainConfigOnVersionChange = false;
            defaultClientConfig.Hive_Sound_Instructions = "Changes the base volume of all domestic beehives. Valid settings for hive sound are off, soft, normal, high (2x), and loud (4x)";
            defaultClientConfig.hiveSoundVolume = "normal";
            defaultClientConfig.Wild_Hive_Sound_Instructions = "Changes the base volume of all wild beehives. Valid settings for wild hive sound are normal, high (2x), and loud (4x)";
            defaultClientConfig.wildHiveSoundVolume = "normal";
            defaultClientConfig.Always_Show_Hive_Info_Instructions = "Hive info includes the three day temp and remaining pollination charges.";
            defaultClientConfig.alwaysShowExtraBeehiveInfo=false;
            defaultClientConfig.showRoamingBees = true;
            defaultClientConfig.roamingBeesRenderDistance = 48;
            defaultClientConfig.roamingBeesMaxDrawn = 300;
            defaultClientConfig.roamingBeesEyesAndAntennae = false;
            defaultClientConfig.roamingBeesAngrySwarm = true;
            defaultClientConfig.roamingBeesPerAngrySwarm = 30;

            return defaultClientConfig;
        }

        internal static void createClientConfig(ICoreAPI api)
        {
            String[] validHiveVolumes = {"off","soft","normal","high","loud" };
            String[] validWildHiveVolumes = {"normal", "high", "loud"};
            double MasterClientConfigVersion = 1.8;

            try
            {
                FGCClientConfig ClientConfig = api.LoadModConfig<FGCClientConfig>("fromgoldencombs/fromgoldencombsclient.json");
                if (ClientConfig != null && ClientConfig.configVersion == MasterClientConfigVersion)
                {
                    api.Logger.Notification(Lang.Get("fromgoldencombs:modclientconfigload"));

                    if (ClientConfig.Hive_Sound_Instructions != "Changes the base volume of all domestic beehives. Valid settings for hive sound are off, soft, normal, high (2x), and loud (4x)")
                    {
                        ClientConfig.Hive_Sound_Instructions = "Changes the base volume of all domestic beehives. Valid settings for hive sound are off, soft, normal, high (2x), and loud (4x)";
                    }
                    if (ClientConfig.Wild_Hive_Sound_Instructions != "Changes the base volume of all wild beehives. Valid settings for wild hive sound are normal, high (2x), and loud (4x)")
                    {
                        ClientConfig.Wild_Hive_Sound_Instructions = "Changes the base volume of all wild beehives. Valid settings for wild hive sound are normal, high (2x), and loud (4x)";
                    }
                    if (!validHiveVolumes.Contains<string>(ClientConfig.hiveSoundVolume))
                    {
                          ClientConfig.hiveSoundVolume = "normal";
                    }
                    if (ClientConfig.Always_Show_Hive_Info_Instructions != "Hive info includes the three day temp and remaining pollination charges.")
                    {
                        ClientConfig.Always_Show_Hive_Info_Instructions = "Hive info includes the three day temp and remaining pollination charges.";
                    }
                    if (!validWildHiveVolumes.Contains<string>(ClientConfig.wildHiveSoundVolume))
                    {
                        ClientConfig.wildHiveSoundVolume = "normal";
                    }

                    ClientConfig.roamingBeesRenderDistance = Math.Clamp(ClientConfig.roamingBeesRenderDistance, 8, 256);
                    ClientConfig.roamingBeesMaxDrawn = Math.Clamp(ClientConfig.roamingBeesMaxDrawn, 10, 2000);
                    ClientConfig.roamingBeesPerAngrySwarm = Math.Clamp(ClientConfig.roamingBeesPerAngrySwarm, 0, 200);

                    Current = ClientConfig;
                }
                else
                {
                    if (ClientConfig != null)
                    {
                        if ((ClientConfig?.configVersion != MasterClientConfigVersion) && !ClientConfig.retainConfigOnVersionChange)
                        {
                            api.Logger.Notification(Lang.Get("fromgoldencombs:wrongclientconfigversion"));
                        }
                    }
                    api.Logger.Notification(Lang.Get("fromgoldencombs:nomodclientconfig"));
                    Current = GetClientDefault();
                }
            }
            catch
            {
                Current = GetClientDefault();
                api.Logger.Error(Lang.Get("fromgoldencombs:defaultclientloaded"));
            }
            finally
            {
                api.StoreModConfig(Current, "fromgoldencombs/fromgoldencombsclient.json");
            }
        }
    }
}
