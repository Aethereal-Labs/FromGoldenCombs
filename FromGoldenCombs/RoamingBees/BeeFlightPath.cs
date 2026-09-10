using ProtoBuf;
using System;
using Vintagestory.API.MathTools;

namespace FromGoldenCombs.RoamingBees
{
    /// <summary>What kind of segment ends at a waypoint. Drives speed, dwell time and easing.</summary>
    public enum EnumBeeWaypointKind : byte
    {
        Depart = 0,   // short hop out of the entrance
        Travel = 1,   // flight between plants (or to a detour waypoint)
        Hover = 2,    // small move inside a plant, followed by a short pause
        Return = 3,   // flight back towards the hive
        Land = 4      // final approach into the entrance; the bee disappears at the end
    }

    /// <summary>
    /// A complete flight, generated on the server and replayed identically on every client.
    /// Waypoints are stored relative to the hive block so they stay compact on the wire.
    /// </summary>
    [ProtoContract]
    public class BeeFlightPath
    {
        [ProtoMember(1)] public int HiveX;
        [ProtoMember(2)] public int HiveY;
        [ProtoMember(3)] public int HiveZ;
        /// <summary>x,y,z triplets relative to (HiveX, HiveY, HiveZ). Point 0 is the entrance.</summary>
        [ProtoMember(4)] public float[] Points;
        /// <summary>Kind of the segment that ends at point i. Kinds[0] is unused.</summary>
        [ProtoMember(5)] public byte[] Kinds;
        /// <summary>Seed for the deterministic dwell times and wobble phases.</summary>
        [ProtoMember(6)] public int Seed;

        public int Count => Kinds?.Length ?? 0;

        public bool IsValid => Points != null && Kinds != null && Kinds.Length >= 2 && Points.Length == Kinds.Length * 3;

        public void GetPoint(int i, Vec3d into)
        {
            int o = i * 3;
            into.Set(HiveX + Points[o], HiveY + Points[o + 1], HiveZ + Points[o + 2]);
        }
    }

    /// <summary>Tiny deterministic generator (xorshift32) so server and client agree on every derived value.</summary>
    public class BeeRandom
    {
        uint state;

        public BeeRandom(int seed)
        {
            unchecked
            {
                state = (uint)seed * 2654435761u ^ 0x9E3779B9u;
            }
            if (state == 0) state = 0x1234567;
        }

        public uint NextUInt()
        {
            uint s = state;
            s ^= s << 13;
            s ^= s >> 17;
            s ^= s << 5;
            state = s;
            return s;
        }

        /// <summary>0 (inclusive) .. 1 (exclusive)</summary>
        public float NextFloat() => (NextUInt() & 0xFFFFFF) / 16777216f;
    }

    /// <summary>
    /// Turns a path into a function of time. Both sides build this from the same path, so the
    /// server knows exactly when a bee is gone and late-joining clients can jump straight to the
    /// current position.
    /// </summary>
    public class BeeFlightTimeline
    {
        public const float DepartSpeed = 1.2f;
        public const float TravelSpeed = 2.0f;
        public const float HoverSpeed = 0.35f;
        public const float ReturnSpeed = 1.8f;
        public const float LandSpeed = 0.45f;

        readonly BeeFlightPath path;
        readonly float[] segStart;     // time the bee starts moving towards point i
        readonly float[] segMove;      // movement duration of that segment
        readonly float[] dwellAfter;   // pause after arriving at point i
        readonly float[] segDist;
        readonly Vec3d a = new Vec3d();
        readonly Vec3d b = new Vec3d();

        public float TotalSeconds { get; }

        public BeeFlightTimeline(BeeFlightPath path)
        {
            this.path = path;
            int n = path.Count;
            segStart = new float[n];
            segMove = new float[n];
            dwellAfter = new float[n];
            segDist = new float[n];

            var rng = new BeeRandom(path.Seed);
            float t = 0f;
            path.GetPoint(0, a);
            for (int i = 1; i < n; i++)
            {
                path.GetPoint(i, b);
                var kind = (EnumBeeWaypointKind)path.Kinds[i];
                float dist = (float)a.DistanceTo(b);
                float move = Math.Max(0.12f, dist / SpeedFor(kind));
                float dwell = kind == EnumBeeWaypointKind.Hover ? 0.3f + 0.6f * rng.NextFloat() : 0f;

                segStart[i] = t;
                segMove[i] = move;
                dwellAfter[i] = dwell;
                segDist[i] = dist;
                t += move + dwell;
                a.Set(b);
            }
            TotalSeconds = t;
        }

        public static float SpeedFor(EnumBeeWaypointKind kind)
        {
            return kind switch
            {
                EnumBeeWaypointKind.Depart => DepartSpeed,
                EnumBeeWaypointKind.Hover => HoverSpeed,
                EnumBeeWaypointKind.Return => ReturnSpeed,
                EnumBeeWaypointKind.Land => LandSpeed,
                _ => TravelSpeed,
            };
        }

        /// <summary>Position at time t (seconds since the bee left the entrance), without wobble.</summary>
        public void PositionAt(float t, Vec3d into, out EnumBeeWaypointKind kind)
        {
            int n = path.Count;
            if (t <= 0f || n < 2)
            {
                path.GetPoint(0, into);
                kind = EnumBeeWaypointKind.Depart;
                return;
            }

            for (int i = 1; i < n; i++)
            {
                float end = segStart[i] + segMove[i] + dwellAfter[i];
                if (t < end || i == n - 1)
                {
                    kind = (EnumBeeWaypointKind)path.Kinds[i];
                    float u = segMove[i] <= 0f ? 1f : GameMath.Clamp((t - segStart[i]) / segMove[i], 0f, 1f);
                    float e = u * u * (3f - 2f * u);   // ease in/out: accelerate, cruise, slow down at the target

                    path.GetPoint(i - 1, a);
                    path.GetPoint(i, b);

                    float arc = 0f;
                    if (kind == EnumBeeWaypointKind.Travel || kind == EnumBeeWaypointKind.Return)
                    {
                        arc = (float)Math.Sin(Math.PI * u) * Math.Min(0.25f, segDist[i] * 0.05f);
                    }

                    into.Set(a.X + (b.X - a.X) * e, a.Y + (b.Y - a.Y) * e + arc, a.Z + (b.Z - a.Z) * e);
                    return;
                }
            }

            path.GetPoint(n - 1, into);
            kind = EnumBeeWaypointKind.Land;
        }

        /// <summary>
        /// Adds a small sinusoidal wobble. Wind (0..1) increases both amplitude and frequency.
        /// The wobble fades in right after leaving the hive and fades out before landing so the
        /// bee visibly enters the entrance.
        /// </summary>
        public static void AddWobble(Vec3d pos, float t, int seed, float wind, EnumBeeWaypointKind kind, float timeLeft)
        {
            wind = GameMath.Clamp(wind, 0f, 1f);
            float amp = 0.035f + 0.11f * wind;
            if (kind == EnumBeeWaypointKind.Hover) amp *= 0.45f;

            float fadeIn = GameMath.Clamp(t / 0.6f, 0f, 1f);
            float fadeOut = GameMath.Clamp(timeLeft / 1.2f, 0f, 1f);
            amp *= Math.Min(fadeIn, fadeOut);
            if (amp <= 0f) return;

            float p1 = (seed & 0xFF) * 0.0245f;
            float p2 = ((seed >> 8) & 0xFF) * 0.0245f;
            float p3 = ((seed >> 16) & 0xFF) * 0.0245f;
            float wf = 1f + 2f * wind;

            pos.X += amp * Math.Sin(5.3f * wf * t + p1);
            pos.Y += amp * 0.6f * Math.Sin(7.9f * wf * t + p2);
            pos.Z += amp * Math.Cos(4.7f * wf * t + p3);
        }
    }
}
