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
            Block bushBlock = world.BlockAccessor.GetBlock(blockSel.Position);
            float harvestTime = 0.5f;
            BlockEntity be = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            BEBehaviorFruitingBush bebfb= be.GetBehavior<BEBehaviorFruitingBush>();
            BlockBehaviorFruitingBush bbfb = bushBlock.GetBehavior<BlockBehaviorFruitingBush>();
            handled = EnumHandling.Handled;
            float harvestMul = 1f;

            if (bebfb != null)
            {
                
                if (bebfb.BState.Traits.Contains("weakclusteredberries"))
                {
                    harvestMul = 1.35f;
                }
                if (bebfb.BState.Traits.Contains("strongclusteredberries"))
                {
                    harvestMul = 0.65f;
                }
                float HarvestDuration = bebfb.GetHarvestDuration(byPlayer.InventoryManager.ActiveHotbarSlot, byPlayer.Entity);
                harvestTime = bbfb.harvestTime;
            }
            if (secondsUsed > harvestTime)
            {
                float dropRate = 0f;
                JsonObject attributes = this.block.Attributes;
                if (attributes != null && (attributes.IsTrue("forageStatAffected") || bebfb?.BState?.WildBushState == null))
                {
                    TreeAttribute tree = new();
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
                    if (bbfb != null)
                    {
                        bbfb.harvestedStacks.Foreach(delegate (BlockDropItemStack harvestedStack)
                        {
                            ItemStack stack = harvestedStack.GetNextItemStack(dropRate*harvestMul).Clone();
                            if (stack == null)
                            {
                                return;
                            }
                            if (!byPlayer.InventoryManager.TryGiveItemstack(stack, false))
                            {
                                world.SpawnItemEntity(stack, blockSel.Position, null);
                            }
                        });
                    }
                    else
                    {
                        block.GetDrops(byPlayer.Entity.World, blockSel.Position, byPlayer).Foreach<ItemStack>(delegate (ItemStack harvestedStack)
                        {
                            ItemStack stack = harvestedStack;
                            if (stack == null)
                            {
                                return;
                            }
                            ItemStack newStack = stack.Clone();
                            newStack.StackSize = Math.Max(1, (int)(newStack.StackSize * dropRate));
                            if (!byPlayer.InventoryManager.TryGiveItemstack(newStack, false))
                            {
                                world.SpawnItemEntity(newStack, blockSel.Position, null);
                            }
                        });
                    }
                }
                useBeeBoost = false;
                _beeChanceMultiplier = 0;
                
            }
        }

        

        private string _eventName;
        private float _beeChanceMultiplier;
        public float beeChanceMultiplier { get => _beeChanceMultiplier; set => _beeChanceMultiplier = value; }

        public bool useBeeBoost = false;
    }
}
