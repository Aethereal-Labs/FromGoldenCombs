using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace FromGoldenCombs.RoamingBees
{
    /// <summary>
    /// Server-side flight planning: entrance, a handful of plants with a few hover points each,
    /// back to the entrance. Every flight leg is checked for line of sight; blocked legs try a
    /// small table of detour waypoints and plants that stay unreachable are skipped.
    /// </summary>
    public class BeeFlightPlanner
    {
        public const float RayStep = 0.4f;
        public const float LateralOffset = 0.15f;
        public const int MaxPoints = 80;

        public int MinVisits = 2;
        public int MaxVisits = 6;
        public int MinHoverPoints = 3;
        public int MaxHoverPoints = 6;

        static readonly Vec3f[] DetourOffsets =
        {
            new Vec3f(0, 1.5f, 0), new Vec3f(0, 2.5f, 0),
            new Vec3f(1.5f, 1f, 0), new Vec3f(-1.5f, 1f, 0), new Vec3f(0, 1f, 1.5f), new Vec3f(0, 1f, -1.5f),
            new Vec3f(1.5f, 0, 1.5f), new Vec3f(-1.5f, 0, -1.5f), new Vec3f(1.5f, 0, -1.5f), new Vec3f(-1.5f, 0, 1.5f),
            new Vec3f(0, 3.5f, 0), new Vec3f(2.5f, 2f, 0), new Vec3f(-2.5f, 2f, 0), new Vec3f(0, 2f, 2.5f), new Vec3f(0, 2f, -2.5f),
        };

        readonly IBlockAccessor ba;
        readonly Random rand;
        readonly BlockPos scratchPos = new BlockPos(0);
        readonly Vec3d scratchA = new Vec3d();
        readonly Vec3d scratchB = new Vec3d();

        public BeeFlightPlanner(IBlockAccessor blockAccessor, Random rand)
        {
            ba = blockAccessor;
            this.rand = rand;
        }

        /// <summary>
        /// Returns null when no flight could be planned (no reachable plants and scouting disabled).
        /// </summary>
        public BeeFlightPath Plan(BlockPos hivePos, Vec3d entrance, Vec3f front, List<BlockPos> plants, float radius, bool allowScout)
        {
            var pts = new List<Vec3d>(32);
            var kinds = new List<EnumBeeWaypointKind>(32);

            pts.Add(entrance.Clone());
            kinds.Add(EnumBeeWaypointKind.Depart);

            Vec3d depart = new Vec3d(entrance.X + front.X * 0.6, entrance.Y + front.Y * 0.6 + 0.12, entrance.Z + front.Z * 0.6);
            pts.Add(depart);
            kinds.Add(EnumBeeWaypointKind.Depart);

            Vec3d cur = depart;
            int visited = 0;

            int visits = rand.Next(Math.Max(1, MinVisits), Math.Max(MinVisits, MaxVisits) + 1);
            List<BlockPos> route = PickRoute(plants, visits, depart);

            foreach (BlockPos plant in route)
            {
                if (pts.Count > MaxPoints - 12) break;

                Block block = ba.GetBlock(plant);
                if (!ForageRegistry.IsForagePlant(block, true)) continue;

                List<Vec3d> hover = HoverPoints(block, plant, rand.Next(MinHoverPoints, Math.Max(MinHoverPoints, MaxHoverPoints) + 1));
                if (hover.Count == 0) continue;

                if (!TryAppendClearLeg(pts, kinds, cur, hover[0], EnumBeeWaypointKind.Travel, hivePos)) continue;

                for (int i = 1; i < hover.Count; i++)
                {
                    pts.Add(hover[i]);
                    kinds.Add(EnumBeeWaypointKind.Hover);
                }
                cur = hover[hover.Count - 1];
                visited++;
            }

            if (visited == 0)
            {
                if (!allowScout) return null;
                cur = AppendScoutingLegs(pts, kinds, cur, hivePos, radius);
                if (cur == null) return null;
            }

            // Home: back to the spot in front of the entrance, then into it.
            if (!TryAppendClearLeg(pts, kinds, cur, depart, EnumBeeWaypointKind.Return, hivePos))
            {
                pts.Add(depart);
                kinds.Add(EnumBeeWaypointKind.Return);
            }
            pts.Add(entrance.Clone());
            kinds.Add(EnumBeeWaypointKind.Land);

            return Pack(hivePos, pts, kinds);
        }

        BeeFlightPath Pack(BlockPos hivePos, List<Vec3d> pts, List<EnumBeeWaypointKind> kinds)
        {
            var path = new BeeFlightPath
            {
                HiveX = hivePos.X,
                HiveY = hivePos.Y,
                HiveZ = hivePos.Z,
                Points = new float[pts.Count * 3],
                Kinds = new byte[kinds.Count],
                Seed = rand.Next(),
            };
            for (int i = 0; i < pts.Count; i++)
            {
                path.Points[i * 3] = (float)(pts[i].X - hivePos.X);
                path.Points[i * 3 + 1] = (float)(pts[i].Y - hivePos.Y);
                path.Points[i * 3 + 2] = (float)(pts[i].Z - hivePos.Z);
                path.Kinds[i] = (byte)kinds[i];
            }
            return path;
        }

        /// <summary>Random subset of the plants, ordered nearest-neighbour from the start point so the route makes sense.</summary>
        List<BlockPos> PickRoute(List<BlockPos> plants, int visits, Vec3d start)
        {
            var route = new List<BlockPos>(visits);
            if (plants == null || plants.Count == 0) return route;

            var pool = new List<BlockPos>(plants);
            // partial shuffle: take 'visits' random distinct entries
            int take = Math.Min(visits, pool.Count);
            for (int i = 0; i < take; i++)
            {
                int j = i + rand.Next(pool.Count - i);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            var chosen = pool.GetRange(0, take);

            double cx = start.X, cy = start.Y, cz = start.Z;
            while (chosen.Count > 0)
            {
                int best = 0;
                double bestDist = double.MaxValue;
                for (int i = 0; i < chosen.Count; i++)
                {
                    double dx = chosen[i].X + 0.5 - cx, dy = chosen[i].Y + 0.5 - cy, dz = chosen[i].Z + 0.5 - cz;
                    double d = dx * dx + dy * dy + dz * dz;
                    if (d < bestDist) { bestDist = d; best = i; }
                }
                BlockPos next = chosen[best];
                chosen.RemoveAt(best);
                route.Add(next);
                cx = next.X + 0.5; cy = next.Y + 0.5; cz = next.Z + 0.5;
            }
            return route;
        }

        /// <summary>Points inside the plant's selection box(es), biased towards the upper part where the flowers are.</summary>
        List<Vec3d> HoverPoints(Block block, BlockPos pos, int count)
        {
            var result = new List<Vec3d>(count);
            Cuboidf[] boxes = null;
            try { boxes = block.GetSelectionBoxes(ba, pos); } catch { /* fall through to the unit cube */ }
            if (boxes == null || boxes.Length == 0) boxes = new[] { new Cuboidf(0.2f, 0f, 0.2f, 0.8f, 0.8f, 0.8f) };

            for (int i = 0; i < count; i++)
            {
                Cuboidf box = boxes[rand.Next(boxes.Length)];
                float w = Math.Max(0.05f, box.X2 - box.X1);
                float h = Math.Max(0.05f, box.Y2 - box.Y1);
                float d = Math.Max(0.05f, box.Z2 - box.Z1);
                float x = GameMath.Clamp(box.X1 + (float)rand.NextDouble() * w, 0.1f, 0.9f);
                float z = GameMath.Clamp(box.Z1 + (float)rand.NextDouble() * d, 0.1f, 0.9f);
                float y = box.Y1 + h * (0.55f + 0.45f * (float)rand.NextDouble()) + 0.05f + 0.15f * (float)rand.NextDouble();
                result.Add(new Vec3d(pos.X + x, pos.Y + y, pos.Z + z));
            }
            return result;
        }

        /// <summary>No flowers in range: fly to a couple of random open spots and look around.</summary>
        Vec3d AppendScoutingLegs(List<Vec3d> pts, List<EnumBeeWaypointKind> kinds, Vec3d cur, BlockPos hivePos, float radius)
        {
            int legs = 2 + rand.Next(2);
            int made = 0;
            for (int leg = 0; leg < legs; leg++)
            {
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    double angle = rand.NextDouble() * Math.PI * 2;
                    double dist = 3 + rand.NextDouble() * Math.Max(1f, radius - 3);
                    Vec3d target = new Vec3d(
                        hivePos.X + 0.5 + Math.Cos(angle) * dist,
                        hivePos.Y + 0.5 + rand.NextDouble() * 3 - 0.5,
                        hivePos.Z + 0.5 + Math.Sin(angle) * dist);

                    if (PointBlocked(target, hivePos)) continue;
                    if (!TryAppendClearLeg(pts, kinds, cur, target, EnumBeeWaypointKind.Travel, hivePos)) continue;

                    int hovers = 1 + rand.Next(2);
                    for (int i = 0; i < hovers; i++)
                    {
                        Vec3d h = new Vec3d(target.X + rand.NextDouble() * 0.8 - 0.4, target.Y + rand.NextDouble() * 0.5 - 0.25, target.Z + rand.NextDouble() * 0.8 - 0.4);
                        pts.Add(h);
                        kinds.Add(EnumBeeWaypointKind.Hover);
                        target = h;
                    }
                    cur = target;
                    made++;
                    break;
                }
            }
            return made > 0 ? cur : null;
        }

        /// <summary>
        /// Appends 'to' (kind) if the straight line is clear, otherwise tries detour waypoints around
        /// the midpoint, the start and the end. Returns false when no clear route was found.
        /// </summary>
        bool TryAppendClearLeg(List<Vec3d> pts, List<EnumBeeWaypointKind> kinds, Vec3d from, Vec3d to, EnumBeeWaypointKind kind, BlockPos hivePos)
        {
            if (IsClear(from, to, hivePos))
            {
                pts.Add(to);
                kinds.Add(kind);
                return true;
            }

            Vec3d mid = new Vec3d((from.X + to.X) / 2, (from.Y + to.Y) / 2, (from.Z + to.Z) / 2);
            Vec3d[] anchors = { mid, from, to };
            Vec3d via = new Vec3d();

            foreach (Vec3d anchor in anchors)
            {
                foreach (Vec3f off in DetourOffsets)
                {
                    via.Set(anchor.X + off.X, anchor.Y + off.Y, anchor.Z + off.Z);
                    if (PointBlocked(via, hivePos)) continue;
                    if (IsClear(from, via, hivePos) && IsClear(via, to, hivePos))
                    {
                        pts.Add(via.Clone());
                        kinds.Add(kind == EnumBeeWaypointKind.Return ? EnumBeeWaypointKind.Return : EnumBeeWaypointKind.Travel);
                        pts.Add(to);
                        kinds.Add(kind);
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Steps along the segment (RayStep) and checks the centre line plus two lateral offsets
        /// against block collision boxes. Endpoints are not tested: they sit inside flowers or the hive.
        /// </summary>
        bool IsClear(Vec3d from, Vec3d to, BlockPos hivePos)
        {
            double dx = to.X - from.X, dy = to.Y - from.Y, dz = to.Z - from.Z;
            double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (len < 0.01) return true;

            // horizontal perpendicular for the lateral rays
            double px = -dz, pz = dx;
            double plen = Math.Sqrt(px * px + pz * pz);
            if (plen < 1e-6) { px = 1; pz = 0; plen = 1; }
            px = px / plen * LateralOffset;
            pz = pz / plen * LateralOffset;

            int steps = Math.Max(2, (int)Math.Ceiling(len / RayStep));
            for (int s = 1; s < steps; s++)
            {
                double f = (double)s / steps;
                scratchA.Set(from.X + dx * f, from.Y + dy * f, from.Z + dz * f);
                if (PointBlocked(scratchA, hivePos)) return false;
                scratchB.Set(scratchA.X + px, scratchA.Y, scratchA.Z + pz);
                if (PointBlocked(scratchB, hivePos)) return false;
                scratchB.Set(scratchA.X - px, scratchA.Y, scratchA.Z - pz);
                if (PointBlocked(scratchB, hivePos)) return false;
            }
            return true;
        }

        bool PointBlocked(Vec3d p, BlockPos hivePos)
        {
            int bx = (int)Math.Floor(p.X), by = (int)Math.Floor(p.Y), bz = (int)Math.Floor(p.Z);
            if (bx == hivePos.X && by == hivePos.Y && bz == hivePos.Z) return false;

            scratchPos.Set(bx, by, bz);
            Block block = ba.GetBlock(scratchPos);
            if (block == null || block.Id == 0) return false;

            Cuboidf[] boxes;
            try { boxes = block.GetCollisionBoxes(ba, scratchPos); } catch { return true; }
            if (boxes == null || boxes.Length == 0) return false;

            float lx = (float)(p.X - bx), ly = (float)(p.Y - by), lz = (float)(p.Z - bz);
            foreach (Cuboidf box in boxes)
            {
                if (box == null) continue;
                if (lx >= box.X1 && lx <= box.X2 && ly >= box.Y1 && ly <= box.Y2 && lz >= box.Z1 && lz <= box.Z2) return true;
            }
            return false;
        }
    }
}
