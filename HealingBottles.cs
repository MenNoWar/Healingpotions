using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Channels;
using System.Threading;
using System.Timers;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;
using static System.Net.Mime.MediaTypeNames;
using UnityEngine.Assertions;

namespace Hearthstone
{
    [BepInPlugin(HealingBottles.PluginGUID, "HealingBottles", "1.0.3")]
    [BepInDependency(Jotunn.Main.ModGuid, "2.9.0")]
    public class HealingBottles : BaseUnityPlugin
    {
        public const string PluginGUID = "mennowar.mods.HealingBottles";
        Harmony harmony = new Harmony(PluginGUID);

        private static List<string> bottleNames = new List<string>();

        private static DateTime lastHealDateTime = DateTime.MinValue;
        private static DateTime lastStaminaDateTime = DateTime.MinValue;
        private static DateTime lastEitrDateTime = DateTime.MinValue;

        private void Awake()
        {
            harmony.PatchAll();

            PrefabManager.OnVanillaPrefabsAvailable += VanillaPrefabsAvailable;
        }

        private void VanillaPrefabsAvailable()
        {
            // 1st thing: opt out of VanillaPrefabsAvailable:
            PrefabManager.OnVanillaPrefabsAvailable -= VanillaPrefabsAvailable;

            // assetBundle points to the Assets/bottles file. make sure it has Build-Action "embedded Resource":
            var assetBundle = AssetUtils.LoadAssetBundleFromResources("bottles", typeof(HealingBottles).Assembly);

            // some hardcoded prefab names. see bottles.manifest:
            const string healingBottleAssetName0 = "Assets/_Custom/HealingBottle00.prefab";
            const string healingBottleAssetName1 = "Assets/_Custom/HealingBottle01.prefab";
            const string healingBottleAssetName2 = "Assets/_Custom/HealingBottle02.prefab";
            const string healingBottleAssetName3 = "Assets/_Custom/HealingBottle03.prefab";

            const string staminaBottleAssetName0 = "Assets/_Custom/StaminaBottle00.prefab";
            const string staminaBottleAssetName1 = "Assets/_Custom/StaminaBottle01.prefab";
            const string staminaBottleAssetName2 = "Assets/_Custom/StaminaBottle02.prefab";
            const string staminaBottleAssetName3 = "Assets/_Custom/StaminaBottle03.prefab";

            const string manaBottleAssetName0 = "Assets/_Custom/ManaBottle00.prefab";
            const string manaBottleAssetName1 = "Assets/_Custom/ManaBottle01.prefab";
            const string manaBottleAssetName2 = "Assets/_Custom/ManaBottle02.prefab";
            const string manaBottleAssetName3 = "Assets/_Custom/ManaBottle03.prefab";

            // --- REQUIREMENTS ---
            // basic requirement for *every* potion
            var rcBaseAllPotions = new RequirementConfig("Dandelion", 5);

            // requirements for the different qualities:
            var rcSmallPotion = new RequirementConfig("Mushroom", 5);
            var rcMediumPotion = new RequirementConfig("Thistle", 5);
            var rcHugePotion = new RequirementConfig("Obsidian", 5);
            var rcGodlyPotion = new RequirementConfig("Flax", 5);

            # region helper functions:
            // Load the Prefab asset from the bundle and add the requirements for the item ath the cauldron
            ItemConfig LoadPrefab(string prefabName, params RequirementConfig[] requirements)
            {
                var prefab = assetBundle.LoadAsset<GameObject>(prefabName);
                var drop = prefab.GetComponent<ItemDrop>();
                var shared = drop.m_itemData.m_shared;
                bottleNames.Add(drop.gameObject.name);
                
                var result = new ItemConfig
                {
                    Name = shared.m_name,
                    Icons = shared.m_icons,
                    Description = shared.m_description,
                    CraftingStation = "piece_cauldron",
                    MinStationLevel = shared.m_value
                };

                result.Requirements = requirements;

                return result;
            }

            // simple wrapper for one line item creation
            void AddItem(string assetName, params RequirementConfig[] requirements)
            {
                var item = LoadPrefab(assetName, requirements);
                ItemManager.Instance.AddItem(new CustomItem(assetBundle, assetName, true, item));
            }
            #endregion

            #region Healing-Bottles
            // requirement for healing potion:
            var rcHealingPotion = new RequirementConfig("Raspberry", 5);

            AddItem(healingBottleAssetName0, rcBaseAllPotions, rcSmallPotion, rcHealingPotion);
            AddItem(healingBottleAssetName1, rcBaseAllPotions, rcMediumPotion, rcHealingPotion);
            AddItem(healingBottleAssetName2, rcBaseAllPotions, rcHugePotion, rcHealingPotion);
            AddItem(healingBottleAssetName3, rcBaseAllPotions, rcGodlyPotion, rcHealingPotion);
            #endregion

            #region Stamina-Bottles
            // requirement for stamina potion:
            var rcStaminaPotion = new RequirementConfig("Blueberries", 5);
            AddItem(staminaBottleAssetName0, rcBaseAllPotions, rcSmallPotion, rcStaminaPotion);
            AddItem(staminaBottleAssetName1, rcBaseAllPotions, rcMediumPotion, rcStaminaPotion);
            AddItem(staminaBottleAssetName2, rcBaseAllPotions, rcHugePotion, rcStaminaPotion);
            AddItem(staminaBottleAssetName3, rcBaseAllPotions, rcGodlyPotion, rcStaminaPotion);
            #endregion

            #region mana bottles
            // requirement for mana potion:
            var rcManaPotion = new RequirementConfig("MushroomYellow", 5);
            AddItem(manaBottleAssetName0, rcBaseAllPotions, rcSmallPotion, rcManaPotion);
            AddItem(manaBottleAssetName1, rcBaseAllPotions, rcMediumPotion, rcManaPotion);
            AddItem(manaBottleAssetName2, rcBaseAllPotions, rcHugePotion, rcManaPotion);
            AddItem(manaBottleAssetName3, rcBaseAllPotions, rcGodlyPotion, rcManaPotion);
            #endregion
        }

        public static bool Heal(float amount)
        {
            if (Player.m_localPlayer == null)
                return false;
            
            if ((DateTime.Now - lastHealDateTime).TotalSeconds <= 3)
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, "You can't drink this healing potion right now");
                return false;
            }

            var maxHealth = Player.m_localPlayer.GetMaxHealth();
            var healAmount = (maxHealth / 100) * amount; // like 1 * 25%
            var healthAmount = Player.m_localPlayer.GetHealth() + healAmount;

            if (healthAmount < 0)
                healthAmount = 20;
            else if (healthAmount > maxHealth)
                healthAmount = maxHealth;

            Player.m_localPlayer.SetHealth(healthAmount);
            lastHealDateTime = DateTime.Now;

            return true;
        }

        public static bool FillMana(float amount)
        {
            if (Player.m_localPlayer == null)
                return false;

            if ((DateTime.Now - lastEitrDateTime).TotalSeconds <= 3)
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, "You can't drink this eitr potion right now");
                return false;
            }

            var maxEitr = Player.m_localPlayer.GetMaxEitr();
            if (maxEitr == 0) maxEitr = 1;
            var eitrAmount = (maxEitr / 100) * amount;
            
            if (eitrAmount > maxEitr)
                eitrAmount = maxEitr - eitrAmount;

            Player.m_localPlayer.AddEitr(eitrAmount);

            lastEitrDateTime = DateTime.Now;

            return true;
        }

        public static bool Regenerate(float amount)
        {
            if (Player.m_localPlayer == null)
                return false;

            if ((DateTime.Now - lastStaminaDateTime).TotalSeconds <= 3)
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, "You can't drink this stamina potion right now");
                return false;
            }

            var maxStamina = Player.m_localPlayer.GetMaxStamina();
            var restoreAmount = (maxStamina / 100) * amount;

            Player.m_localPlayer.AddStamina(restoreAmount);
            lastStaminaDateTime = DateTime.Now;

            return true;
        }

        private static bool IsHealingBottle(ItemDrop.ItemData item)
        {
            return (item != null && item.m_shared != null && item.m_dropPrefab != null &&
                    bottleNames.Contains(item.m_dropPrefab.gameObject.name));
        }

        [HarmonyPatch(typeof(ItemStand), "CanAttach")]
        public static class AttachPatch
        {
            [HarmonyPostfix]
            private static void Postfix(ItemStand __instance, ItemDrop.ItemData item, ref bool __result)
            {
                if (!__result)
                {
                    __result = __instance.m_supportedTypes.Contains(item.m_shared.m_itemType) && IsHealingBottle(item);
                }
            }
        }

        [HarmonyPatch(typeof(Player), "ConsumeItem")]
        public static class ConsumePatch
        {
            private static bool Prefix(ItemDrop.ItemData item)
            {
                if (item != null && item.m_shared != null && item.m_dropPrefab != null && item.m_dropPrefab.gameObject != null && bottleNames.Contains(item.m_dropPrefab.gameObject.name))
                {
                    var name = item.m_dropPrefab.gameObject.name;
                    if (name.ToUpper().StartsWith("HEAL"))
                    {
                        if (name.EndsWith("00")) return Heal(25);
                        if (name.EndsWith("01")) return Heal(50);
                        if (name.EndsWith("02")) return Heal(75);
                        if (name.EndsWith("03")) return Heal(100);
                    } else if (name.ToUpper().StartsWith("STAM"))
                    {
                        if (name.EndsWith("00")) return Regenerate(25);
                        if (name.EndsWith("01")) return Regenerate(50);
                        if (name.EndsWith("02")) return Regenerate(75);
                        if (name.EndsWith("03")) return Regenerate(100);
                    }
                    else if (name.ToUpper().StartsWith("MANA"))
                    {
                        if (name.EndsWith("00")) return FillMana(25);
                        if (name.EndsWith("01")) return FillMana(50);
                        if (name.EndsWith("02")) return FillMana(75);
                        if (name.EndsWith("03")) return FillMana(100);
                    }

                    if (Debugger.IsAttached) // well, when we enter this, the item has not been found
                        Debugger.Break();
                }
                
                return true;
            }
        }
    }
}
