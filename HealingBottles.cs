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

        private void Awake()
        {
            harmony.PatchAll();

            PrefabManager.OnVanillaPrefabsAvailable += VanillaPrefabsAvailable;
        }

        private void VanillaPrefabsAvailable()
        {
            const string hbAssetName0 = "Assets/_Custom/HealingBottle00.prefab";
            const string hbAssetName1 = "Assets/_Custom/HealingBottle01.prefab";
            const string hbAssetName2 = "Assets/_Custom/HealingBottle02.prefab";
            const string hbAssetName3 = "Assets/_Custom/HealingBottle03.prefab";

            const string sbAssetName0 = "Assets/_Custom/StaminaBottle00.prefab"; // don't know why they are in fbx
            const string sbAssetName1 = "Assets/_Custom/StaminaBottle01.prefab";
            const string sbAssetName2 = "Assets/_Custom/StaminaBottle02.prefab";
            const string sbAssetName3 = "Assets/_Custom/StaminaBottle03.prefab";


            var bundle = AssetUtils.LoadAssetBundleFromResources("bottles", typeof(HealingBottles).Assembly);
            var rqD = new RequirementConfig("Dandelion", 5);
            var rqB = new RequirementConfig("Blueberries", 5);

            ItemConfig loadPrefab(string prefabName)
            {
                var prefab = bundle.LoadAsset<GameObject>(prefabName);
                var drop = prefab.GetComponent<ItemDrop>();
                var shared = drop.m_itemData.m_shared;
                bottleNames.Add(drop.gameObject.name);
                
                return new ItemConfig
                {
                    Name = shared.m_name,
                    Icons = shared.m_icons,
                    Description = shared.m_description,
                    CraftingStation = "piece_cauldron",
                    MinStationLevel = shared.m_value
                };
            }

            PrefabManager.OnVanillaPrefabsAvailable -= VanillaPrefabsAvailable;

            #region Healing-Bottles
            var meadows = loadPrefab(hbAssetName0);
            meadows.Requirements = new RequirementConfig[] { rqB, rqD, new RequirementConfig("Mushroom", 5) };
            ItemManager.Instance.AddItem(new CustomItem(bundle, hbAssetName0, true, meadows));

            var blackForest = loadPrefab(hbAssetName1);
            blackForest.Requirements = new RequirementConfig[] { rqB, rqD, new RequirementConfig("Thistle", 5)};
            ItemManager.Instance.AddItem(new CustomItem(bundle, hbAssetName1, true, blackForest));

            var mountain = loadPrefab(hbAssetName2);
            mountain.Requirements = new RequirementConfig[] { rqB, rqD, new RequirementConfig("FreezeGland", 5) };
            ItemManager.Instance.AddItem(new CustomItem(bundle, hbAssetName2, true, mountain));

            var plains = loadPrefab(hbAssetName3);
            plains.Requirements = new RequirementConfig[] { rqB, rqD, new RequirementConfig("Flax", 5) };
            ItemManager.Instance.AddItem(new CustomItem(bundle, hbAssetName3, true, plains));
            #endregion

            #region Stamina-Bottles
            var staminaM = loadPrefab(sbAssetName0);
            staminaM.Requirements = new RequirementConfig[] { rqB, rqD, new RequirementConfig("Mushroom", 5) };
            ItemManager.Instance.AddItem(new CustomItem(bundle, sbAssetName0, true, staminaM));

            var staminaBF = loadPrefab(sbAssetName1);
            staminaBF.Requirements = new RequirementConfig[] { rqB, rqD, new RequirementConfig("Thistle", 5) };
            ItemManager.Instance.AddItem(new CustomItem(bundle, sbAssetName1, true, staminaBF));

            var staminaMt = loadPrefab(sbAssetName2);
            staminaMt.Requirements = new RequirementConfig[] { rqB, rqD, new RequirementConfig("FreezeGland", 5) };
            ItemManager.Instance.AddItem(new CustomItem(bundle, sbAssetName2, true, staminaMt));

            var staminaP = loadPrefab(sbAssetName3);
            staminaP.Requirements = new RequirementConfig[] { rqB, rqD, new RequirementConfig("Flax", 5) };
            ItemManager.Instance.AddItem(new CustomItem(bundle, sbAssetName3, true, staminaP));
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
            var healAmount = (maxHealth / 100) * amount;
            var healthAmount = Player.m_localPlayer.GetHealth() + healAmount;

            if (healthAmount < 0)
                healthAmount = 20;
            else if (healthAmount > maxHealth)
                healthAmount = maxHealth;

            Player.m_localPlayer.SetHealth(healthAmount);
            lastHealDateTime = DateTime.Now;

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

                    if (Debugger.IsAttached) // well, when we enter this, the item has not been found
                        Debugger.Break();
                }
                
                return true;
            }
        }
    }
}
