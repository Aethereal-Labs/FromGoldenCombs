using FromGoldenCombs.BlockBehaviors;
using FromGoldenCombs.Util.Config;
using FromGoldenCombs.Util.Config;
using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace FromGoldenCombs.BlockEntities
{
    class BECeramicBroodPot : BlockEntityDisplay
    {
        double harvestableAtTotalHours;
        double cooldownUntilTotalHours;
        
        int quantityNearbyFlowers;
        int quantityNearbyHives;
        float _activityLevel;
        RoomRegistry roomreg;
        int roomness;
        public static SimpleParticleProperties Bees;
        int scanQuantityNearbyFlowers;
        int scanQuantityNearbyHives;
        int scanIteration;
        public bool isActiveHive = false;
        double cropChargeGrowthHours = 24;
        double chargesPerDay = FGCServerConfig.Current.ceramicBaseChargesPerDay; //Number of hours until the hive accumulates a new grow charge.
        double cropChargeAtTotalHours;
        int cropcharges;
        int maxCropCharges = FGCServerConfig.Current.ceramicMaxCropCharges;
        int cropChargeRange = FGCServerConfig.Current.ceramicCropRange;
        long scanForFlowersListener;
        long testHarvestableListener;
        long beeParticleListener;
        float harvestBase;
        EnumHivePopSize _hivePopSize;

        //TODO: Implement Config Option To Set AllowUndergroundApiculture.
        bool AllowUndergroundApiculture = false;

        public readonly InventoryGeneric inv;
        public override InventoryBase Inventory => inv;

        public EnumHivePopSize HivePopSize { 
            get { return _hivePopSize; } 
        }

        public float ActivityLevel
        {
            get { return _activityLevel; }
        }
        public override string InventoryClassName => "ceramicbroodpot";

        public BECeramicBroodPot()
        {
            inv = new InventoryGeneric(1, "hivepot-slot", null, null);
        }

        static BECeramicBroodPot()
        {
            Bees = new SimpleParticleProperties(
                1, 1,
                ColorUtil.ToRgba(255, 215, 156, 65),
                new Vec3d(), new Vec3d(),
                new Vec3f(0, 0, 0),
                new Vec3f(0, 0, 0),
                1f,
                0f,
                0.5f, 0.5f,
                EnumParticleModel.Cube
            );
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (Api.Side.IsServer() && isActiveHive)
            {
                Api.ModLoader.GetModSystem<FromGoldenCombs>().OnPollination += OnPollinationNearby;
                if (testHarvestableListener == 0)
                {
                    testHarvestableListener = RegisterGameTickListener(TestHarvestable, 5000);
                    scanForFlowersListener = RegisterGameTickListener(OnScanForFlowers, Api.World.Rand.Next(5000) + 30000);
                    cooldownUntilTotalHours = Api.World.Calendar.TotalHours + 8;
                }
                MarkDirty();
            }

            //TODO: Implement Config Option To Set This Value.
            this.AllowUndergroundApiculture = this.AllowUndergroundApiculture;
            //PushEventOnBlockBroken
            roomreg = Api.ModLoader.GetModSystem<RoomRegistry>();

            if (api.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = api as ICoreClientAPI;
                Shape shape = capi.Assets.TryGet(new AssetLocation("fromgoldencombs", "shapes/block/hive/ceramic/ceramicbroodpot.json")).ToObject<Shape>();

                if (api.Side == EnumAppSide.Client)
                {
                    beeParticleListener = RegisterGameTickListener(SpawnBeeParticles, 300);
                }
            }
            
            harvestBase = (FGCServerConfig.Current.ClayPotDaysToHarvestIn30DayMonths * (Api.World.Calendar.DaysPerMonth/ 30f)) * api.World.Calendar.HoursPerDay;
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            
        }

        public override void OnBlockRemoved()
        {
            if (Api.Side == EnumAppSide.Server)
            {
                Api.ModLoader.GetModSystem<FromGoldenCombs>().OnPollination -= OnPollinationNearby;
            }
            base.OnBlockRemoved();
        }
        public void SetHiveSize(int size)
        {
            _hivePopSize = (EnumHivePopSize)size;
        }

        public bool OnInteract(IPlayer byPlayer)
        {
            Block hive = Api.World.BlockAccessor.GetBlock(Pos, 0);
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot.Empty)
            {
                if (TryTake(byPlayer))
                {
                    MarkDirty(true);
                    updateMeshes();
                    return true;
                }
            }
            else if (slot.Itemstack.Collectible.WildCardMatch(new AssetLocation("game", "skep-*-populated-*")) && !isActiveHive)
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.TakeOutWhole();
                testHarvestableListener = RegisterGameTickListener(TestHarvestable, 5000);
                scanForFlowersListener = RegisterGameTickListener(OnScanForFlowers, Api.World.Rand.Next(5000) + 30000);
                isActiveHive = true;
                Api.ModLoader.GetModSystem<FromGoldenCombs>().OnPollination += OnPollinationNearby;
                updateMeshes();
                return true;
            }
            else if (slot.Itemstack.Collectible.WildCardMatch(new AssetLocation("game", "skep-*-empty-*")) && isActiveHive)
            {
                ItemStack newStack = new ItemStack(Api.World.BlockAccessor.GetBlock(slot.Itemstack.Collectible.CodeWithVariant("type","populated")));
                
                if (byPlayer.InventoryManager.TryGiveItemstack(newStack))
                {
                    Api.ModLoader.GetModSystem<FromGoldenCombs>().OnPollination -= OnPollinationNearby;
                    byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
                    resetHive();
                };
                MarkDirty();
                return true;

            }
            else if (TryPut(slot))
            {
                {
                    Api.World.BlockAccessor.ExchangeBlock(Api.World.BlockAccessor.GetBlock(hive.CodeWithVariant("top", "withtop")).BlockId, Pos);
                    MarkDirty(true);
                }
                updateMeshes();
                return true; //This prevents TryPlaceBlock from passing if TryPut fails.
            }
            return false;

        }

        private void resetHive()
        {
            isActiveHive = false;
            cropcharges = 0;
            harvestableAtTotalHours = 0;
            cooldownUntilTotalHours = 0;
            int quantityNearbyFlowers = 0;
            int quantityNearbyHives = 0;
            float _activityLevel = 0;
            int scanQuantityNearbyFlowers = 0;
            int scanQuantityNearbyHives = 0;
            int scanIteration = 0;
            double cropChargeAtTotalHours = 0;
            double cooldownUntilCropCharge = 0;
            float harvestBase;
            EnumHivePopSize _hivePopSize = 0;
            UnregisterAllTickListeners();
            Api.ModLoader.GetModSystem<FromGoldenCombs>().OnPollination -= OnPollinationNearby;
            this.MarkDirty(true);
        }

        private bool TryTake(IPlayer player)
        {
                ItemSlot activeHotbarSlot = player.InventoryManager.ActiveHotbarSlot;
                BlockContainer blockContainer = this.Api.World.BlockAccessor.GetBlock(Pos, 0) as BlockContainer;
                int index = 0;
            if (!inv[index].Empty)
            {

                ItemStack stack = inv[0].TakeOut(1);
                player.InventoryManager.TryGiveItemstack(stack, false);
                if (stack.StackSize > 0)
                {
                    this.Api.World.SpawnItemEntity(stack, this.Pos, null);
                }
                Api.World.BlockAccessor.ExchangeBlock(Api.World.BlockAccessor.GetBlock(this.Block.CodeWithVariant("top", "notop")).BlockId, Pos);
                return true;
            }
            else if (activeHotbarSlot.Empty)
            {
                ItemStack stack = blockContainer.OnPickBlock(this.Api.World, Pos);
                SetAttributesOnPickup(stack);

                if (player.InventoryManager.TryGiveItemstack(stack.Clone(), true))
                {

                    Api.World.BlockAccessor.SetBlock(0, Pos);
                    BlockEntity blockentity = Api.World.BlockAccessor.GetBlockEntity(Pos);
                    return true;
                }
            }
            return false;
        }

        public void TryPutDirect(ItemStack stack)
        {
            int index = 0;
            if (inv[index].Empty
               && stack.Block.FirstCodePart() == "hivetop" && stack.Block.Variant["type"] != "raw")
            {
                inv[index].Itemstack = stack;
            }

        }

        private bool TryPut(ItemSlot slot)
        {
            int index = 0;
            if (inv[index].Empty
                && slot.Itemstack.Collectible.FirstCodePart() == "hivetop" && slot.Itemstack.Collectible.Variant["type"] != "raw")
            {
                if (slot.Itemstack.Collectible.Code == "fromgoldencombs:hivetop-empty")
                {
                    slot.Itemstack = new(Api.World.GetBlock(new AssetLocation("fromgoldencombs:hivetop-blue-fired")), slot.Itemstack.StackSize);
                    slot.TryPutInto(Api.World, inv[index]);
                    slot.MarkDirty();
                } else if(slot.Itemstack.Collectible.Code == "fromgoldencombs:hivetop-harvestable")
                {
                        slot.Itemstack = new(Api.World.GetBlock(new AssetLocation("fromgoldencombs:hivetop-blue-harvestable")), slot.Itemstack.StackSize);
                        slot.TryPutInto(Api.World, inv[index]);
                        slot.MarkDirty();
                } else
                {
                    slot.TryPutInto(Api.World, inv[index]);
                }

                if (inv[index].Itemstack.Block.Variant["type"] != "raw" && isActiveHive)
                {
                    cooldownUntilTotalHours = Api.World.Calendar.TotalHours + 8;
                }
                return true;
            }
            return false;
        }

        public virtual void SetAttributesOnPickup(ItemStack hiveStack)
        {
            hiveStack.Attributes.SetInt("scanIteration", scanIteration);
            hiveStack.Attributes.SetInt("quantityNearbyFlowers", 0);
            hiveStack.Attributes.SetInt("quantityNearbyHives", 0);
            hiveStack.Attributes.SetInt("scanQuantityNearbyFlowers", 0);
            hiveStack.Attributes.SetInt("scanQuantityNearbyHives", 0);
            hiveStack.Attributes.SetBool("isactivehive", isActiveHive);
            hiveStack.Attributes.SetDouble("cooldownUntilTotalHours", 0);
            hiveStack.Attributes.SetDouble("harvestableAtTotalHours", 0);
            hiveStack.Attributes.SetInt("hiveHealth", (int)_hivePopSize);
            hiveStack.Attributes.SetFloat("roomness", 0.0f);
            hiveStack.Attributes.SetInt("cropcharges", 0);
        }
        //Rendering Processes
        readonly Matrixf mat = new();

        public override void updateMeshes()
        {
            mat.Identity();
            mat.RotateYDeg(this.Block.Shape.rotateY);

            base.updateMeshes();
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            mat.Identity();
            return base.OnTesselation(mesher, tessThreadTesselator);

        }

        public void TestHarvestable(float dt)
        {
                       if (this.Block.Code == "fromgoldencombs:ceramicbroodpot-withtop" || this.Block.Code == "fromgoldencombs:ceramicbroodpot-notop")
            {
                if (Api.Side.IsServer())
                {

                    Api.World.BlockAccessor.ExchangeBlock(Api.World.BlockAccessor.GetBlock(new AssetLocation("fromgoldencombs:ceramicbroodpot-blue-" + this.Block.LastCodePart().ToString())).Id, this.Pos);
                    this.MarkDirty(true);
                }
            }
            Random randy = new Random();

            bool hasEmptyHivetop = !inv[0].Empty && (inv[0]?.Itemstack?.Block.Variant["type"] == "empty" || inv[0]?.Itemstack?.Block.Variant["type"] == "fired");
            float minTemp = FGCServerConfig.Current.CeramicHiveMinTemp;
            float maxTemp = FGCServerConfig.Current.CeramicHiveMaxTemp == 0 ? 37f : FGCServerConfig.Current.CeramicHiveMaxTemp;
            double worldTime = Api.World.Calendar.TotalHours;
            ClimateCondition conds = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.NowValues);
            float todayNoonTemp = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, (Double)((int)(Api.World.Calendar.TotalDays)) + 0.66f).Temperature;
            float yesterdayNoonTemp = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, (Double)((int)(Api.World.Calendar.TotalDays - 1)) + 0.66f).Temperature;
            float twoDayAgoNoonTemp = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, (Double)((int)(Api.World.Calendar.TotalDays - 2)) + 0.66f).Temperature;
            if (conds == null) return;
            float threeDayTemp = (todayNoonTemp * 2 + yesterdayNoonTemp + twoDayAgoNoonTemp) / 4 + GetGreenhouseBonus();
            float optimalTemp = (maxTemp + minTemp) / 2;
            double distance = Math.Abs(conds.Temperature + GetGreenhouseBonus() - optimalTemp);
            double range = Math.Max(maxTemp - optimalTemp, optimalTemp - minTemp);
            float beeParticleModifier = 1f - (float)(distance / range);
            _activityLevel = GameMath.Clamp(beeParticleModifier, 0f, 1f);

            if (!isActiveHive) { cropcharges = 0; return; }


            bool tempOutOfRange = false;

            if ((threeDayTemp < minTemp || threeDayTemp > maxTemp))
            {
                harvestableAtTotalHours = worldTime + HarvestableTime(harvestBase);
                cooldownUntilTotalHours = worldTime + 8;
                tempOutOfRange = true;
            }

            if (HivePopSize > 0 && !tempOutOfRange)
            {
                handleCropCharges(worldTime);
            }

            if (worldTime > cooldownUntilTotalHours && hasEmptyHivetop && quantityNearbyFlowers>0)
            {
                if (harvestableAtTotalHours == 0 && _hivePopSize > EnumHivePopSize.Poor)
                {
                    harvestableAtTotalHours = worldTime + HarvestableTime(harvestBase);
                }
                else if (worldTime > harvestableAtTotalHours && _hivePopSize > EnumHivePopSize.Poor)
                {
                    inv[0].Itemstack = new ItemStack(Api.World.GetBlock(inv[0]?.Itemstack?.Collectible.CodeWithVariant("type", "harvestable")), 1);
                    harvestableAtTotalHours = 0;
                    cooldownUntilTotalHours = worldTime + 8;
                    updateMeshes();
                }
            }

            if (cooldownUntilTotalHours <= 0 )
            {
                cooldownUntilTotalHours = worldTime + 8;
            }

            MarkDirty(true);
        }

        private void handleCropCharges(double worldTime)
        {
            if (worldTime > cropChargeAtTotalHours && cropcharges < maxCropCharges && _hivePopSize != EnumHivePopSize.Poor && quantityNearbyFlowers > 0)
            {
                if (cropChargeAtTotalHours != 0)
                {
                    int cropchargebase = (int)Math.Max(1,(Math.Round((worldTime - cropChargeAtTotalHours) / (float)(Api.World.Calendar.HoursPerDay * (float)(Api.World.Calendar.DaysPerMonth / 30f))))); ;
                    int cropchargegrowth = (int)Math.Max(1, ((cropchargebase * chargesPerDay) * (int)_hivePopSize));
                    cropcharges = (int)Math.Min(cropchargegrowth + cropcharges, maxCropCharges);
                }
                cropChargeAtTotalHours = worldTime + cropChargeGrowthHours;
            }
        }

        private float GetGreenhouseBonus()
        {
            return roomness > 0 ? 5f : 0f;
        }

        private int CalculateRoomness(Room room)
        {
            return (room != null && room.SkylightCount > room.NonSkylightCount && room.ExitCount == 0) ? 1 : 0;
        }

        private double HarvestableTime(float harvestbase)
        {
            Random rand = new();
            return (harvestBase * .75) + ((harvestBase * .5) * rand.NextDouble());
        }

        readonly Vec3d startPos = new();
        readonly Vec3d endPos = new();
        Vec3f minVelo = new();
                             
        private void SpawnBeeParticles(float dt)
        {
            if (isActiveHive)
            {
                float dayLightStrength = Api.World.Calendar.GetDayLightStrength(Pos.X, Pos.Z);
                if (Api.World.Rand.NextDouble() > (2 * dayLightStrength - 0.5))                     
                    return;

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
        }

        private void OnScanForFlowers(float dt)
        {
            double worldTime = Api.World.Calendar.TotalHours;

            if (isActiveHive)
            {
                Room room = roomreg?.GetRoomForPosition(Pos);
                roomness = CalculateRoomness(room);
                
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
                    BlockPos curPos = new BlockPos(posx, posy, posz);
                    BlockEntity curBE = Api.World.BlockAccessor.GetBlockEntity(curPos);
                    if (block.Id == 0 || (roomness != 0 && !room.Contains(new BlockPos(posx, posy, posz)))) return;

                    if (block.Attributes != null && block.Attributes.IsTrue("beeFeed"))
                    {
                        scanQuantityNearbyFlowers++;
                    }
                    else if (block.Code.FirstCodePart() == "langstrothstack" && curBE is BELangstrothStack langstroth)
                    {
                        if (langstroth.GetBottomStack().Pos == curPos
                        && langstroth.isHiveActive())
                            scanQuantityNearbyHives++;
                    } 
                    else if(block.Code.FirstCodePart() == "ceramicbroodpot" && curBE is BECeramicBroodPot ceramic)
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
            _hivePopSize = (EnumHivePopSize)GameMath.Clamp(quantityNearbyFlowers - FGCServerConfig.Current.minFlowersPerHive * quantityNearbyHives, 0, 2); ;
        }


        #region Pollination Code
        public void OnPollinationNearby(string eventName, BlockPos cropPos, ref EnumHandling handling, IAttribute data)
        {
            if (Api.Side.IsClient()) return;
            TreeAttribute tdata = data as TreeAttribute;
            int deltaX = cropPos.X - Pos.X;
            int deltaY = cropPos.Y - Pos.Y;
            int deltaZ = cropPos.Z - Pos.Z;

            double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ);
            if (isActiveHive && _hivePopSize != EnumHivePopSize.Poor && Api.Side.IsServer())
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

            if (cropcharges < 1 || Api?.World == null || distance >= FGCServerConfig.Current.ceramicCropRange) return;


            Block cropBlock = Api.World.BlockAccessor.GetBlock(cropPos);
            if (cropBlock == null) return;
            if (!cropBlock.HasBehavior<PushEventOnCropBreakBehavior>()) return;

            PushEventOnCropBreakBehavior behavior = cropBlock.GetBehavior<PushEventOnCropBreakBehavior>();
            if (behavior?.validCropStages == null) return;




            if (cropBlock is not BlockCrop crop) return;
            if (Api.World.BlockAccessor.GetBlockEntity(cropPos.DownCopy()) is not BlockEntityFarmland) return;

            // Claim the pollination event so other nearby hives don't all do the same work.
            handling = EnumHandling.PreventSubsequent;

            if (!behavior.validCropStages.Contains<int>(crop.CurrentCropStage)) return;

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
            if(!(bushBlock.FirstCodePart() == "fruitingbush") || !bushBlock.HasBehavior<PushEventOnBlockHarvested>() || bebfb.BState.Growthstate == EnumFruitingBushGrowthState.Ripe) return;

            PushEventOnBlockHarvested eventBehavior = bushBlock.GetBehavior<PushEventOnBlockHarvested>();
            if (eventBehavior == null) return;

            // Claim the pollination event so other nearby hives don't all do the same work.
            handling = EnumHandling.PreventSubsequent;

            eventBehavior.useBeeBoost = true;
            cropcharges--;
            MarkDirty();

        }

        private void manageFruitBoost(BlockPos fruitFoliagePos, double distance, ref EnumHandling handling)
        {

            if (Api?.Side != EnumAppSide.Server || Api?.World == null) return;
            if (cropcharges < 1 || distance >= FGCServerConfig.Current.ceramicCropRange) return;

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

                ItemStack stack = drop.GetNextItemStack(1f + FGCServerConfig.Current.cropBoostPercentage);
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
            MarkDirty();
        }
        #endregion

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetInt("scanIteration", scanIteration);

            tree.SetInt("quantityNearbyFlowers", quantityNearbyFlowers);
            tree.SetInt("quantityNearbyHives", quantityNearbyHives);

            tree.SetInt("scanQuantityNearbyFlowers", scanQuantityNearbyFlowers);
            tree.SetInt("scanQuantityNearbyHives", scanQuantityNearbyHives);
            tree.SetBool("isactivehive", isActiveHive);
            
            tree.SetDouble("cooldownUntilTotalHours", cooldownUntilTotalHours);
            tree.SetDouble("harvestableAtTotalHours", harvestableAtTotalHours);
            tree.SetInt("hiveHealth", (int)_hivePopSize);
            tree.SetInt("roomness", roomness);

            tree.SetDouble("cropChargeAtTotalHours", cropChargeAtTotalHours);
            tree.SetInt("maxCropCharges", maxCropCharges);
            tree.SetInt("cropcharges", cropcharges);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            Api = worldForResolving.Api;
            scanIteration = tree.GetInt("scanIteration");

            quantityNearbyFlowers = tree.GetInt("quantityNearbyFlowers");
            quantityNearbyHives = tree.GetInt("quantityNearbyHives");

            scanQuantityNearbyFlowers = tree.GetInt("scanQuantityNearbyFlowers");
            scanQuantityNearbyHives = tree.GetInt("scanQuantityNearbyHives");

            isActiveHive = tree.GetBool("isactivehive");
             
            
            cooldownUntilTotalHours = tree.GetDouble("cooldownUntilTotalHours");
            harvestableAtTotalHours = tree.GetDouble("harvestableAtTotalHours");
            _hivePopSize = (EnumHivePopSize)tree.GetInt("hiveHealth");
            roomness = tree.GetInt("roomness");
            
            cropChargeAtTotalHours = tree.GetDouble("cropChargeAtTotalHours");
            maxCropCharges = tree.GetInt("maxCropCharges");
            cropcharges = tree.GetInt("cropcharges");

            updateMeshes();

        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            
            float minTemp = FGCServerConfig.Current.CeramicHiveMinTemp;
            float maxTemp = FGCServerConfig.Current.CeramicHiveMaxTemp == 0?37f:FGCServerConfig.Current.CeramicHiveMaxTemp;
            float temp = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.NowValues).Temperature + GetGreenhouseBonus();
            ClimateCondition conds = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.NowValues);
            float todayNoonTemp = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, (Double)((int)(Api.World.Calendar.TotalDays)) + 0.66f).Temperature;
            float yesterdayNoonTemp = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, (Double)((int)(Api.World.Calendar.TotalDays - 1)) + 0.66f).Temperature;
            float twoDayAgoNoonTemp = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, (Double)((int)(Api.World.Calendar.TotalDays - 2)) + 0.66f).Temperature;
            if (conds == null) return;
            float threeDayTemp = (todayNoonTemp * 2 + yesterdayNoonTemp + twoDayAgoNoonTemp) / 4 + GetGreenhouseBonus();
            string tempReport = Lang.Get("fromgoldencombs:3DayTemp") + " " + (threeDayTemp > maxTemp ? Lang.Get("fromgoldencombs:3DayTooHot") : threeDayTemp < minTemp ? Lang.Get("fromgoldencombs:3DayTooCold") : Lang.Get("fromgoldencombs:3DayPerfect"));
            bool isOutTemp = (temp <= minTemp || temp >= maxTemp);

            if (isActiveHive)
            {
                
                double worldTime = Api.World.Calendar.TotalHours;
                int daysTillHarvest = (int)Math.Round((harvestableAtTotalHours - worldTime) / Api.World.Calendar.HoursPerDay);
                daysTillHarvest = daysTillHarvest <= 0 ? 0 : daysTillHarvest;
                
                if (quantityNearbyFlowers > 0) dsc.AppendLine(Lang.Get("fromgoldencombs:nearbyflowers", quantityNearbyFlowers, Lang.Get(("population-" + _hivePopSize.ToString()))));


                if (temp < minTemp)
                {
                    dsc.AppendLine(Lang.Get("fromgoldencombs:toocold"));
                }
                if (temp > maxTemp)
                {
                    dsc.AppendLine(Lang.Get("fromgoldencombs:toohot"));
                }
                if ((harvestableAtTotalHours - worldTime / 24 > 0) && this.Block.Variant["top"] == "withtop" && !isOutTemp)
                {
                    if (FGCServerConfig.Current.showcombpoptime)
                    {
                        dsc.AppendLine(Lang.Get("fromgoldencombs:timetillpop", daysTillHarvest < 1 ? Lang.Get("fromgoldencombs:lessthanday") : (daysTillHarvest + " " + Lang.Get("fromgoldencombs:days"))));
                    }
                }
                else if (isActiveHive && (this.Block.Variant["top"] == "notop"))
                {
                    dsc.AppendLine(Lang.Get("fromgoldencombs:nopot"));

                }
                else if (inv[0]?.Itemstack?.Collectible.Variant["type"] == "harvestable")
                {

                    dsc.AppendLine(Lang.Get("fromgoldencombs:fullpot"));
                }
                else if (quantityNearbyFlowers > 0 && !isOutTemp)
                {
                    dsc.AppendLine(Lang.Get("fromgoldencombs:outgathering"));
                }
                else if (quantityNearbyFlowers <= 0 && !isOutTemp)
                {
                    dsc.AppendLine(Lang.Get("fromgoldencombs:findflowers"));
                }
                if (this.roomness > 0)
                {
                    dsc.AppendLine(Lang.Get("greenhousetempbonus", Array.Empty<object>()));
                }
                if (FGCServerConfig.Current.showExtraBeehiveInfo && (forPlayer.Entity.Controls.ShiftKey || FGCClientConfig.Current.alwaysShowExtraBeehiveInfo == true))
                {
                    dsc.AppendLine(tempReport);
                    dsc.AppendLine(Lang.Get("fromgoldencombs:croprange") + " " + cropChargeRange);
                    dsc.AppendLine(Lang.Get("fromgoldencombs:cropcharges") + " " + cropcharges);
                }
                if (Api is ICoreClientAPI capi && capi.Settings.Bool.Get("extendedDebugInfo", false))
                {
                    dsc.AppendLine("Current Time: " + (int)Api.World.Calendar.TotalHours);
                    dsc.AppendLine("coolDownUntilTotalHours: " + (int)cooldownUntilTotalHours);
                    dsc.AppendLine("ScanInteration " + scanIteration);
                }
            }            
        }

        
        protected override float[][] genTransformationMatrices()
        {
            float[][] tfMatrices = new float[1][];
            for (int index = 0; index < 1; index++)
            {
                ItemStack itemstack = this.Inventory[index].Itemstack;
                if (itemstack != null)
                {
                    tfMatrices[index] = new Matrixf().Translate(0, 1.1f, 1).RotateXDeg(180f).Values;
                }
            }
            return tfMatrices;
        }
    }
}
