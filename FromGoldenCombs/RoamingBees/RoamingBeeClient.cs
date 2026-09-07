using FromGoldenCombs.Util.Config;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace FromGoldenCombs.RoamingBees
{
    /// <summary>
    /// Client-side replay of the flights the server announced. Every bee is a pure function of
    /// time, so a late join simply starts the clock in the past.
    ///
    /// Particle cost is bounded: bees are drawn nearest first up to roamingBeesMaxDrawn per tick,
    /// with full detail inside FullDetailDistance, body/head/tail inside BasicDetailDistance and a
    /// single dot every other tick beyond that.
    /// </summary>
    public class RoamingBeeClient
    {
        const int TickMs = 20;
        const int WindSampleMs = 2000;
        public const double FullDetailDistance = 12;
        public const double BasicDetailDistance = 24;

        class ClientBee
        {
            public int Id;
            public BeeFlightPath Path;
            public BeeFlightTimeline Timeline;
            public long StartMs;
        }

        struct Candidate
        {
            public ClientBee Bee;
            public float T;
            public double DistSq;
        }

        readonly ICoreClientAPI capi;
        readonly IClientNetworkChannel channel;
        readonly List<ClientBee> bees = new List<ClientBee>();
        readonly HashSet<int> knownIds = new HashSet<int>();
        readonly List<Candidate> candidates = new List<Candidate>();
        readonly BeeParticleRenderer renderer;
        readonly AngrySwarmClient swarms;
        readonly Vec3d pos = new Vec3d();
        readonly Vec3d posAhead = new Vec3d();
        readonly Vec3d velocity = new Vec3d();
        long tickListenerId;
        long windSampledAt = -1;
        float wind;
        int tickCounter;

        public int BeeCount => bees.Count;

        public RoamingBeeClient(ICoreClientAPI capi, IClientNetworkChannel channel)
        {
            this.capi = capi;
            this.channel = channel;
            renderer = new BeeParticleRenderer(capi);
            swarms = new AngrySwarmClient(capi, renderer);

            channel.SetMessageHandler<BeeSpawnPacket>(OnSpawn);
            channel.SetMessageHandler<BeeCatchupPacket>(OnCatchup);
            tickListenerId = capi.Event.RegisterGameTickListener(OnTick, TickMs);
            capi.Event.LevelFinalize += RequestCatchup;
        }

        public void Dispose()
        {
            if (tickListenerId != 0) capi.Event.UnregisterGameTickListener(tickListenerId);
            tickListenerId = 0;
            capi.Event.LevelFinalize -= RequestCatchup;
            swarms.Dispose();
            bees.Clear();
            knownIds.Clear();
        }

        static bool RenderingEnabled => FGCClientConfig.Current == null || FGCClientConfig.Current.showRoamingBees;

        void RequestCatchup()
        {
            if (!RenderingEnabled) return;
            channel.SendPacket(new BeeCatchupRequestPacket());
        }

        void OnSpawn(BeeSpawnPacket packet)
        {
            if (!RenderingEnabled || packet?.Path == null || !packet.Path.IsValid) return;
            if (!knownIds.Add(packet.BeeId)) return;

            var timeline = new BeeFlightTimeline(packet.Path);
            long start = capi.World.ElapsedMilliseconds - packet.ElapsedMs;
            if (packet.ElapsedMs >= timeline.TotalSeconds * 1000f)
            {
                knownIds.Remove(packet.BeeId);
                return;   // already home
            }
            bees.Add(new ClientBee { Id = packet.BeeId, Path = packet.Path, Timeline = timeline, StartMs = start });
        }

        void OnCatchup(BeeCatchupPacket packet)
        {
            if (packet?.Bees == null) return;
            foreach (BeeSpawnPacket spawn in packet.Bees) OnSpawn(spawn);
        }

        void OnTick(float dt)
        {
            tickCounter++;
            if (!RenderingEnabled)
            {
                bees.Clear();
                knownIds.Clear();
                return;
            }

            var player = capi.World.Player?.Entity;
            if (player == null) return;

            long now = capi.World.ElapsedMilliseconds;
            if (windSampledAt < 0 || now - windSampledAt > WindSampleMs)
            {
                windSampledAt = now;
                try { wind = (float)capi.World.BlockAccessor.GetWindSpeedAt(player.Pos.XYZ).Length(); } catch { wind = 0f; }
            }

            FGCClientConfig cfg = FGCClientConfig.Current;
            float maxDist = cfg?.roamingBeesRenderDistance ?? 48;
            int budget = cfg?.roamingBeesMaxDrawn ?? 300;
            double maxDistSq = maxDist * maxDist;
            double px = player.Pos.X, py = player.Pos.Y, pz = player.Pos.Z;

            swarms.Tick(now, px, py, pz, maxDistSq, wind);
            if (bees.Count == 0) return;

            // pass 1: expire finished flights
            for (int i = bees.Count - 1; i >= 0; i--)
            {
                ClientBee bee = bees[i];
                if ((now - bee.StartMs) / 1000f >= bee.Timeline.TotalSeconds)
                {
                    knownIds.Remove(bee.Id);
                    bees.RemoveAt(i);
                }
            }

            // pass 2: cull by distance (coarse position, no wobble needed)
            candidates.Clear();
            foreach (ClientBee bee in bees)
            {
                float t = (now - bee.StartMs) / 1000f;
                bee.Timeline.PositionAt(t, pos, out _);
                double dx = pos.X - px, dy = pos.Y - py, dz = pos.Z - pz;
                double distSq = dx * dx + dy * dy + dz * dz;
                if (distSq > maxDistSq) continue;
                candidates.Add(new Candidate { Bee = bee, T = t, DistSq = distSq });
            }

            // pass 3: nearest first within the budget
            if (candidates.Count > budget)
            {
                candidates.Sort((a, b) => a.DistSq.CompareTo(b.DistSq));
                candidates.RemoveRange(budget, candidates.Count - budget);
            }

            bool dotTick = (tickCounter & 1) == 0;
            foreach (Candidate c in candidates)
            {
                EnumBeeDetail detail;
                if (c.DistSq < FullDetailDistance * FullDetailDistance) detail = EnumBeeDetail.Full;
                else if (c.DistSq < BasicDetailDistance * BasicDetailDistance) detail = EnumBeeDetail.Basic;
                else { if (!dotTick) continue; detail = EnumBeeDetail.Dot; }

                ClientBee bee = c.Bee;
                float t = c.T;
                float total = bee.Timeline.TotalSeconds;

                bee.Timeline.PositionAt(t, pos, out EnumBeeWaypointKind kind);
                BeeFlightTimeline.AddWobble(pos, t, bee.Path.Seed, wind, kind, total - t);

                const float ahead = 0.05f;
                bee.Timeline.PositionAt(t + ahead, posAhead, out EnumBeeWaypointKind kindAhead);
                BeeFlightTimeline.AddWobble(posAhead, t + ahead, bee.Path.Seed, wind, kindAhead, total - t - ahead);
                velocity.Set((posAhead.X - pos.X) / ahead, (posAhead.Y - pos.Y) / ahead, (posAhead.Z - pos.Z) / ahead);

                renderer.Draw(pos, velocity, t, detail);
            }
        }
    }
}
