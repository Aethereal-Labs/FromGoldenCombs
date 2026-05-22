using FromGoldenCombs.BlockEntities;
using FromGoldenCombs.Util.Config;
using System;
using System.Collections.Generic;
using System.Globalization;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace FromGoldenCombs.Blocks.ClaypotHive
{

    class CeramicBroodPot : BlockContainer
    {

        public override EnumItemStorageFlags GetStorageFlags(ItemStack itemstack)
        {

            if (FGCServerConfig.Current.backpackSlotOnly)
            {
                return (EnumItemStorageFlags)2;
            }
            if (!FGCServerConfig.Current.backpackSlotOnly)
            {
                if (!itemstack.Attributes.GetBool("isactivehive"))
                {
                    return (EnumItemStorageFlags)1;
                }
                return (EnumItemStorageFlags)2;
            }
            return base.GetStorageFlags(itemstack);
        }

        /// <summary>
        /// When a player does a right click while targeting this placed block. Should return true if the event is handled, so that other events can occur, e.g. eating a held item if the block is not interactable with.
        /// </summary>
        /// <param name="world"></param>
        /// <param name="byPlayer"></param>
        /// <param name="blockSel"></param>
        /// <returns>False if the interaction should be stopped. True if the interaction should continue. If you return false, the interaction will not be synced to the server.</returns>
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {                      
            BECeramicBroodPot beCeramicBroodPot = (BECeramicBroodPot)world.BlockAccessor.GetBlockEntity(blockSel.Position);
            return beCeramicBroodPot is not null ? beCeramicBroodPot.OnInteract(byPlayer) : base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override float GetAmbientSoundStrength(IWorldAccessor world, BlockPos pos)
        {
            float soundVolume;
                if (world.BlockAccessor.GetBlockEntity(pos) is BECeramicBroodPot pot && pot.isActiveHive)
                {

                soundVolume = (int)pot.HivePopSize switch
                {
                    0 => 0.44f,
                    1 => 0.88f,
                    _ => 1f,
                };

                switch (FGCClientConfig.Current.hiveSoundVolume)
                    {
                        case "off": soundVolume = 0f; break;
                        case "soft": soundVolume *= 0.5f; break;
                        case "normal": soundVolume = 1f; break;
                        case "high": soundVolume *= 2f; break;
                        case "loud": soundVolume *= 4f; break;
                        default: soundVolume *= 1f; break;
                    }
                    soundVolume = Math.Max(soundVolume * pot.ActivityLevel, 0.25f);
                    return soundVolume;
                }
            return 0f;

        }

        /// <summary>Called when a block is initially placed.</summary>
        /// <param name="world">The world.</param>
        /// <param name="blockPos">The block position.</param>
        /// <param name="stack">The stack.</param>
        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack stack)
        {
            if (stack != null && stack.Attributes != null)
            {
                bool isHiveActive = stack.Attributes.GetBool("isactivehive", false);
                base.OnBlockPlaced(world, blockPos);
                BECeramicBroodPot beCeramicBroodPot = (BECeramicBroodPot)world.BlockAccessor.GetBlockEntity(blockPos);
                if (beCeramicBroodPot == null) return;
                beCeramicBroodPot.isActiveHive = isHiveActive;
                beCeramicBroodPot.SetHiveSize(stack.Attributes.GetAsInt("hiveHealth"));
                beCeramicBroodPot.TestHarvestable(0);
                beCeramicBroodPot.Initialize(api);
            }
        }

        /// <summary>Called when a block is broken, and rolls chance of producing a beemob if populated.</summary>
        /// <param name="world">The world.</param>
        /// <param name="pos">The position.</param>
        /// <param name="byPlayer">The by player.</param>
        /// <param name="dropQuantityMultiplier">The drop quantity multiplier.</param>
        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            BECeramicBroodPot beCeramicBroodPot = (BECeramicBroodPot)world.BlockAccessor.GetBlockEntity(pos);

            if (world.Side == EnumAppSide.Server && (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative))
            {
                
                ItemStack stack = this.OnPickBlock(world, pos);
                beCeramicBroodPot?.SetAttributesOnPickup(stack);
                world.SpawnItemEntity(stack, new Vec3d((double)pos.X + 0.5, (double)pos.Y + 0.5, (double)pos.Z + 0.5));
                world.PlaySoundAt(Sounds.GetBreakSound(byPlayer), pos.X, pos.Y, pos.Z, 1);
            }

            if (EntityClass != null)
            {
                world.BlockAccessor.GetBlockEntity(pos)?.OnBlockBroken();
            }

            world.BlockAccessor.SetBlock(0, pos);
        }

        public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref string failureCode)
        {
            if (blockSel.Block is BlockFence && !FGCServerConfig.Current.canPlaceLangstrothOnFence)
            {
                failureCode = "requireattachable";
                return false;
            }
            return base.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode);
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            WorldInteraction[] wi = null;
            WorldInteraction[] wi2 = base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
            WorldInteraction[] wi2a = base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
            WorldInteraction[] wi3 = base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);

            
            if (world.BlockAccessor.GetBlockEntity(selection.Position) is BECeramicBroodPot pot)
            {
                List<ItemStack> topList = new();
                topList.Add(new ItemStack(api.World.BlockAccessor.GetBlock(new AssetLocation("fromgoldencombs", "hivetop-blue-fired"))));

                if (pot != null)
                {

                    //Information about world interaction
                    if (!pot.isActiveHive)
                    {
                        wi = ObjectCacheUtil.GetOrCreate(api, "broodPotInteractions", () =>
                        {
                            List<ItemStack> skepList = new();
                            skepList.Add(new ItemStack(api.World.BlockAccessor.GetBlock(new AssetLocation("game", "skep-papyrus-populated-east")), 1));
                            skepList.Add(new ItemStack(api.World.BlockAccessor.GetBlock(new AssetLocation("game", "skep-reed-populated-east")), 1));


                            return new WorldInteraction[]
                            {
                            new (){
                                ActionLangCode = "fromgoldencombs:blockhelp-ceramichive-empty-notop",
                                MouseButton = EnumMouseButton.Right,
                                Itemstacks = skepList.ToArray()
                            }
                            };
                        });
                    }
                }

                    wi2 = ObjectCacheUtil.GetOrCreate(api, "broodPotInteractions2", () =>
                    {


                        return new WorldInteraction[]
                        {
                                new (){
                                    ActionLangCode = Lang.Get("fromgoldencombs:placepot"),
                                    MouseButton = EnumMouseButton.Right,
                                    Itemstacks = topList.ToArray()
                            }

                        };

                    });

                    wi2a = ObjectCacheUtil.GetOrCreate(api, "broodPotInteractions2a", () =>
                    {


                        return new WorldInteraction[]
                        {
                                new (){
                                    ActionLangCode = Lang.Get("fromgoldencombs:removepot"),
                                    MouseButton = EnumMouseButton.Right
                            }

                    };

                    });

                    wi3 = ObjectCacheUtil.GetOrCreate(api, "broodPotInteractions3", () =>
                    {

                        return new WorldInteraction[]
                        {
                                new (){
                                    ActionLangCode = Lang.Get("fromgoldencombs:emptybagslot"),
                                    MouseButton = EnumMouseButton.Right,
                                    Itemstacks = null
                                }
                        };
                    });

                if (pot.isActiveHive)
                {
                    if (Variant["top"] == "withtop")
                    {
                        return wi2a;
                    }
                    else
                    {
                        return wi2.Append(wi3);
                    }
                    
                }

                if (!pot.isActiveHive)
                {

                    if (Variant["top"] == "withtop")
                    {
                        return wi.Append(wi2a);
                    }
                    else
                    {
                        return wi.Append(wi2).Append(wi3);
                    }
                }
            }
            return wi;
        }

        public override string GetHeldItemName(ItemStack itemStack)
        {

            return base.GetHeldItemName(itemStack) + " (" + CultureInfo.CurrentCulture.TextInfo.ToTitleCase(Lang.Get(this.Variant["color"] + ")"));            
        }
    }
}
