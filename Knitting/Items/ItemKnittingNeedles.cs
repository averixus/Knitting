using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Net;

namespace Knitting.Items
{
    public class ItemKnittingNeedles : Item
    {
        private const int TWINE_PER_CLOTH = 4;
        private const float SECONDS_PER_CLOTH = 4.0f;

        private const float SECONDS_PER_CLOTH_SITTING = 3.0f;
        private static Dictionary<CollectibleObject, CollectibleObject> CLOTH_OUTPUTS = new Dictionary<CollectibleObject, CollectibleObject>();

        private ILoadedSound knittingSound;

        public static void MapOutputs(ICoreAPI api)
        {
            if (api.ModLoader.IsModEnabled("wool")) {
                
                string[] types = ["mordant", "plain", "black", "blue", "brown",
                    "gray", "green", "orange", "pink", "purple", "red", "white",
                    "yellow", "darkblue", "darkbrown", "darkgreen", "darkred"];

                foreach (string type in types) {
                    
                    CLOTH_OUTPUTS.Add(api.World.GetItem(new AssetLocation("wool:twine-wool-" + type)),
                        api.World.GetBlock(new AssetLocation("wool:wool-" + type)));
                }
            }

            if (api.ModLoader.IsModEnabled("tailorsdelight"))
            {
                string[] vstypes = ["black", "blue", "brown", "gray", "green",
                    "orange", "pink", "purple", "red", "white", "yellow"];

                foreach (string vstype in vstypes)
                {
                    CLOTH_OUTPUTS.Add(api.World.GetItem(new AssetLocation("tailorsdelight:twine-" + vstype)),
                        api.World.GetItem(new AssetLocation("game:cloth-" + vstype)));
                }

                string[] tdtypes = ["darkblue", "darkbrown", "darkgreen", "darkred"];

                foreach (string tdtype in tdtypes)
                {
                    CLOTH_OUTPUTS.Add(api.World.GetItem(new AssetLocation("tailorsdelight:twine-" + tdtype)),
                        api.World.GetItem(new AssetLocation("tailorsdelight:cloth-" + tdtype)));
                }
            }
            
            CLOTH_OUTPUTS.Add(api.World.GetItem(new AssetLocation("game:flaxtwine")),
                api.World.GetBlock(new AssetLocation("game:linen-normal-down")));
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {

            // Do nothing if invalid player
            IPlayer player = (byEntity as EntityPlayer)?.Player;
            if (player == null) return;

            // Attempt ground storage
            if (byEntity.Controls.ShiftKey)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                return;
            }

            // Check for enough knittable twine
            if (!CanKnit(byEntity.LeftHandItemSlot))
            {
                if (api.Side == EnumAppSide.Client)
                {
                    (api as ICoreClientAPI).TriggerIngameError(this, "notwine", 
                        Lang.Get("knitting:knitting-need-twine"));
                }
                byEntity.StopAnimation("startfire");
                return;

            } else {
                       
                // Start knitting
                handHandling = EnumHandHandling.PreventDefaultAction;
                byEntity.StartAnimation("startfire");

                // Start sound
                if (api.Side == EnumAppSide.Client)
                {
                    knittingSound?.Stop();
                    knittingSound?.Dispose();

                    knittingSound = (api as ICoreClientAPI).World.LoadSound(new SoundParams()
                    {
                        Location = new AssetLocation("knitting:sounds/knitting"),
                        ShouldLoop = false,
                        Position = byEntity.Pos.XYZ.ToVec3f(),
                        DisposeOnFinish = true,
                        Volume = 0.5f,
                        Range = 8
                    });
                    knittingSound?.Start();
                }
            }
        }


        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot,
            EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (CanKnit(byEntity.LeftHandItemSlot))
            {
                return secondsUsed < GetKnitTime(byEntity as EntityPlayer);
            } 
            else 
            {
                if (api.Side == EnumAppSide.Client)
                {
                    (api as ICoreClientAPI).TriggerIngameError(this, "notwine", 
                        Lang.Get("knitting:knitting-need-twine"));
                    knittingSound?.Stop();
                    knittingSound?.Dispose();
                    knittingSound = null;
                }
                byEntity.StopAnimation("startfire");
                return false;
            }
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot needlesSlot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {

            // Do nothing if stopped early
            if (secondsUsed < GetKnitTime(byEntity as EntityPlayer)) return;

            // Do nothing if invalid player
            IPlayer player = (byEntity as EntityPlayer)?.Player;
            if (player == null) return;

            // Do nothing if not enough knittable twine
            ItemSlot twineSlot = byEntity.LeftHandItemSlot;
            if (!CanKnit(twineSlot)) return;

            // Only process on server side
            if (api.Side != EnumAppSide.Server) return;

            // Damage needles
            DamageItem(api.World, byEntity, needlesSlot, 1);
            needlesSlot.MarkDirty();
        
            // Make cloth
            CollectibleObject clothItem = CLOTH_OUTPUTS.Get(twineSlot.Itemstack.Item);
            if (clothItem == null) return;
            ItemStack clothStack = new ItemStack(clothItem, 1);

            // Consume twine
            twineSlot.TakeOut(TWINE_PER_CLOTH);
            twineSlot.MarkDirty();

            // Try to give cloth to player
            if (!player.InventoryManager.TryGiveItemstack(clothStack))
            {
                // Drop if inventory full
                api.World.SpawnItemEntity(clothStack, player.Entity.Pos.XYZ);
            }

            // Stop animation
            byEntity.StopAnimation("startfire");
        }

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            // Stop sound
            if (api.Side == EnumAppSide.Client)
            {
                knittingSound?.Stop();
                knittingSound?.Dispose();
                knittingSound = null;
            }
            byEntity.StopAnimation("startfire");
            return true;
        }
        

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {

            // Get example knittable items for the interaction help
            var items = api.World.Collectibles;
            var knittableStacks = new System.Collections.Generic.List<ItemStack>();

            foreach (var collectible in CLOTH_OUTPUTS.Keys)
            {
                knittableStacks.Add(new ItemStack(collectible));
                if (knittableStacks.Count >= 3) break; // Limit to 3 examples
            }

            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "knitting:knit",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = [.. knittableStacks]
                }
            };
        }

        private static bool CanKnit(ItemSlot twineSlot)
        {
            if (twineSlot != null && !twineSlot.Empty) {

                ItemStack twine = twineSlot.Itemstack;
                if (CLOTH_OUTPUTS.ContainsKey(twine.Item))
                {
                        return twine.StackSize >= TWINE_PER_CLOTH;
                }
            }
            return false;
        }

        private static float GetKnitTime(EntityPlayer player)
        {
               return (player.Controls.FloorSitting) ? SECONDS_PER_CLOTH_SITTING : SECONDS_PER_CLOTH;
        }
    }
}