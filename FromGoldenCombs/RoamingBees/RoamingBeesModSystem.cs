using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FromGoldenCombs.RoamingBees
{
    /// <summary>
    /// Wires the roaming-bee feature into the game: one network channel, a server manager and a
    /// client replayer. Hive block entities call RegisterHive / UnregisterHive.
    /// </summary>
    public class RoamingBeesModSystem : ModSystem
    {
        public const string ChannelName = "fromgoldencombs-roamingbees";

        public RoamingBeeServer Server { get; private set; }
        public RoamingBeeClient Client { get; private set; }

        public override bool ShouldLoad(EnumAppSide forSide) => true;

        // after the main FromGoldenCombs system so the configs exist
        public override double ExecuteOrder() => 0.2;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.Network.RegisterChannel(ChannelName)
                .RegisterMessageType(typeof(BeeSpawnPacket))
                .RegisterMessageType(typeof(BeeCatchupRequestPacket))
                .RegisterMessageType(typeof(BeeCatchupPacket));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            Server = new RoamingBeeServer(api, api.Network.GetChannel(ChannelName));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            Client = new RoamingBeeClient(api, api.Network.GetChannel(ChannelName));
        }

        public override void Dispose()
        {
            Server?.Dispose();
            Client?.Dispose();
            Server = null;
            Client = null;
            base.Dispose();
        }

        /// <summary>Server only. Safe to call from any hive block entity's Initialize.</summary>
        public static void RegisterHive(ICoreAPI api, IRoamingBeeHive hive)
        {
            if (api == null || api.Side != EnumAppSide.Server) return;
            api.ModLoader.GetModSystem<RoamingBeesModSystem>()?.Server?.Register(hive);
        }

        /// <summary>Server only. Call from OnBlockRemoved and OnBlockUnloaded.</summary>
        public static void UnregisterHive(ICoreAPI api, BlockPos pos)
        {
            if (api == null || api.Side != EnumAppSide.Server) return;
            api.ModLoader.GetModSystem<RoamingBeesModSystem>()?.Server?.Unregister(pos);
        }
    }
}
