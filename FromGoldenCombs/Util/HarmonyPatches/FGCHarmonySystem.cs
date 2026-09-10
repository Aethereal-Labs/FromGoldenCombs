using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using HarmonyLib;
using System;
using Vintagestory.GameContent;
using System.Reflection;
using Vintagestory.API.Datastructures;
using FromGoldenCombs.BlockEntities;
using FromGoldenCombs.RoamingBees;
using FromGoldenCombs.Util.Config;


namespace FromGoldenCombs.Util.HarmonyPatches
{
    public class FGCHarmonySystem : ModSystem
    {
        private Harmony harmony;
        private readonly string harmonyId = "vinternacht.FGCPatches";


        public override void Start(ICoreAPI api)
        {
            PatchGame();
            base.Start(api);
        }
        private void PatchGame()
        {
            harmony = new Harmony(harmonyId);
            harmony.Patch(typeof(BlockEntityFruitTreePart).GetMethod("OnBlockInteractStop", BindingFlags.Instance | BindingFlags.Public),
                prefix: new HarmonyMethod(typeof(FGCHarmonySystem).GetMethod("OnBlockInteractStopPrefix", BindingFlags.Static | BindingFlags.Public))
            );
            harmony.Patch(typeof(Block).GetMethod("GetAmbientSoundStrength", BindingFlags.Instance | BindingFlags.Public),
            prefix: new HarmonyMethod(typeof(FGCHarmonySystem).GetMethod("GetAmbientSoundStrengthPrefix", BindingFlags.Static | BindingFlags.Public))
            );
            // Fork: roaming bees replace the vanilla skep bee particles on FGC skeps while enabled
            var vanillaBeeParticles = AccessTools.Method(typeof(BlockEntityBeehive), "SpawnBeeParticles");
            if (vanillaBeeParticles != null)
            {
                harmony.Patch(vanillaBeeParticles,
                    prefix: new HarmonyMethod(typeof(FGCHarmonySystem).GetMethod(nameof(SkipVanillaBeeParticlesPrefix), BindingFlags.Static | BindingFlags.Public)));
            }
            // Fork: vanilla wild hives (BlockBeehive with the vanilla block entity) join the roaming bee system
            var beInitialize = AccessTools.Method(typeof(BlockEntityBeehive), nameof(BlockEntityBeehive.Initialize));
            if (beInitialize != null)
            {
                harmony.Patch(beInitialize, postfix: new HarmonyMethod(typeof(FGCHarmonySystem).GetMethod(nameof(WildHiveInitializedPostfix), BindingFlags.Static | BindingFlags.Public)));
            }
            foreach (string gone in new[] { nameof(BlockEntity.OnBlockRemoved), nameof(BlockEntity.OnBlockUnloaded) })
            {
                var method = AccessTools.Method(typeof(BlockEntityBeehive), gone);
                if (method != null)
                {
                    harmony.Patch(method, postfix: new HarmonyMethod(typeof(FGCHarmonySystem).GetMethod(nameof(WildHiveGonePostfix), BindingFlags.Static | BindingFlags.Public)));
                }
            }

            // Fork: the angry bee swarm entity is drawn with roaming bees instead of its own shape
            foreach (string render in new[] { "DoRender3DOpaque", "DoRender3DOpaqueBatched" })
            {
                var method = AccessTools.Method(typeof(EntityShapeRenderer), render);
                if (method != null)
                {
                    harmony.Patch(method, prefix: new HarmonyMethod(typeof(FGCHarmonySystem).GetMethod(nameof(HideBeeMobShapePrefix), BindingFlags.Static | BindingFlags.Public)));
                }
            }
            harmony.PatchAll();
        }

        public static bool SkipVanillaBeeParticlesPrefix(BlockEntityBeehive __instance)
        {
            return !(__instance is BEFGCBeehive && FGCServerConfig.Current != null && FGCServerConfig.Current.roamingBeesEnabled);
        }

        /// <summary>Registers vanilla wild hives with the roaming bee manager (FGC skeps register themselves).</summary>
        public static void WildHiveInitializedPostfix(BlockEntity __instance, ICoreAPI api)
        {
            if (api == null || api.Side != EnumAppSide.Server) return;
            if (__instance is not BlockEntityBeehive be || __instance is BEFGCBeehive) return;
            if (be.Block is not BlockBeehive) return;
            if (FGCServerConfig.Current == null || !FGCServerConfig.Current.roamingBeesEnabled) return;
            RoamingBeesModSystem.RegisterHive(api, new WildHiveAdapter(be));
        }

        public static void WildHiveGonePostfix(BlockEntity __instance)
        {
            if (__instance is not BlockEntityBeehive || __instance is BEFGCBeehive) return;
            ICoreAPI api = __instance.Api;
            if (api == null || api.Side != EnumAppSide.Server) return;
            RoamingBeesModSystem.UnregisterHive(api, __instance.Pos);
        }

        /// <summary>Skips the vanilla swarm mesh only while the client is actually drawing that swarm as roaming bees.</summary>
        public static bool HideBeeMobShapePrefix(EntityShapeRenderer __instance)
        {
            var entity = __instance.entity;
            if (!AngrySwarmClient.IsSwarm(entity)) return true;
            return !AngrySwarmClient.IsDrawing(entity);
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(BlockEntityFruitTreePart), "OnBlockInteractStop")]
        public static bool OnBlockInteractStopPrefix(BlockEntityFruitTreePart __instance, float secondsUsed, IPlayer byPlayer, BlockSelection blockSel)
        {

            if (byPlayer.Entity.Api.Side.IsServer()) { 
                if ((double)secondsUsed > 1.1 && __instance.FoliageState == EnumFoliageState.Ripe)
                {
                    counter++;
                    if (counter > 1) { 
                        //This is some jury-rigged bullshit until I can find out why this is getting called twice. 
                        counter = 0; return true; 
                    }
                    if (byPlayer != null)
                    {
                        TreeAttribute tree = new ();
                        tree.SetInt("x", __instance.Pos.X);
                        tree.SetInt("y", __instance.Pos.Y);
                        tree.SetInt("z", __instance.Pos.Z);
                        byPlayer.Entity.World.Api.Event.PushEvent("fruitharvest", tree);
                    }
                }
            }
            return true;
        }

        public static bool GetAmbientSoundStrengthPrefix(Block __instance, IWorldAccessor world, BlockPos pos, ref float __result)
        {
 
            float soundVolume = 0f;
            if (__instance is BlockBeehive)
            {
                switch (FGCClientConfig.Current.wildHiveSoundVolume)
                {
                    case "normal": soundVolume = 1f; break;
                    case "high": soundVolume *= 2f; break;
                    case "loud": soundVolume *= 4f; break;
                    default: soundVolume = 1f; break;
                }
                __result = soundVolume;
                return false;
            }
                
            if (world.BlockAccessor.GetBlockEntity(pos) is BEFGCBeehive skep)
            {

                //Below switch statement converted from the below. Seems to be possible because of numbers?
                //soundVolume = 0f;
                //
                //switch ((int)skep.hivePopSize)
                //{
                //    case 0: soundVolume = 0.44f; break;
                //    case 1: soundVolume = 0.88f; break;
                //    default: soundVolume = 1f; break;
                //}
                soundVolume = (int)skep.hivePopSize switch
                {
                    0 => 0.44f,
                    1 => 0.88f,
                    _ => 1f,
                };
                
                switch (FGCClientConfig.Current.hiveSoundVolume)
                {
                    case "off": soundVolume = 0f; break;
                    case "soft": soundVolume *= 0.5f; break;
                    case "normal": soundVolume = 1f; break;
                    case "high": soundVolume *= 2f; break;
                    case "loud": soundVolume *= 4f; break;
                    default: soundVolume = 1f; break;
                }
                soundVolume = Math.Max(soundVolume * skep.actvitiyLevel, 0.4f);
                __result = soundVolume;
                return false;
            }
            return true;
        }


        static int counter;

        public override void Dispose()
        {
            harmony?.UnpatchAll();
            harmony = null;

        }
    }
}
