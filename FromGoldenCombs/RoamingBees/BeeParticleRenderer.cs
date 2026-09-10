using System;
using FromGoldenCombs.Util.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace FromGoldenCombs.RoamingBees
{
    /// <summary>How much of a bee to draw; picked by distance so far-away swarms stay cheap.</summary>
    public enum EnumBeeDetail
    {
        Dot,     // body only, drawn every other tick
        Basic,   // body, head, tail
        Full     // plus wings, and eyes/antennae when enabled
    }

    /// <summary>
    /// Draws a bee as a handful of very short-lived cube particles re-spawned every client tick.
    /// The particles inherit the bee's velocity so they keep moving smoothly between ticks.
    ///
    /// Dimensions follow the look of the Roaming Bees mod: a 0.5-size yellow body cube (the engine
    /// cube particle is 1/32 block per size unit, so about 1.5 cm), black head and tail cubes of the
    /// same size shifted 1/64 block forward and back, two pale translucent wings that flick between
    /// a raised and a swept-back position 16 times a second, and optional eyes and antennae.
    ///
    /// Cost per bee per 20 ms tick: Full 5 (+12 with eyes/antennae), Basic 3, Dot 0.5 particles.
    /// The client's main-thread cube pool holds 4000 particles by default.
    /// </summary>
    public class BeeParticleRenderer
    {
        public const float Life = 0.03f;          // client tick is 20 ms; a little overlap hides frame hitches
        public const float DotLife = 0.06f;       // dots are drawn every other tick
        public const float WingFlapHz = 16f;

        public const float BodySize = 0.5f;
        public const float WingSize = 0.28f;
        public const float EyeSize = 0.18f;
        public const float AntennaSize = 0.12f;
        public const int AntennaSegments = 5;

        /// <summary>One "unit" of offset: the body size expressed in blocks (size / 32).</summary>
        const double Unit = BodySize / 32.0;

        readonly ICoreClientAPI capi;
        readonly SimpleParticleProperties body;
        readonly SimpleParticleProperties bodyDot;
        readonly SimpleParticleProperties dark;
        readonly SimpleParticleProperties wing;
        readonly SimpleParticleProperties eye;
        readonly SimpleParticleProperties antenna;
        readonly Random rand = new Random();
        readonly Vec3f vel = new Vec3f();
        double dirX = 1, dirZ = 0;   // horizontal heading, kept from the last tick when the bee is (nearly) still

        public BeeParticleRenderer(ICoreClientAPI capi)
        {
            this.capi = capi;
            int yellow = ColorUtil.ToRgba(255, 180, 160, 0);
            body = Make(yellow, BodySize, Life);
            bodyDot = Make(yellow, BodySize, DotLife);
            dark = Make(ColorUtil.ToRgba(255, 0, 0, 0), BodySize, Life);
            wing = Make(ColorUtil.ToRgba(50, 255, 255, 100), WingSize, Life);
            eye = Make(ColorUtil.ToRgba(255, 51, 51, 51), EyeSize, Life);
            antenna = Make(ColorUtil.ToRgba(255, 51, 51, 51), AntennaSize, Life);
        }

        static SimpleParticleProperties Make(int color, float size, float life)
        {
            return new SimpleParticleProperties(1, 1, color, new Vec3d(), new Vec3d(), new Vec3f(), new Vec3f(), life, 0f, size, size, EnumParticleModel.Cube)
            {
                WithTerrainCollision = false,
                ShouldDieInLiquid = false,
                SelfPropelled = false,
                WindAffected = false,
            };
        }

        public void Draw(Vec3d pos, Vec3d velocity, float t, EnumBeeDetail detail)
        {
            vel.Set((float)velocity.X, (float)velocity.Y, (float)velocity.Z);

            if (detail == EnumBeeDetail.Dot)
            {
                Spawn(bodyDot, pos.X, pos.Y, pos.Z);
                return;
            }

            // heading: horizontal part of the velocity
            double hx = velocity.X, hz = velocity.Z;
            double hl = Math.Sqrt(hx * hx + hz * hz);
            if (hl > 0.02) { dirX = hx / hl; dirZ = hz / hl; }
            double sideX = -dirZ, sideZ = dirX;   // left of the heading

            Spawn(body, pos.X, pos.Y, pos.Z);
            Spawn(dark, pos.X + dirX * Unit, pos.Y + 0.5 * Unit, pos.Z + dirZ * Unit);    // head, slightly up
            Spawn(dark, pos.X - dirX * Unit, pos.Y - 0.5 * Unit, pos.Z - dirZ * Unit);    // tail, slightly down

            if (detail == EnumBeeDetail.Basic) return;

            // wings: raised and swept back while "flapping", forward and flat at "rest", 16 flips a second
            bool flap = ((int)(t * WingFlapHz)) % 2 == 0;
            double wingForward = flap ? -2.0 * Unit : -1.0 * Unit;
            double wingUp = flap ? 0.0 : 1.0 * Unit;
            for (int s = -1; s <= 1; s += 2)
            {
                double jx = (rand.NextDouble() - 0.5) * 0.005, jz = (rand.NextDouble() - 0.5) * 0.005;
                Spawn(wing,
                    pos.X + s * sideX * Unit + dirX * wingForward + jx,
                    pos.Y + wingUp,
                    pos.Z + s * sideZ * Unit + dirZ * wingForward + jz);
            }

            if (FGCClientConfig.Current?.roamingBeesEyesAndAntennae != true) return;

            double eyeFx = dirX * 1.7 * Unit, eyeFz = dirZ * 1.7 * Unit;
            for (int s = -1; s <= 1; s += 2)
            {
                Spawn(eye, pos.X + eyeFx + s * sideX * 0.5 * Unit, pos.Y + 0.6 * Unit, pos.Z + eyeFz + s * sideZ * 0.5 * Unit);
            }

            double antFx = dirX * 1.5 * Unit, antFz = dirZ * 1.5 * Unit;
            for (int i = 0; i < AntennaSegments; i++)
            {
                double up = (0.9 + 0.3 * i) * Unit;
                for (int s = -1; s <= 1; s += 2)
                {
                    Spawn(antenna, pos.X + antFx + s * sideX * 0.35 * Unit, pos.Y + up, pos.Z + antFz + s * sideZ * 0.35 * Unit);
                }
            }
        }

        void Spawn(SimpleParticleProperties props, double x, double y, double z)
        {
            props.MinPos.Set(x, y, z);
            props.MinVelocity = vel;
            capi.World.SpawnParticles(props);
        }
    }
}
