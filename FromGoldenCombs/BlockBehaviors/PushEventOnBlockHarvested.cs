using FromGoldenCombs.Util.Config;
using Newtonsoft.Json.Linq;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace FromGoldenCombs.BlockBehaviors
{
    internal class PushEventOnBlockHarvested : BlockBehavior
    {
        public PushEventOnBlockHarvested(Block block) : base(block)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            this._eventName = properties["eventname"].ToString();
            this._beeChanceMultiplier = FGCServerConfig.Current.cropBoostPercentage;
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            return true;
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handled)
        {
            return true;
        }
        
        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handled)
        {
            if (world.Side.IsClient()) return;
            handled = EnumHandling.Handled;
            BlockBehaviorHarvestable blockBehaviorHarvestable = this.block.GetBehavior<BlockBehaviorHarvestable>();
            String properties2 = blockBehaviorHarvestable.propertiesAtString;
            JObject jsonProperties2 = JObject.Parse(properties2);
            float harvestTime = jsonProperties2["harvestTime"].ToObject<float>();

            if (blockBehaviorHarvestable != null && secondsUsed > harvestTime 
                && blockBehaviorHarvestable.harvestedStacks != null && world.Side == EnumAppSide.Server)
            {
                float dropRate = 0f;
                JsonObject attributes = this.block.Attributes;
                if (attributes != null && attributes.IsTrue("forageStatAffected"))
                {
                    TreeAttribute tree = new TreeAttribute();
                    tree.SetInt("x", blockSel.Position.X);
                    tree.SetInt("y", blockSel.Position.Y);
                    tree.SetInt("z", blockSel.Position.Z);
                    world.Api.Event.PushEvent(this._eventName, tree);
                    
                    if (useBeeBoost) {
                        dropRate += _beeChanceMultiplier; 
                    }
                }
                if (useBeeBoost)
                {
                    blockBehaviorHarvestable.harvestedStacks.Foreach(delegate (BlockDropItemStack harvestedStack)
                    {
                        ItemStack stack = harvestedStack.GetNextItemStack(dropRate);
                        if (stack == null)
                        {
                            return;
                        }
                        ItemStack origStack = stack.Clone();
                        int quantity = stack.StackSize;
                        if (!byPlayer.InventoryManager.TryGiveItemstack(stack, false))
                        {
                            world.SpawnItemEntity(stack, blockSel.Position, null);
                        }
                    });
                }
                useBeeBoost = false;
                world.PlaySoundAt(blockBehaviorHarvestable.harvestingSound, blockSel.Position, 0.0, byPlayer, true, 32f, 1f);
            }
        }

        private string _eventName;
        private float _beeChanceMultiplier;
        public bool useBeeBoost = false;
    }
}
