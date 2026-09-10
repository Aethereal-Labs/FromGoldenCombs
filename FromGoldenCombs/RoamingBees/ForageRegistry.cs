using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace FromGoldenCombs.RoamingBees
{
    /// <summary>
    /// Server-side index of flower and crop positions around registered hives.
    ///
    /// Each hive registers a scan area (a sphere). Areas are scanned incrementally, a bounded
    /// number of blocks per tick, and rescanned every few minutes. Player block changes inside a
    /// watched area are applied immediately. Positions live in 16-block grid cells so radius
    /// queries only touch nearby cells.
    /// </summary>
    public class ForageRegistry
    {
        public const int CellShift = 4;   // 16-block cells
        public int BlocksPerTick = 1024;   // ~5000 blocks/s at a 200 ms tick: a radius-10 sphere scans in about 2 s
        public int RescanIntervalMs = 180_000;
        public int InitialScanDelayMs = 8_000;
        public bool IncludeCrops = true;

        class Area
        {
            public BlockPos Hive;
            public int Radius;
            public HashSet<BlockPos> Found = new HashSet<BlockPos>();
            public long NextScanAt;
            public bool Queued;
        }

        class ScanJob
        {
            public Area Area;
            public int X, Y, Z;    // relative cursor, each in [-r, r]
            public HashSet<BlockPos> Result = new HashSet<BlockPos>();
        }

        readonly ICoreServerAPI sapi;
        readonly Dictionary<BlockPos, Area> areas = new Dictionary<BlockPos, Area>();
        readonly Dictionary<long, HashSet<BlockPos>> cells = new Dictionary<long, HashSet<BlockPos>>();
        readonly Dictionary<long, int> watchedCells = new Dictionary<long, int>();
        readonly Queue<Area> queue = new Queue<Area>();
        ScanJob current;
        readonly BlockPos scratch = new BlockPos(0);

        public int AreaCount => areas.Count;
        public int PlantCount { get { int n = 0; foreach (var c in cells.Values) n += c.Count; return n; } }

        public ForageRegistry(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
        }

        public static bool IsForagePlant(Block block, bool includeCrops)
        {
            if (block == null || block.Id == 0) return false;
            if (block.Attributes != null && block.Attributes.IsTrue("beeFeed")) return true;
            return includeCrops && block is BlockCrop;
        }

        static long CellKey(int x, int y, int z)
        {
            long cx = (x >> CellShift) & 0x1FFFFF;
            long cy = (y >> CellShift) & 0x1FFFFF;
            long cz = (z >> CellShift) & 0x1FFFFF;
            return (cx << 42) | (cy << 21) | cz;
        }

        public void Register(BlockPos hivePos, int radius)
        {
            radius = Math.Max(2, Math.Min(radius, 40));
            if (areas.TryGetValue(hivePos, out Area existing))
            {
                if (existing.Radius == radius) return;
                Unregister(hivePos);
            }

            var area = new Area
            {
                Hive = hivePos.Copy(),
                Radius = radius,
                NextScanAt = sapi.World.ElapsedMilliseconds + InitialScanDelayMs + sapi.World.Rand.Next(2000),
            };
            areas[area.Hive] = area;
            ForEachCellOf(area, key => { watchedCells.TryGetValue(key, out int n); watchedCells[key] = n + 1; });
        }

        public void Unregister(BlockPos hivePos)
        {
            if (!areas.TryGetValue(hivePos, out Area area)) return;
            areas.Remove(hivePos);
            if (current != null && current.Area == area) current = null;

            foreach (BlockPos pos in area.Found)
            {
                if (!AnyAreaContains(pos, area)) RemoveFromCells(pos);
            }
            ForEachCellOf(area, key =>
            {
                if (watchedCells.TryGetValue(key, out int n))
                {
                    if (n <= 1) watchedCells.Remove(key); else watchedCells[key] = n - 1;
                }
            });
        }

        /// <summary>Called every server tick of the bee manager. Does a bounded amount of scanning.</summary>
        public void Tick(long nowMs)
        {
            if (current == null)
            {
                // queue due rescans
                foreach (Area area in areas.Values)
                {
                    if (!area.Queued && nowMs >= area.NextScanAt)
                    {
                        area.Queued = true;
                        queue.Enqueue(area);
                    }
                }
                while (queue.Count > 0 && current == null)
                {
                    Area next = queue.Dequeue();
                    if (!areas.ContainsKey(next.Hive)) continue;   // unregistered meanwhile
                    current = new ScanJob { Area = next, X = -next.Radius, Y = -next.Radius, Z = -next.Radius };
                }
                if (current == null) return;
            }

            ScanJob job = current;
            Area a = job.Area;
            int r = a.Radius;
            int r2 = r * r;
            IBlockAccessor ba = sapi.World.BlockAccessor;
            int budget = BlocksPerTick;

            while (budget-- > 0)
            {
                if (job.X * job.X + job.Y * job.Y + job.Z * job.Z <= r2)
                {
                    scratch.Set(a.Hive.X + job.X, a.Hive.Y + job.Y, a.Hive.Z + job.Z);
                    Block block = ba.GetBlock(scratch);
                    if (IsForagePlant(block, IncludeCrops))
                    {
                        job.Result.Add(scratch.Copy());
                    }
                }

                // advance cursor
                if (++job.Z > r)
                {
                    job.Z = -r;
                    if (++job.X > r)
                    {
                        job.X = -r;
                        if (++job.Y > r)
                        {
                            CompleteScan(job, nowMs);
                            current = null;
                            return;
                        }
                    }
                }
            }
        }

        void CompleteScan(ScanJob job, long nowMs)
        {
            Area area = job.Area;
            foreach (BlockPos old in area.Found)
            {
                if (!job.Result.Contains(old) && !AnyAreaContains(old, area)) RemoveFromCells(old);
            }
            foreach (BlockPos pos in job.Result)
            {
                AddToCells(pos);
            }
            area.Found = job.Result;
            area.Queued = false;
            area.NextScanAt = nowMs + RescanIntervalMs + sapi.World.Rand.Next(20_000);
        }

        /// <summary>Player placed or broke a block: update the index if the position is inside a watched area.</summary>
        public void OnBlockChanged(BlockPos pos)
        {
            if (pos == null) return;
            if (!watchedCells.ContainsKey(CellKey(pos.X, pos.Y, pos.Z))) return;

            Block block = sapi.World.BlockAccessor.GetBlock(pos);
            bool plant = IsForagePlant(block, IncludeCrops);
            bool inAnyArea = false;

            foreach (Area area in areas.Values)
            {
                int dx = pos.X - area.Hive.X, dy = pos.Y - area.Hive.Y, dz = pos.Z - area.Hive.Z;
                if (dx * dx + dy * dy + dz * dz > area.Radius * area.Radius) continue;
                inAnyArea = true;
                if (plant) area.Found.Add(pos.Copy()); else area.Found.Remove(pos);
            }

            if (!inAnyArea) return;
            if (plant) AddToCells(pos.Copy()); else RemoveFromCells(pos);
        }

        /// <summary>All indexed plant positions within 'radius' of 'center'.</summary>
        public void Query(Vec3d center, double radius, List<BlockPos> result)
        {
            double r2 = radius * radius;
            int minX = (int)Math.Floor(center.X - radius) >> CellShift, maxX = (int)Math.Floor(center.X + radius) >> CellShift;
            int minY = (int)Math.Floor(center.Y - radius) >> CellShift, maxY = (int)Math.Floor(center.Y + radius) >> CellShift;
            int minZ = (int)Math.Floor(center.Z - radius) >> CellShift, maxZ = (int)Math.Floor(center.Z + radius) >> CellShift;

            for (int cx = minX; cx <= maxX; cx++)
            {
                for (int cy = minY; cy <= maxY; cy++)
                {
                    for (int cz = minZ; cz <= maxZ; cz++)
                    {
                        if (!cells.TryGetValue(CellKey(cx << CellShift, cy << CellShift, cz << CellShift), out HashSet<BlockPos> set)) continue;
                        foreach (BlockPos p in set)
                        {
                            double dx = p.X + 0.5 - center.X, dy = p.Y + 0.5 - center.Y, dz = p.Z + 0.5 - center.Z;
                            if (dx * dx + dy * dy + dz * dz <= r2) result.Add(p);
                        }
                    }
                }
            }
        }

        /// <summary>Drop a position that turned out not to be a plant any more.</summary>
        public void Forget(BlockPos pos)
        {
            foreach (Area area in areas.Values) area.Found.Remove(pos);
            RemoveFromCells(pos);
        }

        bool AnyAreaContains(BlockPos pos, Area except)
        {
            foreach (Area other in areas.Values)
            {
                if (other != except && other.Found.Contains(pos)) return true;
            }
            return false;
        }

        void AddToCells(BlockPos pos)
        {
            long key = CellKey(pos.X, pos.Y, pos.Z);
            if (!cells.TryGetValue(key, out HashSet<BlockPos> set))
            {
                set = new HashSet<BlockPos>();
                cells[key] = set;
            }
            set.Add(pos);
        }

        void RemoveFromCells(BlockPos pos)
        {
            long key = CellKey(pos.X, pos.Y, pos.Z);
            if (cells.TryGetValue(key, out HashSet<BlockPos> set))
            {
                set.Remove(pos);
                if (set.Count == 0) cells.Remove(key);
            }
        }

        void ForEachCellOf(Area area, Action<long> action)
        {
            int r = area.Radius;
            int minX = (area.Hive.X - r) >> CellShift, maxX = (area.Hive.X + r) >> CellShift;
            int minY = (area.Hive.Y - r) >> CellShift, maxY = (area.Hive.Y + r) >> CellShift;
            int minZ = (area.Hive.Z - r) >> CellShift, maxZ = (area.Hive.Z + r) >> CellShift;
            for (int cx = minX; cx <= maxX; cx++)
                for (int cy = minY; cy <= maxY; cy++)
                    for (int cz = minZ; cz <= maxZ; cz++)
                        action(CellKey(cx << CellShift, cy << CellShift, cz << CellShift));
        }
    }
}
