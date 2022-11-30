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
    [BepInPlugin(HealingBottles.PluginGUID, "HealingBottles", "1.0.0")]
    [BepInDependency(Jotunn.Main.ModGuid, "2.9.0")]
    public class HealingBottles : BaseUnityPlugin
    {
        public const string PluginGUID = "mennowar.mods.HealingBottles";
        Harmony harmony = new Harmony(PluginGUID);

        private static List<string> bottleNames = new List<string>();

        private static DateTime lastDateTime = DateTime.MinValue;

        private void Awake()
        {
            harmony.PatchAll();

            PrefabManager.OnVanillaPrefabsAvailable += VanillaPrefabsAvailable;
        }

        private void VanillaPrefabsAvailable()
        {
            const string assetName0 = "Assets/_Custom/HealingBottle00.prefab";
            const string assetName1 = "Assets/_Custom/HealingBottle01.prefab";
            const string assetName2 = "Assets/_Custom/HealingBottle02.prefab";
            const string assetName3 = "Assets/_Custom/HealingBottle03.prefab";

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

            ItemManager.OnItemsRegistered -= VanillaPrefabsAvailable;

            var meadows = loadPrefab(assetName0);
            meadows.Requirements = new RequirementConfig[] { rqB, rqD, new RequirementConfig("Mushroom", 5) };
            ItemManager.Instance.AddItem(new CustomItem(bundle, assetName0, true, meadows));

            var blackForest = loadPrefab(assetName1);
            blackForest.Requirements = new RequirementConfig[] { rqB, rqD, new RequirementConfig("Thistle", 5)};
            ItemManager.Instance.AddItem(new CustomItem(bundle, assetName1, true, blackForest));

            var mountain = loadPrefab(assetName2);
            mountain.Requirements = new RequirementConfig[] { rqB, rqD, new RequirementConfig("FreezeGland", 5) };
            ItemManager.Instance.AddItem(new CustomItem(bundle, assetName2, true, mountain));

            var plains = loadPrefab(assetName3);
            plains.Requirements = new RequirementConfig[] { rqB, rqD, new RequirementConfig("Flax", 5) };
            ItemManager.Instance.AddItem(new CustomItem(bundle, assetName3, true, plains));
        }
        
        public static bool Heal(float amount)
        {
            if (Player.m_localPlayer == null)
                return false;
            
            if ((DateTime.Now - lastDateTime).TotalSeconds <= 3)
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, "You can't drink this potion right now");
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
            lastDateTime = DateTime.Now;

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
                var arrr = string.Join(", ", bottleNames);
                if (item != null && item.m_shared != null && item.m_dropPrefab != null && item.m_dropPrefab.gameObject != null && bottleNames.Contains(item.m_dropPrefab.gameObject.name))
                {
                    var name = item.m_dropPrefab.gameObject.name;
                    if (name.EndsWith("00")) return Heal(25);
                    if (name.EndsWith("01")) return Heal(50);
                    if (name.EndsWith("02")) return Heal(75);
                    if (name.EndsWith("03")) return Heal(100);

                    if (Debugger.IsAttached) // well, when we enter this, the item has not been found
                        Debugger.Break();
                }
                
                return true;
            }
        }
    }
}
