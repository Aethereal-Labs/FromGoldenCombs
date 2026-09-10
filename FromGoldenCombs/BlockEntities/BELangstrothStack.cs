using FromGoldenCombs.BlockBehaviors;
using FromGoldenCombs.Blocks;
using FromGoldenCombs.Blocks.Langstroth;
using FromGoldenCombs.Util.Config;
using System;
using System.Text;
using VFromGoldenCombs.Blocks.Langstroth;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace FromGoldenCombs.BlockEntities
{
    class BELangstrothStack : BlockEntityDisplay
    {

        double harvestableAtTotalHours;
        double cooldownUntilTotalHours;
        int quantityNearbyFlowers;
        int quantityNearbyHives;
        float _activityLevel;
        private RoomRegistry roomreg;
        float roomness;
        public static SimpleParticleProperties Bees;
        int scanQuantityNearbyFlowers;
        int scanQuantityNearbyHives;
        int scanIteration;
        EnumHivePopSize _hivePopSize;
        int harvestableFrames = 0;
        int linedFrames = 0;
        int totalFrames = 0;
        public readonly InventoryGeneric inv;
        public override InventoryBase Inventory => inv;
        float harvestBase;
        public override string InventoryClassName => "langstrothstack";
        private bool _isActiveHive = false;
        double chargesPerDay = FGCServerConfig.Current.langstrothBaseChargesPerDay;
        double cropChargeGrowthHours = 24; //Number of hours until the hive accumulates a new grow charge.
        double cropChargeAtTotalHours;
        int cropcharges;
        int maxCropCharges = FGCServerConfig.Current.langstrothMaxCropCharges;
        int cropChargeRange = FGCServerConfig.Current.langstrothCropRange;
        int nonSupersInFullStack = 0;
        BELangstrothStack bottomStack;
        float todayNoonTemp;
        float yesterdayNoonTemp;
        float twoDayAgoNoonTemp;
        float threeDayTemp;
        ClimateCondition conds;
        int daysTillHarvest;
        double worldTime;
        float minTemp = FGCServerConfig.Current.LangstrothHiveMinTemp;
        float maxTemp = FGCServerConfig.Current.LangstrothHiveMaxTemp == 0 ? 37f : FGCServerConfig.Current.LangstrothHiveMaxTemp;
        float optimalTemp;
        float curTemp;

        public EnumHivePopSize HivePopSize { 
            get{
                return _hivePopSize;
            } 
        }
        public float ActivityLevel
        {
            get { return _activityLevel; }
        }
        public BELangstrothStack()
        {
            inv = new InventoryGeneric(3, "superslot-0", null, null);
        }

        public bool isHiveActive()
        {
            return _isActiveHive;
        }



        static BELangstrothStack()
        { 
        Bees = new SimpleParticleProperties(1f, 1f, ColorUtil.ToRgba(255, 215, 156, 65), new Vec3d(), new Vec3d(), new Vec3f(0f, 0f, 0f), new Vec3f(0f, 0f, 0f), 1f, 0f, 0.5f, 0.5f, EnumParticleModel.Cube);
        }

        public override void Initialize(ICoreAPI api)
        {
            Block block = api.World.BlockAccessor.GetBlock(Pos, 0);
            base.Initialize(api);
            RegisterGameTickListener(TestHarvestable, 5000);
            RegisterGameTickListener(OnScanForFlowers, api.World.Rand.Next(5000) + 30000);
            
            roomreg = Api.ModLoader.GetModSystem<RoomRegistry>();

            block = Api.World.BlockAccessor.GetBlock(Pos, 0);

            if (api.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = api as ICoreClientAPI;
                RegisterGameTickListener(SpawnBeeParticles, 300);
            }
            UpdateClimateValues(api);
        }

        private void UpdateClimateValues(ICoreAPI api)
        {
            worldTime = api.World.Calendar.TotalHours;
            bottomStack = GetBottomStack();
            bottomStack.harvestBase = (float)((FGCServerConfig.Current.LangstrothDaysToHarvestIn30DayMonths * ((float)Api.World.Calendar.DaysPerMonth / 30f)) * Api.World.Calendar.HoursPerDay);
            bottomStack.todayNoonTemp = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, (Double)((int)(Api.World.Calendar.TotalDays)) + 0.66f).Temperature;
            bottomStack.yesterdayNoonTemp = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, (Double)((int)(Api.World.Calendar.TotalDays - 1)) + 0.66f).Temperature;
            bottomStack.twoDayAgoNoonTemp = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, (Double)((int)(Api.World.Calendar.TotalDays - 2)) + 0.66f).Temperature;
            bottomStack.threeDayTemp = (todayNoonTemp * 2 + yesterdayNoonTemp + twoDayAgoNoonTemp) / 4 + (roomness > 0 ? 5 : 0);
            conds = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.NowValues);
            double actualDaysTillHarvest = (bottomStack.harvestableAtTotalHours - worldTime) / 24d;
            daysTillHarvest = actualDaysTillHarvest < 1d ? 0 : (int)(actualDaysTillHarvest);
            optimalTemp = (maxTemp + minTemp) / 2;
            curTemp = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.NowValues).Temperature + (this.roomness > 0 ? -5 : 0);
        }

        public void OnPollinationNearby(string eventName, BlockPos cropPos, ref EnumHandling handling, IAttribute data)
        {
            
            if (Api.Side.IsClient()) return;
            int deltaX = cropPos.X - bottomStack.Pos.X;
            int deltaY = cropPos.Y - bottomStack.Pos.Y;
            int deltaZ = cropPos.Z - bottomStack.Pos.Z;
            double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ);

            if (isHiveActive() && bottomStack._hivePopSize != EnumHivePopSize.Poor && bottomStack.Api.Side.IsServer())
            {
                if (eventName == "cropbreak")
                {
                    manageCropBoost(cropPos, distance, ref handling);
                }
                else if (eventName == "berryharvest")
                {
                    manageBerryBoost(cropPos, distance, ref handling);
                }
                else if (eventName == "fruitharvest")
                {
                    manageFruitBoost(cropPos, distance, ref handling);
                }
            }
        }

        private void manageCropBoost(BlockPos cropPos, double distance, ref EnumHandling handling)
        {

            if (bottomStack.cropcharges < 1 || Api?.World == null || distance >= bottomStack.cropChargeRange) return;

            if (Api.World.BlockAccessor.GetBlock(cropPos) is not BlockCrop crop 
               || Api.World.BlockAccessor.GetBlockEntity(cropPos.DownCopy()) is not BlockEntityFarmland
               || !crop.HasBehavior<PushEventOnCropBreakBehavior>()) return;

            PushEventOnCropBreakBehavior behavior = crop.GetBehavior<PushEventOnCropBreakBehavior>();
            
            if (behavior?.validCropStages == null 
                || !behavior.validCropStages.Contains<int>(crop.CurrentCropStage)) return;


            // Claim the pollination event so other nearby hives don't all do the same work.
            handling = EnumHandling.PreventSubsequent;

            behavior.beeChanceMultiplier = FGCServerConfig.Current.langstrothCropBoostPercentage;
            behavior.setHandling(EnumHandling.PreventSubsequent);
            cropcharges--;
            MarkDirty();
        }

        private void manageBerryBoost(BlockPos bushPos, double distance, ref EnumHandling handling)
        {
            if (Api?.Side != EnumAppSide.Server || Api?.World == null) return;
            if (cropcharges < 1 || distance >= cropChargeRange) return;

            Block bushBlock = Api.World.BlockAccessor.GetBlock(bushPos);
            if (bushBlock == null) return;

            BEBehaviorFruitingBush bebfb = Api.World.BlockAccessor.GetBlockEntity(bushPos)?.GetBehavior<BEBehaviorFruitingBush>();
            if (!(bushBlock.Attributes?["foodTags"]?.AsArray<string>()?.Contains("sweetBerryBush") == true || !bushBlock.HasBehavior<PushEventOnBlockHarvested>() || bebfb.BState.Growthstate == EnumFruitingBushGrowthState.Ripe)) return;

            PushEventOnBlockHarvested eventBehavior = bushBlock.GetBehavior<PushEventOnBlockHarvested>();
            if (eventBehavior == null) return;

            // Claim the pollination event so other nearby hives don't all do the same work.
            handling = EnumHandling.PreventSubsequent;

            eventBehavior.beeChanceMultiplier = FGCServerConfig.Current.langstrothCropBoostPercentage;
            eventBehavior.useBeeBoost = true;
            cropcharges--;
            MarkDirty();

        }

        private void manageFruitBoost(BlockPos fruitFoliagePos, double distance, ref EnumHandling handling)
        {

            if (Api?.Side != EnumAppSide.Server || Api?.World == null) return;
            if (cropcharges < 1 || distance >= cropChargeRange) return;

            if (Api.World.BlockAccessor.GetBlockEntity(fruitFoliagePos) is not BlockEntityFruitTreePart beFTP) return;

            string branchBlockCode = beFTP.Block?.Attributes?["branchBlock"]?.AsString(null);
            if (string.IsNullOrEmpty(branchBlockCode) || beFTP.Block?.Code == null) return;

            AssetLocation loc;
            try
            {
                loc = AssetLocation.Create(branchBlockCode, beFTP.Block.Code.Domain);
            }
            catch
            {
                return;
            }

            if (loc == null) return;

            BlockFruitTreeBranch branchBlock = Api.World.GetBlock(loc) as BlockFruitTreeBranch;
            if (branchBlock?.TypeProps == null) return;
            if (!branchBlock.TypeProps.TryGetValue(beFTP.TreeType, out var typeProps) || typeProps?.FruitStacks == null) return;

            foreach (BlockDropItemStack drop in typeProps.FruitStacks)
            {
                if (drop == null) continue;

                ItemStack stack = drop.GetNextItemStack(FGCServerConfig.Current.langstrothCropBoostPercentage);
                if (stack != null)
                {
                    Api.World.SpawnItemEntity(stack, beFTP.Pos.Add(0.0f, 0.5f, 0.0f), null);
                }


                if (drop.LastDrop)
                {
                    break;
                }

            }

            cropcharges--;
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            Api.ModLoader.GetModSystem<FromGoldenCombs>().OnPollination -= OnPollinationNearby;
            base.OnBlockBroken(byPlayer);
        }
        public bool OnInteract(IPlayer byPlayer)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            CollectibleObject colObj = slot.Itemstack?.Collectible;
            bool isLangstroth = colObj is LangstrothCore;
            UpdateClimateValues(Api);
            if (Api.Side == EnumAppSide.Server)
            {
                Api.ModLoader.GetModSystem<FromGoldenCombs>().OnPollination -= bottomStack.OnPollinationNearby;
            }
            if (slot.Empty)
            {
                if (TryTake(byPlayer)) //Attempt to take a super from the topmost stack
                                       //if there are multiple stacks on top of each other.
                                       //Or from the topmost occupied slot of this stack.
                {
                    if (Api.World.BlockAccessor.GetBlock(bottomStack.Pos, 0) is LangstrothStack)
                    {
                        //Get Current Hive Activity
                        bool curHiveActive = bottomStack._isActiveHive;

                        //Update hive activity after adding block
                        bottomStack._isActiveHive = bottomStack.IsValidHive();

                        //
                        if (!bottomStack._isActiveHive && curHiveActive && Api.Side.IsServer())
                        {
                            Api.ModLoader.GetModSystem<FromGoldenCombs>().OnPollination -= OnPollinationNearby;
                        }
                        bottomStack.ResetHive();
                    }
                    CountFrames();
                    MarkDirty(true);
                    return true;
                }
            }
            else if (isLangstroth && !IsStackFull())
            {
                if (TryPut(slot)) //Attempt to place super either in the current stack,
                                  //any stacks above this, or as a new stack above the
                                  //topmost stack if the block at that position is an air block.
                {
                    //Get Current Hive Activity
                    bool curHiveActive = bottomStack._isActiveHive;

                    //Update hive activity after adding block
                    bottomStack._isActiveHive = bottomStack.IsValidHive();
                    
                    //
                    if (bottomStack._isActiveHive && !curHiveActive && Api.Side.IsServer())
                    {
                        Api.ModLoader.GetModSystem<FromGoldenCombs>().OnPollination += bottomStack.OnPollinationNearby;
                    }
                    bottomStack.ResetHive();
                }
                CountFrames();
                MarkDirty(true);
                return true; //This prevents TryPlaceBlock from passing if TryPut fails.
            }
            if (slot.Itemstack?.Block is BlockSkep skep && skep.Variant["type"] == "populated")
            {
                UpdateBroodBox(slot);
                CountFrames();
                IsValidHive();
                this.GetTopStack().updateMeshes();
                MarkDirty(true);
                return true;
            }
            CountFrames();
            updateMeshes();
            MarkDirty(true);
            return false;
        }

        #region Frame Management
        //UpdateFrames cycles through the stack checking for frames that lined to update to Harvestable
        //Will do as many frames as fillframes
        private void UpdateFrames(int fillframes)
        {
            BELangstrothStack curBE = (BELangstrothStack)Api.World.BlockAccessor.GetBlockEntity(GetTopStack().Pos);

            //curBE is BELangstrothStack was replaced with curBE is not null
            while (curBE is not null)
            {
                for (int index = 2; index >= 0; index--)
                {

                    if (curBE.inv[index].Itemstack != null && curBE.inv[index].Itemstack.Block is LangstrothSuper && curBE.inv[index].Itemstack.Attributes.GetTreeAttribute("contents") != null)
                    {
                        ITreeAttribute contents = curBE.inv[index].Itemstack.Attributes.GetTreeAttribute("contents");
                        int contentsSize = contents.Count;

                        for (int j = 0; j <= contentsSize && fillframes > 0; j++)
                        {
                            ItemStack stack = contents.GetItemstack((j - 1).ToString());
                            if (stack?.Collectible.FirstCodePart() == "beeframe")
                            {
                                if (stack.Collectible.Variant["harvestable"] == "lined")
                                {
                                    int durability = stack.Attributes.GetInt("durability") == 0 ? FGCServerConfig.Current.baseframedurability : stack.Attributes.GetInt("durability");
                                    stack = new ItemStack(Api.World.GetItem(stack.Collectible.CodeWithVariant("harvestable", "harvestable")), 1);
                                    stack.Attributes.SetInt("durability", durability);
                                    fillframes--;
                                }
                                curBE.inv[index].Itemstack.Attributes.GetTreeAttribute("contents").SetItemstack((j - 1).ToString(), stack);
                            }
                            curBE.inv[index].MarkDirty();
                        }
                    }
                }
                if (Api.World.BlockAccessor.GetBlockEntity(curBE.Pos.DownCopy()) is BELangstrothStack beStack) 
                {
                    curBE = beStack;
                }
                else
                {
                    break;
                }

            }
            updateMeshes();
        }

        private int CountFrames()
        {

            BELangstrothStack topStack = GetTopStack();
            if (Api.World.BlockAccessor.GetBlockEntity(topStack.Pos) is not BELangstrothStack curBE) return 0;
            //BELangstrothStack curBE = (BELangstrothStack)Api.World.BlockAccessor.GetBlockEntity(topStack.Pos);
            bottomStack.totalFrames = 0;
            bottomStack.linedFrames = 0;
            bottomStack.harvestableFrames = 0;

            while (curBE is not null)
            {
                for (int index = 2; index >= 0; index--)
                {

                    if (curBE.inv[index].Itemstack != null && curBE.inv[index].Itemstack.Block is LangstrothSuper && curBE.inv[index].Itemstack.Attributes.GetTreeAttribute("contents") != null)
                    {
                        ITreeAttribute contents = curBE.inv[index].Itemstack.Attributes.GetTreeAttribute("contents");
                        int contentsSize = contents.Count;

                        for (int j = 0; j <= contentsSize; j++)
                        {
                            ItemStack stack = contents.GetItemstack((j - 1).ToString());
                            if (stack?.Collectible.FirstCodePart() == "beeframe")
                            {
                                    bottomStack.totalFrames++;
                            }
                            if (stack?.Collectible.Variant["harvestable"] == "lined")
                            {
                                bottomStack.linedFrames++;
                            }
                            if (stack?.Collectible.Variant["harvestable"] == "harvestable")
                            {
                                bottomStack.harvestableFrames++;
                            }
                            curBE.inv[index].MarkDirty();
                        }
                    }
                }
                if (Api.World.BlockAccessor.GetBlockEntity(curBE.Pos.DownCopy()) is BELangstrothStack bestack)
                {
                    curBE = bestack;
                }
                else
                {
                    break;
                }
            }
            MarkDirty(true);
            return bottomStack.totalFrames;
        }

        public bool InitializePut(ItemStack first, ItemSlot slot)
        {
            inv[0].Itemstack = first;
            this.TryPut(slot);
            UpdateStackSize();
            MarkDirty(true);
            updateMeshes();
            return true;

        }
        #endregion

        private bool TryPut(ItemSlot slot)
        {
            int index = 0;

            
            while (index < inv.Count - 1 && !inv[index].Empty) //Cycle through indices until reach an empty index, or the top index
            {
                index++;
            }

            if (inv[index].Empty) //If the new target index is empty, place a super
            {
                
                if (index - 1 >= 0 && (inv[index - 1].Itemstack.Block is LangstrothBrood))
                {
                    return false;
                }
                else if (index - 1 < 0 && Api.World.BlockAccessor.GetBlockEntity(Pos.DownCopy()) is BELangstrothStack stack)
                {
                    if (stack.GetStackIndex(2).Block is LangstrothBrood)
                    {
                        Api.World.BlockAccessor.SetBlock(0, Pos);
                        return false;
                    }
                    inv[index].Itemstack = slot.TakeOutWhole();
                    updateMeshes();
                    MarkDirty(true);
                    return true;
                }
                inv[index].Itemstack = slot.TakeOutWhole();
                updateMeshes();
                MarkDirty(true);
                return true;
            }
            else if (IsLangstrothAt(Pos.UpCopy())) //Otherwise, check to see if the next block up is a Super or SuperStack
            {
                if (Api.World.BlockAccessor.GetBlockEntity(Pos.UpCopy()) is BELangstrothStack stack) //If It's a SuperStack, Send To Next Stack
                {
                    stack.TryPut(slot);
                }
                else if (Api.World.BlockAccessor.GetBlock(Pos.UpCopy(), 0) is LangstrothCore) //If It's a LangstrothCore, create a new LangstrothStack
                {
                    
                    Api.World.BlockAccessor.SetBlock(Api.World.GetBlock(new AssetLocation("fromgoldencombs", "langstrothstack-two-" + GetSide(this.Block))).BlockId, Pos.UpCopy());
                    
                }
            }
            else if (Api.World.BlockAccessor.GetBlock(Pos.UpCopy(), 0).BlockMaterial == EnumBlockMaterial.Air)
            {
                Api.World.BlockAccessor.SetBlock(Api.World.GetBlock(new AssetLocation("fromgoldencombs", "langstrothstack-two-" + this.Block.Variant["side"])).BlockId, Pos.UpCopy());
                TryPut(slot);
            }
            updateMeshes();
            MarkDirty();
            return true;
        }

        //TryTake attemps to retrieve the contents of an Inventory Slot in the stack
        private bool TryTake(IPlayer byPlayer)
        {
            bool isSuccess = false;
            int index = 0;

            //Cycle through indices until the topmost occupied index that has an empty index over it is reached, or the top index is reached.
            while (index < inv.Count - 1 && !inv[index + 1].Empty)
            {
                index++;
            }

            // Confirm if this is the top inventory slot of the stack
            bool isTopSlot = index == inv.Count - 1;
            bool langstrothAbove = IsLangstrothAt(Pos.UpCopy());
            bool airAbove = Api.World.BlockAccessor.GetBlock(Pos.UpCopy(), 0).BlockMaterial == EnumBlockMaterial.Air;

            //If the block above isn't air, of if the target index is empty, return iSSuccess, Still False
            if (isTopSlot && (!airAbove && !langstrothAbove))
            {
                return isSuccess;
            }

            //If the above block is of type LangstrothCore
            if (langstrothAbove)
            {
                Block block = Api.World.BlockAccessor.GetBlock(Pos.UpCopy(), 0);
                //If it's not a LangstrothStack, take the block
                if (block is not LangstrothStack && block is LangstrothCore)
                {
                    ItemStack stack = Api.World.BlockAccessor.GetBlock(Pos.UpCopy(), 0).OnPickBlock(Api.World, Pos.UpCopy());

                    return byPlayer.InventoryManager.TryGiveItemstack(stack);
                }
                //If it is a stack, retrieve the block from the stack
                else if (block is LangstrothStack && Api.World.BlockAccessor.GetBlockEntity(Pos.UpCopy()) is BELangstrothStack BELangStack)
                {
                    return BELangStack.RetrieveSuper(byPlayer);
                }

            }
            else
            //Otherwise return the context of the targeted index
            {
                if (byPlayer.InventoryManager.TryGiveItemstack(inv[index].Itemstack))
                {
                    inv[index].TakeOutWhole();
                    isSuccess = true; //isSuccess only equals true ONLY if the above if passes. All other cases its false.
                    //AssetLocation sound = stack.Block?.Sounds?.Place;
                    //Api.World.PlaySoundAt(sound ?? new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
                    MarkDirty(true);
                }
            }
            UpdateStackSize();
            return isSuccess;

            //Summar"y": TryTake grabs the topmost index out of a stack of stacks. In a single stack, it takes the topmost index, or the targeted index if empty.
        }

        private void UpdateBroodBox(ItemSlot slot)
        {
            if (IsLangstrothAt(Pos.UpCopy()))
            {
                (Api.World.BlockAccessor.GetBlockEntity(Pos.UpCopy()) as BELangstrothStack).UpdateBroodBox(slot);
            }

            int index = 0;
            while (index < inv.Count - 1 && !inv[index + 1].Empty)
            {
                index++;
            }

            if (inv[index].Itemstack.Block is LangstrothBrood Brood && Brood.Variant["populated"] == "empty")
            {
                slot.Itemstack = null;
                inv[index].Itemstack = new ItemStack(Api.World.BlockAccessor.GetBlock(Brood.CodeWithVariant("populated", "populated")));
                bottomStack._isActiveHive = bottomStack.IsValidHive();
                bottomStack.ResetHive();
            };

        }

        private void UpdateStackSize()
        {
            CountFrames();
            //Update the Stack to match the number of blocks in it.
            int filledstacks = 0;
            string stacksize;
            for (int i = 0; i < inv.Count-1; i++)
            {
                if (!inv[i].Empty)
                {
                    filledstacks++;
                }
            }

            stacksize = filledstacks == 0 ? "zero" : filledstacks == 1 ? "one" : filledstacks == 2 ? "two" : "three";
            if (stacksize == "zero")
            {
                Api.World.BlockAccessor.SetBlock(0, this.Pos);
            }
            else if (stacksize == "one" && !IsLangstrothAt(Pos.DownCopy())) //If there's only one block left in the stack, and the below stack is a langstroth block
            {
                ItemStack stack = inv[0].TakeOutWhole();
                Api.World.BlockAccessor.SetBlock(Api.World.BlockAccessor.GetBlock(stack.Block.CodeWithVariant("side", GetSide(this.Block))).BlockId, Pos, stack);
            }
            else
            {
                Api.World.BlockAccessor.ExchangeBlock(Api.World.BlockAccessor
                    .GetBlock(new AssetLocation("fromgoldencombs", "langstrothstack-" + stacksize + "-" + this.Block.Variant["side"])).BlockId, Pos);
            }

            //Summar"y": UpdateStackSize changes the size of the stack as blocks are added or removed from it.
            //If a single block is left in a stack, the stack is removed and that block placed, provided that the block under the stack is not another stack.
        }

        public void ReceiveSuper(ItemSlot slot)
        {
            //Receive a super from another source for placement.
            //Intended to function as a way for other stacks to send blocks to this stack.
            TryPut(slot);
            //MarkDirty(true);
        }

        public bool RetrieveSuper(IPlayer byPlayer)
        {
            //Receive a call to TryTake from another source for this super.
            //Intended to function as a way for other stacks to take blocks from this stack and give them to the player.
            return TryTake(byPlayer);
        }

        private string GetSide(Block block)
        {
            return Api.World.BlockAccessor.GetBlock(block.BlockId).Variant["side"].ToString();
        }

        public ItemStack GetStackIndex(int i)
        {
            //return the contents of a specific index.
            //Current use is checking to see if top index of a stack is a LangstrothBrood. Intended to prevent adding a LangstrothBlock on top of a broodbox.
            return inv[i].Itemstack;
        }

        //Identify if the block at the given BlockPos is of type LangstrothCore
        private bool IsLangstrothAt(BlockPos pos)
        {
            Block aboveBlockName = Api.World.BlockAccessor.GetBlock(pos, 0);

            return aboveBlockName is LangstrothCore;
        }

        public void UpdateOnNeighborBreak()
        {
            bottomStack._isActiveHive = IsValidHive();
        }

        private bool IsValidHive()
        {
            BELangstrothStack topStack = GetTopStack();

           //Check bottomStack's bottom index for a LangstrothBase
            if (bottomStack.inv[0].Itemstack?.Block is not LangstrothBase) {
                ResetHive();
                return false;
            }     

        //Check topStack's top Index for populated brood box
            Block topBlock = topStack?.inv[topStack.StackSize() - 1].Itemstack.Block;
            if (topBlock is not LangstrothBrood || topBlock.Variant["populated"] == "empty"){
                ResetHive();
                return false;
            }

            //Check the rest of the hive for anything not a super.
            bool moreThanTwoNonSupers = nonSupersPresent(topStack);
            return moreThanTwoNonSupers;
        }

        private bool nonSupersPresent(BELangstrothStack topStack)
        {

            BELangstrothStack curBE = topStack;
            nonSupersInFullStack = 0;

            while (curBE is not null) { 
                foreach (ItemSlot index in curBE.Inventory)
                {
                    if (index.Itemstack?.Collectible != null && index.Itemstack?.Collectible is not LangstrothSuper)
                    {
                        nonSupersInFullStack++;
                    }
                }
                if (Api.World.BlockAccessor.GetBlockEntity(curBE.Pos.DownCopy(1)) is BELangstrothStack nextStack)
                {
                    curBE = nextStack;
                }
                else
                {
                    break;
                }
            }
            return nonSupersInFullStack == 2;
        }


        public bool IsStackFull()
        {
            //If there is a stack below this stack
            //True -> GetStackSize from Stack.Down();
            int maxStackSize = FGCServerConfig.Current.MaxStackSize;
            if (TotalStackSize() >= maxStackSize)
                return true;

            return false;
        }


        //ReturnStackSize
        public int StackSize()
        {
            int filledstacks = 0;
            for (int i = 0; i < inv.Count; i++)
            {
                if (!inv[i].Empty)
                {
                    filledstacks++;
                }
            }
            return filledstacks;
        }

        //Return total number of Supers in the Stack
        public int TotalStackSize()
        {
            if (GetTopStack() != null)
            {
                int totalStackSize = GetTopStack().StackSize();
                BlockPos TopStack = GetTopStack().Pos;
                int downCount = 1;
                while (Api.World.BlockAccessor.GetBlockEntity(TopStack.DownCopy(downCount)) is BELangstrothStack i)
                {
                    totalStackSize += i.StackSize();
                    downCount++;
                }
                return totalStackSize;
            }
            return 1;
        }

        //Return Top Stack of Stack
        public BELangstrothStack GetTopStack()
        {
            int upCount = 0;
            BELangstrothStack topStack = this;

            while (Api.World.BlockAccessor.GetBlockEntity(Pos.UpCopy(upCount)) is BELangstrothStack stack)
            {
                topStack = stack;
                upCount++;
            }
            return topStack;
        }

        public BELangstrothStack GetBottomStack()
        {

            int downCount = 1;

            BELangstrothStack bottomStack = this;

            while (Api.World.BlockAccessor.GetBlockEntity(Pos.DownCopy(downCount)) is BELangstrothStack stack)
            {
                bottomStack = stack;
                downCount++;

            }
            return bottomStack;
        }

        //Rendering Processes
        

        //Active Hive Methods/Fields
        readonly Vec3d startPos = new();
        readonly Vec3d endPos = new();
        Vec3f minVelo = new();

        private void SpawnBeeParticles(float dt)
        {
            if (_isActiveHive && Pos == bottomStack.Pos)
            {
                float dayLightStrength = Api.World.Calendar.GetDayLightStrength(Pos.X, Pos.Z);
                if (Api.World.Rand.NextDouble() > 2 * dayLightStrength - 0.5) return;

                Random rand = Api.World.Rand;

                Bees.MinQuantity = _activityLevel;

                // Leave hive
                if (Api.World.Rand.NextDouble() > 0.5)
                {
                    startPos.Set(Pos.X + 0.5f, Pos.Y + 0.5f, Pos.Z + 0.5f);
                    minVelo.Set((float)rand.NextDouble() * 3 - 1.5f, (float)rand.NextDouble() * 1 - 0.5f, (float)rand.NextDouble() * 3 - 1.5f);

                    Bees.MinPos = startPos;
                    Bees.MinVelocity = minVelo;
                    Bees.LifeLength = 1f;
                    Bees.WithTerrainCollision = false;
                }
                // Go back to hive
                else
                {
                    startPos.Set(Pos.X + rand.NextDouble() * 5 - 2.5, Pos.Y + rand.NextDouble() * 2 - 1f, Pos.Z + rand.NextDouble() * 5 - 2.5f);
                    endPos.Set(Pos.X + 0.5f, Pos.Y + 0.5f, Pos.Z + 0.5f);

                    minVelo.Set((float)(endPos.X - startPos.X), (float)(endPos.Y - startPos.Y), (float)(endPos.Z - startPos.Z));
                    minVelo /= 2;

                    Bees.MinPos = startPos;
                    Bees.MinVelocity = minVelo;
                    Bees.WithTerrainCollision = true;
                    Api.World.SpawnParticles(Bees);
                }
            }
            harvestBase = (float)(((float)FGCServerConfig.Current.LangstrothDaysToHarvestIn30DayMonths * (float)(Api.World.Calendar.DaysPerMonth/30f)) * (float)Api.World.Calendar.HoursPerDay);
        }

        public void ResetHive()
        {
            bottomStack.harvestableAtTotalHours = 0;
            bottomStack.quantityNearbyFlowers = 0;
            bottomStack._hivePopSize = 0;
        }

        private void TestHarvestable(float dt)
        {
            UpdateClimateValues(Api);
            bottomStack = GetBottomStack();
            bottomStack.CountFrames();
            if (_isActiveHive && (Pos == bottomStack.Pos))
            {

                double worldTime = Api.World.Calendar.TotalHours;
                
                if (conds == null) return;

                double distance = Math.Abs((conds.Temperature + (roomness > 0 ? 5 : 0)) - optimalTemp); //The roomness is added to account for the presence of a greenhouse
                double range = Math.Max(maxTemp - optimalTemp, optimalTemp - minTemp);
                float beeParticleModifier = 1f - (float)(distance / range);
                _activityLevel = GameMath.Clamp(beeParticleModifier, 0f, 1f);

                bool tempOutOfRange = (threeDayTemp < minTemp || threeDayTemp > maxTemp);

                if(cooldownUntilTotalHours <= 0) {
                    cooldownUntilTotalHours = worldTime + 8;
                }


                if (HivePopSize > 0 && !tempOutOfRange){
                    handleCropCharges(worldTime);
                }

                if (harvestableAtTotalHours == 0 && _hivePopSize > EnumHivePopSize.Poor && bottomStack.linedFrames>0)
                {
                    harvestableAtTotalHours = worldTime + HarvestableTime();
                }
                else if (worldTime > harvestableAtTotalHours && _hivePopSize > EnumHivePopSize.Poor && bottomStack.linedFrames > 0)
                {
                    Random rand = new();
                    int framesToFill = 0;
                    //Determine how many times to run the frameFillCycle, ensure resulting value w
                    int frameFillCycles = 1+(int)Math.Floor(((worldTime - harvestableAtTotalHours)/harvestBase));
                    //determine number of days per cycle based on worldConfig for daysPerMonth and FGC Config for length of the harvest cycle.
                    float daysPerCycle = ((float)FGCServerConfig.Current.LangstrothDaysToHarvestIn30DayMonths * (float)(Api.World.Calendar.DaysPerMonth / 30f));

                    while (frameFillCycles >= 1)
                    {
                        float CycleFirstDay = (frameFillCycles * daysPerCycle);
                        float CycleLastDay = CycleFirstDay-daysPerCycle;
                        float tempFirstDay = (Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, (Double)((int)(Api.World.Calendar.TotalDays-CycleFirstDay)) + 0.66f).Temperature);
                        float tempLastDay = (Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, (Double)((int)(Api.World.Calendar.TotalDays - CycleLastDay)) + 0.66f).Temperature);
                        frameFillCycles--;
                        if ((tempFirstDay + tempLastDay) / 2 < minTemp || (tempFirstDay + tempLastDay) / 2 > maxTemp) continue;
                        framesToFill += Math.Max(1, rand.Next(FGCServerConfig.Current.minFramePerCycle, FGCServerConfig.Current.maxFramePerCycle) * ((int)this.HivePopSize / 2));
                    }
                    GetTopStack().UpdateFrames(framesToFill);
                    if ((threeDayTemp < minTemp || threeDayTemp > maxTemp) && quantityNearbyFlowers != 0)
                    {
                        harvestableAtTotalHours = worldTime + HarvestableTime();
                        cooldownUntilTotalHours = worldTime + 8;
                    }
                    else
                    {
                       //Determine remaining time until the next frame fill cycle.
                       harvestableAtTotalHours = worldTime + (harvestBase - ((worldTime - harvestableAtTotalHours) % harvestBase));
                    }
                    CountFrames();
                }
                MarkDirty();
            }
        }


        
        private void handleCropCharges(double worldTime)
        {
            if (worldTime > cropChargeAtTotalHours && cropcharges < maxCropCharges && _hivePopSize != EnumHivePopSize.Poor && quantityNearbyFlowers > 0)
            {
                if (cropChargeAtTotalHours != 0)
                {
                    int cropchargebase = (int)Math.Max(1, (Math.Round((worldTime - cropChargeAtTotalHours) / (float)(Api.World.Calendar.HoursPerDay * (float)(Api.World.Calendar.DaysPerMonth / 30f)))));
                    int cropchargegrowth = (int)Math.Max(1, ((cropchargebase * chargesPerDay) * (int)_hivePopSize));
                    cropcharges = (int)Math.Min(cropchargegrowth + cropcharges, maxCropCharges);
                }
                cropChargeAtTotalHours = worldTime + cropChargeGrowthHours;
            }
        }
        private double HarvestableTime()
        {
                Random rand = new();
                double harvestTime = (harvestBase * .75f) + ((harvestBase * .5) * rand.NextDouble());
                return harvestTime;
        }


        private void OnScanForFlowers(float dt)
        {
            double worldTime = Api.World.Calendar.TotalHours;

            if (_isActiveHive)
            {

                Room room = roomreg?.GetRoomForPosition(Pos);
                roomness = (room != null && room.SkylightCount > room.NonSkylightCount && room.ExitCount == 0) ? 1 : 0;

                if (_activityLevel <= 0) return;
                if (Api.Side == EnumAppSide.Client) return;
                if (Api.World.Calendar.TotalHours < cooldownUntilTotalHours) return;
                if (scanIteration == 0)
                {
                    scanQuantityNearbyFlowers = 0;
                    scanQuantityNearbyHives = 0;
                }

                int minX = -8 + 8 * (scanIteration / 2);
                int minZ = -8 + 8 * (scanIteration % 2);
                int size = 8;

                Api.World.BlockAccessor.WalkBlocks(Pos.AddCopy(minX, -5, minZ), Pos.AddCopy(minX + size - 1, 5, minZ + size - 1), (block, posx, posy, posz) =>
                {
                    BlockPos curPos = new (posx, posy, posz);
                    BlockEntity curBE = Api.World.BlockAccessor.GetBlockEntity(curPos);
                    if (block.Id == 0 || (roomness > 0 && !room.Contains(new BlockPos(posx, posy, posz)))) return;

                    if (block.Attributes != null && block.Attributes.IsTrue("beeFeed"))
                    {
                        scanQuantityNearbyFlowers++;
                    }
                    else if (block.Code.FirstCodePart() == "langstrothstack" && curBE is BELangstrothStack langstroth)
                    {
                        if (bottomStack.Pos == curPos
                        && langstroth.isHiveActive())
                            scanQuantityNearbyHives++;
                    }
                    else if (block.Code.FirstCodePart() == "ceramicbroodpot" && curBE is BECeramicBroodPot ceramic)
                    {
                        if (block.Code.FirstCodePart() == "skep" && block.Code.SecondCodePart() == "populated")
                        {
                            scanQuantityNearbyHives++;
                        }
                    }
                    else if (block.Code.FirstCodePart() == "wildhive")
                    {
                        scanQuantityNearbyHives++;
                    }
                });
                scanIteration++;
                System.Diagnostics.Debug.WriteLine("Scan Iteration is " + scanIteration);
                if (scanIteration == 4)
                {
                    scanIteration = 0;
                    OnScanComplete();
                }
                MarkDirty(true);
            }
        }

        private void OnScanComplete()
        {
            quantityNearbyFlowers = scanQuantityNearbyFlowers;
            quantityNearbyHives = scanQuantityNearbyHives;
            _hivePopSize = (EnumHivePopSize)GameMath.Clamp(quantityNearbyFlowers - FGCServerConfig.Current.minFlowersPerHive * quantityNearbyHives, 0, 2);
            MarkDirty();
        }

//Misc Methods
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("scanIteration", scanIteration);
            tree.SetInt("harvestableFrames", harvestableFrames);
            tree.SetInt("linedFrames", linedFrames);
            tree.SetInt("totalFrames", totalFrames);
            tree.SetInt("quantityNearbyFlowers", quantityNearbyFlowers);
            tree.SetInt("quantityNearbyHives", quantityNearbyHives);
            tree.SetBool("activeHive", _isActiveHive);
            tree.SetInt("scanQuantityNearbyFlowers", scanQuantityNearbyFlowers);
            tree.SetInt("scanQuantityNearbyHives", scanQuantityNearbyHives);

            tree.SetDouble("cooldownUntilTotalHours", cooldownUntilTotalHours);
            tree.SetDouble("harvestableAtTotalHours", harvestableAtTotalHours);
            tree.SetInt("hiveHealth", (int)_hivePopSize);
            tree.SetFloat("roomness", roomness);            
            tree.SetDouble("cropChargeAtTotalHours", cropChargeAtTotalHours);
            tree.SetInt("cropCharges", cropcharges);
            tree.SetInt("maxCropCharges", maxCropCharges);
            tree.SetFloat("todayNoonTemp", todayNoonTemp);
            tree.SetFloat("twoDayAgoNoonTemp", twoDayAgoNoonTemp);
            tree.SetFloat("threeDayTemp", threeDayTemp);
            tree.SetInt("daysTillHarvest", daysTillHarvest);
            tree.SetDouble("worldTime", worldTime);
            tree.SetFloat("minTemp", minTemp);
            tree.SetFloat("maxTemp", maxTemp);
            tree.SetFloat("optimalTemp", optimalTemp);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            scanIteration = tree.GetInt("scanIteration");
            harvestableFrames = tree.GetInt("harvestableFrames");
            linedFrames = tree.GetInt("linedFrames");
            totalFrames = tree.GetInt("totalFrames");
            quantityNearbyFlowers = tree.GetInt("quantityNearbyFlowers");
            quantityNearbyHives = tree.GetInt("quantityNearbyHives");
            _isActiveHive = tree.GetBool("activeHive");
            if (_isActiveHive && worldForResolving.Api.Side.IsServer())
            {
                worldForResolving.Api.ModLoader.GetModSystem<FromGoldenCombs>().OnPollination += OnPollinationNearby;
                
            }
            scanQuantityNearbyFlowers = tree.GetInt("scanQuantityNearbyFlowers");
            scanQuantityNearbyHives = tree.GetInt("scanQuantityNearbyHives");
            cooldownUntilTotalHours = tree.GetDouble("cooldownUntilTotalHours");
            harvestableAtTotalHours = tree.GetDouble("harvestableAtTotalHours");
            _hivePopSize = (EnumHivePopSize)tree.GetInt("hiveHealth");
            roomness = tree.GetFloat("roomness");
            cropChargeAtTotalHours = tree.GetDouble("cropChargeAtTotalHours");
            cropcharges = tree.GetInt("cropCharges");
            maxCropCharges = tree.GetInt("maxCropCharges");
            todayNoonTemp = tree.GetFloat("todayNoonTemp");
            twoDayAgoNoonTemp = tree.GetFloat("twoDayAgoNoonTemp");
            threeDayTemp = tree.GetFloat("threeDayTemp");
            daysTillHarvest = tree.GetInt("daysTillHarvest");
            worldTime = tree.GetDouble("worldTime");
            minTemp = tree.GetFloat("minTemp");
            maxTemp = tree.GetFloat("maxTemp");
            optimalTemp = tree.GetFloat("optimalTemp");

            updateMeshes();
        }
        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            if (Pos != bottomStack.Pos)
            {
                bottomStack.GetBlockInfo(forPlayer, sb);
            }
            else
            {
                string tempReport = Lang.Get("fromgoldencombs:3DayTemp") + " " + (threeDayTemp > maxTemp ? Lang.Get("fromgoldencombs:3DayTooHot") : threeDayTemp < minTemp ? Lang.Get("fromgoldencombs:3DayTooCold") : Lang.Get("fromgoldencombs:3DayPerfect"));               
                string hiveState = Lang.Get("fromgoldencombs:nearbyflowers", bottomStack.quantityNearbyFlowers, Lang.Get("population-"+bottomStack._hivePopSize.ToString()));
                
                if (bottomStack._isActiveHive)
                {
                    
                    sb.AppendLine(hiveState);
                    sb.AppendLine();
                    if (curTemp < minTemp)
                    {
                        sb.AppendLine(Lang.Get("fromgoldencombs:toocold"));
                    }
                    else if (curTemp > maxTemp)
                    {
                        sb.AppendLine(Lang.Get("fromgoldencombs:toohot"));
                    }
                    else if ((bottomStack.harvestableAtTotalHours - worldTime / 24) > 0 && bottomStack.linedFrames > 0)
                    {
                        string combPopTime;
                        if (FGCServerConfig.Current.showcombpoptime)
                        {
                            combPopTime = Lang.Get("fromgoldencombs:newframepop") + (daysTillHarvest < 1 ? Lang.Get("fromgoldencombs:lessthanday") : daysTillHarvest + " " + Lang.Get("fromgoldencombs:days"));
                        }
                        else
                        {
                            combPopTime = Lang.Get("fromgoldencombs:outgathering");
                        }
                        sb.AppendLine(combPopTime);
                    }
                    else
                    {
                        sb.AppendLine(Lang.Get("fromgoldencombs:findflowers"));
                    }
                    if (this.roomness > 0f)
                    {
                        sb.AppendLine(Lang.Get("greenhousetempbonus", Array.Empty<object>()));
                    }

                    if (FGCServerConfig.Current.showExtraBeehiveInfo && (forPlayer.Entity.Controls.ShiftKey || FGCClientConfig.Current.alwaysShowExtraBeehiveInfo == true))
                    {
                        sb.AppendLine(tempReport);
                        sb.AppendLine(Lang.Get("fromgoldencombs:croprange") + " " + cropChargeRange);
                        sb.AppendLine(Lang.Get("fromgoldencombs:cropcharges") + " " + cropcharges);
                        sb.AppendLine(Lang.Get("fromgoldencombs:cropboostpercentage") + " " + Math.Round(FGCServerConfig.Current.langstrothCropBoostPercentage * 100) + "%");
                        
                    }
                    if (Api is ICoreClientAPI capi && capi.Settings.Bool.Get("extendedDebugInfo", false))
                    {
                        sb.AppendLine("Current Time: " + (int)Api.World.Calendar.TotalHours);
                        sb.AppendLine("coolDownUntilTotalHours: " + (int)cooldownUntilTotalHours);
                        sb.AppendLine("ScanInteration " + scanIteration);
                    }
                }

                if (bottomStack._isActiveHive && bottomStack.linedFrames <= 0)
                {
                    if (bottomStack.harvestableFrames == bottomStack.totalFrames)
                    {
                        sb.AppendLine();
                        sb.AppendLine(Lang.Get("fromgoldencombs:allframesfilled"));
                    }
                    else
                    {
                        sb.AppendLine();
                        sb.AppendLine(Lang.Get("fromgoldencombs:nofillableframes"));
                    }
                    return;
                }

                sb.AppendLine("");
                if (bottomStack.linedFrames != 0) {  sb.AppendLine(Lang.Get("fromgoldencombs:linedframes") + " " + bottomStack.linedFrames); }
                if (bottomStack.harvestableFrames != 0) {  sb.AppendLine(Lang.Get("fromgoldencombs:harvestableframes") + " " + bottomStack.harvestableFrames); 
                }
            } 
        }

        readonly Matrixf mat = new();

        protected override float[][] genTransformationMatrices()
        {
                float[][] tfMatrices = new float[3][];
                for (int index = 0; index <= 2; index++)
                {
                    float x = 0;
                    float z = 0;
                    switch (this.Block.Variant["side"])
                    {
                        case "east": x = 0; break;
                        case "west": x = 1; z = 1; break;
                        case "north": z = 1; break;
                        case "south": x = 1; break;
                    }
                    tfMatrices[index] = new Matrixf().Translate(x, 0.3333f * index, z).RotateYDeg(this.Block.Shape.rotateY).Values; 
                }
                return tfMatrices;
        }
        
        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            if (Api?.Side == EnumAppSide.Server)
            {
                Api.ModLoader.GetModSystem<FromGoldenCombs>().OnPollination -= OnPollinationNearby;
            }
        }

    }
}
