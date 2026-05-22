using FromGoldenCombs.BlockEntities;
using FromGoldenCombs.Util.Config;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace FromGoldenCombs.Blocks.Langstroth
{
    class LangstrothStack : LangstrothCore
    {
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
        }
        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            if (world.BlockAccessor.GetBlock(pos.DownCopy(), 0).BlockMaterial == EnumBlockMaterial.Air)
            {
                this.OnBlockBroken(world, pos, null);
                if (world.BlockAccessor.GetBlock(pos.UpCopy(), 0) is LangstrothCore)
                {
                    world.BlockAccessor.GetBlock(pos.UpCopy(), 0).OnNeighbourBlockChange(world, pos.UpCopy(), neibpos);
                }

            }
            base.OnNeighbourBlockChange(world, pos, neibpos);
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            if (world.Side == EnumAppSide.Server && (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative))
            {
                ItemStack[] array = new ItemStack[]{};
                for (int i = 0; i < array.Length; i++)
                {
                    world.SpawnItemEntity(array[i], new Vec3d((double)pos.X + 0.5, (double)pos.Y + 0.5, (double)pos.Z + 0.5), null);
                }
                world.PlaySoundAt(this.Sounds.GetBreakSound(byPlayer).Location, (double)pos.X, (double)pos.Y, (double)pos.Z, byPlayer, true, 32f, 1f);
            }
            if (this.EntityClass != null)
            {
                BlockEntity blockEntity = world.BlockAccessor.GetBlockEntity(pos);
                blockEntity?.OnBlockBroken();
            }
            world.BlockAccessor.SetBlock(0, pos);
        }

        public override float GetAmbientSoundStrength(IWorldAccessor world, BlockPos pos)
        {
            if ((world.BlockAccessor.GetBlockEntity(pos) is BELangstrothStack stack && stack.isHiveActive())) {
                float v = (int)stack.HivePopSize switch
                {
                    0 => 0.44f,
                    1 => 0.88f,
                    _ => 1f,
                };
                float soundVolume = 0f;
                soundVolume *= FGCClientConfig.Current.hiveSoundVolume switch
                {
                    "off" => 0f,
                    "soft" => 0.5f,
                    "normal" => 1f,
                    "high" => 2f,
                    "loud" => 4f,
                    _ => 1f,
                };
                soundVolume = Math.Max((float)v * stack.ActivityLevel, 0.25f);
                    return (float)v;
            }
            return 0f;
            
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {

            BELangstrothStack belangstrothstack = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BELangstrothStack;
            return belangstrothstack is not null ? belangstrothstack.OnInteract(byPlayer) : base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            List<Cuboidf> curSelectionBoxes = new();
            BELangstrothStack curBE = blockAccessor.GetBlockEntity<BELangstrothStack>(pos);
            curSelectionBoxes.Add(SelectionBoxes[0]);

            if (curBE != null)
            {
                for (int i = 1; i < curBE.Inventory.Count; i++)
                {
                    if (!curBE.Inventory[i].Empty)
                    {
                        curSelectionBoxes.Add(SelectionBoxes[i]);
                    }
                }

            }
            return curSelectionBoxes.ToArray();
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            List<Cuboidf> curCollisionBoxes = new ();
            BELangstrothStack curBE = blockAccessor.GetBlockEntity<BELangstrothStack>(pos);
            curCollisionBoxes.Add(SelectionBoxes[0]);

            if (curBE != null)
            {
                for (int i = 1; i < curBE.Inventory.Count; i++)
                {
                    if (!curBE.Inventory[i].Empty)
                    {
                        curCollisionBoxes.Add(SelectionBoxes[i]);
                    }
                }

            }
            return curCollisionBoxes.ToArray();
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            WorldInteraction[] wi;

            wi = ObjectCacheUtil.GetOrCreate(api, "stackInteractions1", () =>
            {

                return new WorldInteraction[] {
                            new () {
                                    ActionLangCode = "fromgoldencombs:blockhelp-langstrothstack",
                                    MouseButton = EnumMouseButton.Right,
                            }
                    };

            });

            return wi;
        }
    }
}
