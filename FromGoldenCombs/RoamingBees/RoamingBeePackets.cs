using ProtoBuf;

namespace FromGoldenCombs.RoamingBees
{
    /// <summary>Server -> client: a bee left its hive. ElapsedMs is non-zero for catch-up replays.</summary>
    [ProtoContract]
    public class BeeSpawnPacket
    {
        [ProtoMember(1)] public int BeeId;
        [ProtoMember(2)] public BeeFlightPath Path;
        [ProtoMember(3)] public int ElapsedMs;
    }

    /// <summary>Client -> server: send me the bees that are currently in the air near me.</summary>
    [ProtoContract]
    public class BeeCatchupRequestPacket
    {
        [ProtoMember(1)] public int Marker = 1;
    }

    /// <summary>Server -> client: answer to a catch-up request.</summary>
    [ProtoContract]
    public class BeeCatchupPacket
    {
        [ProtoMember(1)] public BeeSpawnPacket[] Bees;
    }
}
