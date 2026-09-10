using FromGoldenCombs.Util.Config;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace FromGoldenCombs.RoamingBees
{
    /// <summary>
    /// Server-side owner of all roaming bees. Hives register themselves; the manager visits a
    /// slice of them every tick (round robin, so no hive is touched every 20 ms), decides how
    /// many bees each should have right now, plans flights and tells nearby clients about them.
    /// </summary>
    public class RoamingBeeServer
    {
        const int TickMs = 200;
        const double SendRange = 160;
        const int EnvSampleMs = 5000;
        /// <summary>Flight planning is the only non-trivial server work; bound it per tick (20 flights per second).</summary>
        const int MaxSpawnsPerTick = 4;

        class Colony
        {
            public IRoamingBeeHive Hive;
            public BlockPos Pos;
            public Vec3d Entrance;
            public Vec3f Front;
            public string FacingUsed;
            public int LiveCount;
            public long NextSpawnAt;
            public long EnvSampledAt = -1;   // never use long.MinValue here: 'now - MinValue' overflows
            public float EnvFactor;
        }

        class LiveBee
        {
            public int Id;
            public Colony Colony;
            public BeeFlightPath Path;
            public long SpawnedAt;
            public int DurationMs;
        }

        readonly ICoreServerAPI sapi;
        readonly IServerNetworkChannel channel;
        readonly ForageRegistry forage;
        readonly BeeFlightPlanner planner;
        readonly List<Colony> colonies = new List<Colony>();
        readonly Dictionary<BlockPos, Colony> byPos = new Dictionary<BlockPos, Colony>();
        readonly List<LiveBee> bees = new List<LiveBee>();
        readonly List<BlockPos> plantScratch = new List<BlockPos>();
        readonly List<IServerPlayer> playerScratch = new List<IServerPlayer>();
        int nextBeeId = 1;
        int roundRobin;
        int spawnsThisTick;
        long tickListenerId;

        public int ColonyCount => colonies.Count;
        public int LiveBeeCount => bees.Count;
        public ForageRegistry Forage => forage;

        public RoamingBeeServer(ICoreServerAPI sapi, IServerNetworkChannel channel)
        {
            this.sapi = sapi;
            this.channel = channel;
            forage = new ForageRegistry(sapi);
            planner = new BeeFlightPlanner(sapi.World.BlockAccessor, sapi.World.Rand);

            channel.SetMessageHandler<BeeCatchupRequestPacket>(OnCatchupRequest);
            tickListenerId = sapi.Event.RegisterGameTickListener(OnTick, TickMs);
            sapi.Event.DidPlaceBlock += OnDidPlaceBlock;
            sapi.Event.DidBreakBlock += OnDidBreakBlock;
        }

        public void Dispose()
        {
            if (tickListenerId != 0) sapi.Event.UnregisterGameTickListener(tickListenerId);
            tickListenerId = 0;
            sapi.Event.DidPlaceBlock -= OnDidPlaceBlock;
            sapi.Event.DidBreakBlock -= OnDidBreakBlock;
            colonies.Clear();
            byPos.Clear();
            bees.Clear();
        }

        public void Register(IRoamingBeeHive hive)
        {
            if (hive?.HivePos == null) return;
            FGCServerConfig cfg = FGCServerConfig.Current;
            if (cfg == null || !cfg.roamingBeesEnabled) return;

            BlockPos pos = hive.HivePos.Copy();
            if (byPos.TryGetValue(pos, out Colony existing))
            {
                existing.Hive = hive;   // block entity instance was replaced (chunk reload)
                return;
            }

            var colony = new Colony { Hive = hive, Pos = pos, NextSpawnAt = sapi.World.ElapsedMilliseconds + 2000 + sapi.World.Rand.Next(3000) };
            colonies.Add(colony);
            byPos[pos] = colony;
            forage.Register(pos, cfg.roamingBeesRadius);
        }

        public void Unregister(BlockPos pos)
        {
            if (pos == null || !byPos.TryGetValue(pos, out Colony colony)) return;
            byPos.Remove(pos);
            colonies.Remove(colony);
            forage.Unregister(pos);
            // bees already in the air finish their flight on the clients; drop the bookkeeping now
            for (int i = bees.Count - 1; i >= 0; i--)
            {
                if (bees[i].Colony == colony) bees.RemoveAt(i);
            }
        }

        void OnTick(float dt)
        {
            FGCServerConfig cfg = FGCServerConfig.Current;
            if (cfg == null || !cfg.roamingBeesEnabled) return;

            long now = sapi.World.ElapsedMilliseconds;
            forage.IncludeCrops = cfg.roamingBeesVisitCrops;
            forage.Tick(now);

            for (int i = bees.Count - 1; i >= 0; i--)
            {
                LiveBee bee = bees[i];
                if (now >= bee.SpawnedAt + bee.DurationMs)
                {
                    bee.Colony.LiveCount = Math.Max(0, bee.Colony.LiveCount - 1);
                    bees.RemoveAt(i);
                }
            }

            if (colonies.Count == 0) return;

            spawnsThisTick = 0;
            int perTick = Math.Max(3, (colonies.Count + 4) / 5);
            perTick = Math.Min(perTick, colonies.Count);
            for (int k = 0; k < perTick && spawnsThisTick < MaxSpawnsPerTick; k++)
            {
                roundRobin = (roundRobin + 1) % colonies.Count;
                TryServe(colonies[roundRobin], now, cfg);
            }
        }

        void TryServe(Colony colony, long now, FGCServerConfig cfg)
        {
            if (now < colony.NextSpawnAt) return;

            bool active;
            try { active = colony.Hive.RoamingBeesActive; }
            catch (Exception) { active = false; }
            if (!active)
            {
                colony.NextSpawnAt = now + 2000;
                return;
            }

            // nobody close enough to see them: skip the planning work (matters with many wild hives loaded)
            if (!AnyPlayerWithin(colony.Pos, SendRange))
            {
                colony.NextSpawnAt = now + 5000;
                return;
            }

            int max = MaxForType(colony.Hive.RoamingHiveType, cfg);
            float env = EnvironmentFactor(colony, now, cfg);
            float activity = GameMath.Clamp(colony.Hive.RoamingActivityLevel, 0f, 1f);
            float pop = PopFactor(colony.Hive.RoamingPopSize);
            float density = Math.Max(0f, cfg.roamingBeesDensity);
            int desired = (int)Math.Round(max * pop * activity * env * density);

            if (desired <= colony.LiveCount || bees.Count >= cfg.roamingBeesGlobalCap)
            {
                colony.NextSpawnAt = now + 1000;
                return;
            }

            EnsureEntrance(colony);

            planner.MinVisits = cfg.roamingBeesMinFlowerVisits;
            planner.MaxVisits = cfg.roamingBeesMaxFlowerVisits;

            plantScratch.Clear();
            forage.Query(colony.Entrance, cfg.roamingBeesRadius, plantScratch);

            BeeFlightPath path = planner.Plan(colony.Pos, colony.Entrance, colony.Front, plantScratch, cfg.roamingBeesRadius, cfg.roamingBeesScoutWithoutFlowers);
            if (path == null || !path.IsValid)
            {
                colony.NextSpawnAt = now + 4000;
                return;
            }

            var timeline = new BeeFlightTimeline(path);
            var bee = new LiveBee
            {
                Id = nextBeeId++,
                Colony = colony,
                Path = path,
                SpawnedAt = now,
                DurationMs = (int)(timeline.TotalSeconds * 1000f) + 100,
            };
            bees.Add(bee);
            colony.LiveCount++;
            spawnsThisTick++;

            SendNear(new BeeSpawnPacket { BeeId = bee.Id, Path = path, ElapsedMs = 0 }, colony.Entrance);

            int weatherPenalty = (int)((1f - env) * 3000f);
            colony.NextSpawnAt = now + Math.Max(200, cfg.roamingBeesSpawnCooldownMs) + weatherPenalty;
        }

        void EnsureEntrance(Colony colony)
        {
            string facing = null;
            try { facing = colony.Hive.RoamingFacing; } catch (Exception) { }
            if (colony.Entrance != null && colony.FacingUsed == facing) return;

            HiveEntrance.Resolve(colony.Hive.RoamingHiveType, facing, colony.Pos, out Vec3d entrance, out Vec3f front);
            colony.Entrance = entrance;
            colony.Front = front;
            colony.FacingUsed = facing;
        }

        static int MaxForType(EnumRoamingHiveType type, FGCServerConfig cfg)
        {
            return type switch
            {
                EnumRoamingHiveType.Ceramic => cfg.roamingBeesPerCeramicHive,
                EnumRoamingHiveType.Langstroth => cfg.roamingBeesPerLangstrothHive,
                EnumRoamingHiveType.Wild => cfg.roamingBeesPerWildHive,
                EnumRoamingHiveType.WildLog => cfg.roamingBeesPerWildHive,
                _ => cfg.roamingBeesPerSkep,
            };
        }

        static float PopFactor(EnumHivePopSize pop)
        {
            return pop switch
            {
                EnumHivePopSize.Large => 1f,
                EnumHivePopSize.Decent => 0.65f,
                _ => 0.3f,
            };
        }

        /// <summary>0..1 from rain, sun altitude and temperature. Cached for a few seconds per colony.</summary>
        float EnvironmentFactor(Colony colony, long now, FGCServerConfig cfg)
        {
            if (colony.EnvSampledAt >= 0 && now - colony.EnvSampledAt < EnvSampleMs) return colony.EnvFactor;
            colony.EnvSampledAt = now;

            float factor = 1f;
            ClimateCondition climate = null;
            try { climate = sapi.World.BlockAccessor.GetClimateAt(colony.Pos, EnumGetClimateMode.NowValues); } catch (Exception) { }

            float rain = climate?.Rainfall ?? 0f;
            float rainStop = Math.Max(0.01f, cfg.roamingBeesRainfallStop);
            factor *= rain >= rainStop ? 0f : 1f - rain / rainStop;

            float temp = (climate?.Temperature ?? 20f) + (colony.Hive.RoamingRoomness > 0 ? 5f : 0f);
            factor *= Ramp(temp, cfg.roamingBeesMinTemperature, cfg.roamingBeesFullTemperature);

            factor *= Ramp(SunAltitudeDegrees(colony.Pos), cfg.roamingBeesMinSunAltitudeDeg, cfg.roamingBeesFullSunAltitudeDeg);

            colony.EnvFactor = GameMath.Clamp(factor, 0f, 1f);
            return colony.EnvFactor;
        }

        float SunAltitudeDegrees(BlockPos pos)
        {
            try
            {
                // GetSunPosition returns a unit vector whose Y is cos(zenith), i.e. sin(altitude)
                Vec3f sun = sapi.World.Calendar.GetSunPosition(new Vec3d(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5), sapi.World.Calendar.TotalDays);
                float len = sun.Length();
                if (len < 1e-5f) return 90f;
                return (float)(Math.Asin(GameMath.Clamp(sun.Y / len, -1f, 1f)) * 180.0 / Math.PI);
            }
            catch (Exception)
            {
                float light = sapi.World.Calendar.GetDayLightStrength(pos.X, pos.Z);
                return light * 90f - 10f;
            }
        }

        static float Ramp(float value, float zeroAt, float fullAt)
        {
            if (fullAt <= zeroAt) return value >= fullAt ? 1f : 0f;
            return GameMath.Clamp((value - zeroAt) / (fullAt - zeroAt), 0f, 1f);
        }

        void SendNear(BeeSpawnPacket packet, Vec3d center)
        {
            playerScratch.Clear();
            foreach (IPlayer player in sapi.World.AllOnlinePlayers)
            {
                if (player is not IServerPlayer sp || sp.ConnectionState != EnumClientState.Playing || sp.Entity == null) continue;
                if (Distance(sp, center) <= SendRange) playerScratch.Add(sp);
            }
            if (playerScratch.Count > 0) channel.SendPacket(packet, playerScratch.ToArray());
        }

        bool AnyPlayerWithin(BlockPos pos, double range)
        {
            double r2 = range * range;
            foreach (IPlayer player in sapi.World.AllOnlinePlayers)
            {
                if (player?.Entity == null) continue;
                double dx = player.Entity.Pos.X - (pos.X + 0.5), dy = player.Entity.Pos.Y - (pos.Y + 0.5), dz = player.Entity.Pos.Z - (pos.Z + 0.5);
                if (dx * dx + dy * dy + dz * dz <= r2) return true;
            }
            return false;
        }

        static double Distance(IServerPlayer sp, Vec3d center)
        {
            double dx = sp.Entity.Pos.X - center.X, dy = sp.Entity.Pos.Y - center.Y, dz = sp.Entity.Pos.Z - center.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        void OnCatchupRequest(IServerPlayer fromPlayer, BeeCatchupRequestPacket packet)
        {
            if (fromPlayer?.Entity == null || bees.Count == 0) return;
            long now = sapi.World.ElapsedMilliseconds;
            var list = new List<BeeSpawnPacket>();
            foreach (LiveBee bee in bees)
            {
                if (bee.Colony.Entrance == null || Distance(fromPlayer, bee.Colony.Entrance) > SendRange) continue;
                list.Add(new BeeSpawnPacket { BeeId = bee.Id, Path = bee.Path, ElapsedMs = (int)(now - bee.SpawnedAt) });
            }
            if (list.Count > 0) channel.SendPacket(new BeeCatchupPacket { Bees = list.ToArray() }, fromPlayer);
        }

        void OnDidPlaceBlock(IServerPlayer byPlayer, int oldblockId, BlockSelection blockSel, ItemStack withItemStack)
        {
            if (blockSel?.Position != null) forage.OnBlockChanged(blockSel.Position);
        }

        void OnDidBreakBlock(IServerPlayer byPlayer, int oldblockId, BlockSelection blockSel)
        {
            if (blockSel?.Position != null) forage.OnBlockChanged(blockSel.Position);
        }
    }
}
