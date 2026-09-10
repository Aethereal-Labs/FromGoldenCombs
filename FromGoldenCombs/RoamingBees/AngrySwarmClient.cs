using FromGoldenCombs.Util.Config;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace FromGoldenCombs.RoamingBees
{
    /// <summary>
    /// Draws the vanilla angry bee swarm (the "beemob" entity the game spawns when a hive is broken)
    /// as a cloud of roaming bees orbiting the entity's position. Purely client-side: the entity's
    /// position, chasing and stinging are unchanged; only its look is replaced. Its own shape is
    /// hidden by a Harmony prefix while the swarm is being drawn (see FGCHarmonySystem).
    ///
    /// Swarms are found by scanning the client's loaded entities twice a second (entity events are
    /// used as a fast path only), so a swarm is never hidden without being drawn.
    /// </summary>
    public class AngrySwarmClient
    {
        const int RescanMs = 500;

        class Swarm
        {
            public Entity Entity;
            public Vec3d LastCenter;
            public long LastMs;
            public readonly Vec3d Velocity = new Vec3d();
            public uint Seed;
        }

        static AngrySwarmClient instance;

        readonly ICoreClientAPI capi;
        readonly BeeParticleRenderer renderer;
        readonly List<Swarm> swarms = new List<Swarm>();
        readonly Dictionary<long, Swarm> byId = new Dictionary<long, Swarm>();
        readonly Vec3d pos = new Vec3d();
        readonly Vec3d vel = new Vec3d();
        long lastRescanMs = -1;

        public int SwarmCount => swarms.Count;

        public AngrySwarmClient(ICoreClientAPI capi, BeeParticleRenderer renderer)
        {
            this.capi = capi;
            this.renderer = renderer;
            instance = this;
            capi.Event.OnEntityLoaded += OnEntityLoaded;
            capi.Event.OnEntitySpawn += OnEntityLoaded;
            capi.Event.OnEntityDespawn += OnEntityDespawn;
        }

        public void Dispose()
        {
            capi.Event.OnEntityLoaded -= OnEntityLoaded;
            capi.Event.OnEntitySpawn -= OnEntityLoaded;
            capi.Event.OnEntityDespawn -= OnEntityDespawn;
            swarms.Clear();
            byId.Clear();
            if (instance == this) instance = null;
        }

        /// <summary>True when this client should draw swarms.</summary>
        public static bool Enabled
        {
            get
            {
                FGCClientConfig c = FGCClientConfig.Current;
                if (c != null && (!c.showRoamingBees || !c.roamingBeesAngrySwarm)) return false;
                return FGCServerConfig.Current == null || FGCServerConfig.Current.roamingBeesEnabled;
            }
        }

        /// <summary>Used by the Harmony prefix: hide the vanilla shape only for swarms that are actually being drawn.</summary>
        public static bool IsDrawing(Entity entity)
        {
            return entity != null && Enabled && instance != null && instance.byId.ContainsKey(entity.EntityId);
        }

        public static bool IsSwarm(Entity entity)
        {
            return entity is EntityBeeMob || entity?.Code?.Path == "beemob";
        }

        void OnEntityLoaded(Entity entity)
        {
            if (IsSwarm(entity)) Track(entity);
        }

        void OnEntityDespawn(Entity entity, EntityDespawnData despawn)
        {
            Untrack(entity?.EntityId ?? -1);
        }

        void Track(Entity entity)
        {
            if (byId.ContainsKey(entity.EntityId)) return;
            var swarm = new Swarm { Entity = entity, Seed = Hash((uint)entity.EntityId * 2654435761u) };
            swarms.Add(swarm);
            byId[entity.EntityId] = swarm;
        }

        void Untrack(long entityId)
        {
            if (byId.TryGetValue(entityId, out Swarm swarm))
            {
                byId.Remove(entityId);
                swarms.Remove(swarm);
            }
        }

        void Rescan()
        {
            var loaded = capi.World.LoadedEntities;
            if (loaded == null) return;

            foreach (Entity entity in loaded.Values)
            {
                if (entity != null && entity.Alive && IsSwarm(entity)) Track(entity);
            }
            for (int i = swarms.Count - 1; i >= 0; i--)
            {
                Entity e = swarms[i].Entity;
                if (e == null || !e.Alive || e.ShouldDespawn || !loaded.ContainsKey(e.EntityId))
                {
                    byId.Remove(e?.EntityId ?? -1);
                    swarms.RemoveAt(i);
                }
            }
        }

        public void Tick(long nowMs, double px, double py, double pz, double maxDistSq, float wind)
        {
            if (!Enabled)
            {
                if (swarms.Count > 0) { swarms.Clear(); byId.Clear(); }
                return;
            }

            if (lastRescanMs < 0 || nowMs - lastRescanMs >= RescanMs)
            {
                lastRescanMs = nowMs;
                Rescan();
            }
            if (swarms.Count == 0) return;

            int perSwarm = Math.Clamp(FGCClientConfig.Current?.roamingBeesPerAngrySwarm ?? 30, 0, 200);
            if (perSwarm == 0) return;
            float t = nowMs / 1000f;

            foreach (Swarm swarm in swarms)
            {
                Entity e = swarm.Entity;
                if (e == null) continue;

                double cx = e.Pos.X, cy = e.Pos.Y + 0.25, cz = e.Pos.Z;   // hitbox is 0.5 high
                if (swarm.LastCenter == null)
                {
                    swarm.LastCenter = new Vec3d(cx, cy, cz);
                    swarm.LastMs = nowMs;
                }
                else
                {
                    double dtSec = Math.Max(0.001, (nowMs - swarm.LastMs) / 1000.0);
                    swarm.Velocity.Set((cx - swarm.LastCenter.X) / dtSec, (cy - swarm.LastCenter.Y) / dtSec, (cz - swarm.LastCenter.Z) / dtSec);
                    swarm.LastCenter.Set(cx, cy, cz);
                    swarm.LastMs = nowMs;
                }

                double dx = cx - px, dy = cy - py, dz = cz - pz;
                double distSq = dx * dx + dy * dy + dz * dz;
                if (distSq > maxDistSq) continue;
                double dist = Math.Sqrt(distSq);
                EnumBeeDetail detail = dist < RoamingBeeClient.FullDetailDistance ? EnumBeeDetail.Full
                    : dist < RoamingBeeClient.BasicDetailDistance ? EnumBeeDetail.Basic : EnumBeeDetail.Dot;

                float windSpin = 1f + 0.5f * GameMath.Clamp(wind, 0f, 1f);
                for (int b = 0; b < perSwarm; b++)
                {
                    // deterministic per-bee orbit parameters from a hash: radius, speed, direction, phase, bob
                    uint h = Hash(swarm.Seed + (uint)b * 7919u);
                    double radius = 0.2 + 0.75 * Frac(h);
                    double omega = (4.0 + 4.0 * Frac(h >> 6)) * windSpin * ((h & 1) == 0 ? 1 : -1);
                    double phase = Math.PI * 2 * Frac(h >> 12);
                    double bobAmp = 0.1 + 0.3 * Frac(h >> 18);
                    double bobFreq = 2.0 + 3.0 * Frac(h >> 24);
                    double bobPhase = Math.PI * 2 * Frac(h >> 3);

                    double a = omega * t + phase;
                    double bobA = bobFreq * t + bobPhase;
                    double sinA = Math.Sin(a), cosA = Math.Cos(a);

                    pos.Set(cx + radius * cosA, cy + bobAmp * Math.Sin(bobA), cz + radius * sinA);
                    vel.Set(swarm.Velocity.X - radius * omega * sinA, swarm.Velocity.Y + bobAmp * bobFreq * Math.Cos(bobA), swarm.Velocity.Z + radius * omega * cosA);

                    renderer.Draw(pos, vel, t + b * 0.037f, detail);
                }
            }
        }

        static uint Hash(uint x)
        {
            x ^= x >> 16;
            x *= 0x7feb352d;
            x ^= x >> 15;
            x *= 0x846ca68b;
            x ^= x >> 16;
            return x;
        }

        /// <summary>0..1 from the low 16 bits.</summary>
        static double Frac(uint h) => (h & 0xFFFF) / 65536.0;
    }
}
