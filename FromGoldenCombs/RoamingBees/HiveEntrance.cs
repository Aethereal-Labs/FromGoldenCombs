using Vintagestory.API.MathTools;

namespace FromGoldenCombs.RoamingBees
{
    /// <summary>
    /// Where bees leave and enter each hive type, in block-local coordinates, plus the direction
    /// they fly out in. A block without a facing variant (the ceramic brood pot) gets a stable
    /// pseudo-random side derived from its position.
    /// </summary>
    public static class HiveEntrance
    {
        static readonly string[] Sides = { "north", "east", "south", "west" };

        public static void Resolve(EnumRoamingHiveType type, string facing, BlockPos pos, out Vec3d entrance, out Vec3f front)
        {
            int side = SideIndex(facing);
            if (side < 0) side = StableSide(pos);

            Vec3f offset;
            switch (type)
            {
                case EnumRoamingHiveType.Ceramic:
                    offset = side switch
                    {
                        0 => new Vec3f(0.5f, 0.1f, 0.8f),
                        1 => new Vec3f(0.2f, 0.1f, 0.5f),
                        2 => new Vec3f(0.5f, 0.1f, 0.2f),
                        _ => new Vec3f(0.8f, 0.1f, 0.5f),
                    };
                    front = FlatFront(side);
                    break;

                case EnumRoamingHiveType.Langstroth:
                    offset = side switch
                    {
                        0 => new Vec3f(0.5f, 0.27f, 0.7f),
                        1 => new Vec3f(0.3f, 0.27f, 0.5f),
                        2 => new Vec3f(0.5f, 0.27f, 0.3f),
                        _ => new Vec3f(0.7f, 0.27f, 0.5f),
                    };
                    front = FlatFront(side);
                    front.Y = -0.5f;   // the Langstroth entrance sits low; bees dip out and down first
                    break;

                case EnumRoamingHiveType.Wild:
                    // hangs under a block; the body spans y 0.375..1 (large) or 0.56..1 (medium), bees leave low on a side
                    offset = side switch
                    {
                        0 => new Vec3f(0.5f, 0.45f, 0.78f),
                        1 => new Vec3f(0.22f, 0.45f, 0.5f),
                        2 => new Vec3f(0.5f, 0.45f, 0.22f),
                        _ => new Vec3f(0.78f, 0.45f, 0.5f),
                    };
                    front = FlatFront(side);
                    front.Y = -0.3f;
                    break;

                case EnumRoamingHiveType.WildLog:
                    // a hollow inside a full log block; the opening sits mid-height on a side
                    offset = side switch
                    {
                        0 => new Vec3f(0.5f, 0.5f, 0.9f),
                        1 => new Vec3f(0.1f, 0.5f, 0.5f),
                        2 => new Vec3f(0.5f, 0.5f, 0.1f),
                        _ => new Vec3f(0.9f, 0.5f, 0.5f),
                    };
                    front = FlatFront(side);
                    break;

                default: // skep
                    offset = side switch
                    {
                        0 => new Vec3f(0.5f, 0.1f, 0.6f),
                        1 => new Vec3f(0.4f, 0.1f, 0.5f),
                        2 => new Vec3f(0.5f, 0.1f, 0.4f),
                        _ => new Vec3f(0.6f, 0.1f, 0.5f),
                    };
                    front = FlatFront(side);
                    break;
            }

            entrance = new Vec3d(pos.X + offset.X, pos.Y + offset.Y, pos.Z + offset.Z);
        }

        static Vec3f FlatFront(int side)
        {
            return side switch
            {
                0 => new Vec3f(0, 0, 1),
                1 => new Vec3f(-1, 0, 0),
                2 => new Vec3f(0, 0, -1),
                _ => new Vec3f(1, 0, 0),
            };
        }

        static int SideIndex(string facing)
        {
            if (string.IsNullOrEmpty(facing)) return -1;
            for (int i = 0; i < Sides.Length; i++)
            {
                if (Sides[i] == facing) return i;
            }
            return -1;
        }

        static int StableSide(BlockPos pos)
        {
            unchecked
            {
                int h = pos.X * 73856093 ^ pos.Y * 19349663 ^ pos.Z * 83492791;
                h ^= h >> 13;
                return h & 3;
            }
        }
    }
}
