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
    [BepInDependency(Jotunn.Main.ModGuid, "2.10.4")]
    public class HealingBottles : BaseUnityPlugin
    {
        public const string PluginGUID = "mennowar.mods.HealingBottles";
        private Harmony harmony = new Harmony(PluginGUID);

        private static List<string> bottleNames = new List<string>();

        private static DateTime lastHealDateTime = DateTime.MinValue;
        private static DateTime lastStaminaDateTime = DateTime.MinValue;
        private static DateTime lastEitrDateTime = DateTime.MinValue;

        private AssetBundle assetBundle = null;

        // some prefab names. see bottles.manifest:
        const string healingBottleAssetNameBase = "HealingBottle";
        private string healingBottleAssetName0 = $"Assets/_Custom/{healingBottleAssetNameBase}00.prefab";
        private string healingBottleAssetName1 = $"Assets/_Custom/{healingBottleAssetNameBase}01.prefab";
        private string healingBottleAssetName2 = $"Assets/_Custom/{healingBottleAssetNameBase}02.prefab";
        private string healingBottleAssetName3 = $"Assets/_Custom/{healingBottleAssetNameBase}03.prefab";

        const string staminaBottleAssetNameBase = "StaminaBottle";
        private string staminaBottleAssetName0 = $"Assets/_Custom/{staminaBottleAssetNameBase}00.prefab";
        private string staminaBottleAssetName1 = $"Assets/_Custom/{staminaBottleAssetNameBase}01.prefab";
        private string staminaBottleAssetName2 = $"Assets/_Custom/{staminaBottleAssetNameBase}02.prefab";
        private string staminaBottleAssetName3 = $"Assets/_Custom/{staminaBottleAssetNameBase}03.prefab";

        const string manaBottleAssetNameBase = "ManaBottle";
        private string manaBottleAssetName0 = $"Assets/_Custom/{manaBottleAssetNameBase}00.prefab";
        private string manaBottleAssetName1 = $"Assets/_Custom/{manaBottleAssetNameBase}01.prefab";
        private string manaBottleAssetName2 = $"Assets/_Custom/{manaBottleAssetNameBase}02.prefab";
        private string manaBottleAssetName3 = $"Assets/_Custom/{manaBottleAssetNameBase}03.prefab";

        private ConfigEntry<string> craftingStationItemName;
        private ConfigEntry<string> baseItemName;
        private ConfigEntry<int> baseItemAmount;

        private ConfigEntry<string> smallPotionQualityItemName;
        private ConfigEntry<int> smallPotionQualityItemAmount;
        private ConfigEntry<int> smallPotionCraftingStationLevel;

        private ConfigEntry<string> mediumPotionQualityItemName;
        private ConfigEntry<int> mediumPotionQualityItemAmount;
        private ConfigEntry<int> mediumPotionCraftingStationLevel;

        private ConfigEntry<string> hugePotionQualityItemName;
        private ConfigEntry<int> hugePotionQualityItemAmount;
        private ConfigEntry<int> hugePotionCraftingStationLevel;

        private ConfigEntry<string> godlyPotionQualityItemName;
        private ConfigEntry<int> godlyPotionQualityItemAmount;
        private ConfigEntry<int> godlyPotionCraftingStationLevel;

        private ConfigEntry<string> healingPotionTypeItemName;
        private ConfigEntry<int> healingPotionTypeItemAmount;
        private static ConfigEntry<int> healingPotionTimeOut;

        private ConfigEntry<string> staminaPotionTypeItemName;
        private ConfigEntry<int> staminaPotionTypeItemAmount;
        private static ConfigEntry<int> staminaPotionTimeOut;

        private ConfigEntry<string> manaPotionTypeItemName;
        private ConfigEntry<int> manaPotionTypeItemAmount;
        private static ConfigEntry<int> manaPotionTimeOut;

        private ConfigEntry<bool> healingPotionsEnabled;
        private ConfigEntry<bool> staminaPotionsEnabled;
        private ConfigEntry<bool> manaPotionsEnabled;

        private static string drinkNotPossibleMessage = "You can't drink this potion right now";

        private void Awake()
        {
            harmony.PatchAll();

            #region Read or Create Config Settings
            Config.SaveOnConfigSet = true;
            
            // Crafting Stations:
            craftingStationItemName = Config.Bind<string>("General", "CraftingStationID", "piece_cauldron", new ConfigDescription("The id of the crafting station.", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            healingPotionsEnabled = Config.Bind<bool>("General", "HealingPotionsEnabled", true, new ConfigDescription("Indicates whether healing potions are craftable.", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            staminaPotionsEnabled = Config.Bind<bool>("General", "StaminaPotionsEnabled", true, new ConfigDescription("Indicates whether stamina potions are craftable.", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            manaPotionsEnabled = Config.Bind<bool>("General", "ManaPotionsEnabled", true, new ConfigDescription("Indicates whether Eitr/Mana potions are craftable.", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            
            // Base Item Config:
            baseItemName = Config.Bind<string>("Base", "BaseItemName", "Dandelion", new ConfigDescription("Base Item, required for all Potions.", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            baseItemAmount = Config.Bind<int>("Base", "BaseItemAmount", 5, new ConfigDescription("Amount of Base Item required.", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            
            // Timeouts:
            healingPotionTimeOut = Config.Bind<int>("Base", "HealingPotionTimeout", 3, new ConfigDescription("Wait time in seconds before the next healing potion may be consumed.", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            staminaPotionTimeOut = Config.Bind<int>("Base", "StaminaPotionTimeout", 3, new ConfigDescription("Wait time in seconds before the next stamina potion may be consumed.", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            manaPotionTimeOut = Config.Bind<int>("Base", "EitrPotionTimeout", 3, new ConfigDescription("Wait time in seconds before the next MANA/EITR potion may be consumed.", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));

            // Potions Quality items:
            smallPotionCraftingStationLevel = Config.Bind<int>("Quality", "SmallPotionCraftingStationLevel", 0, new ConfigDescription("The Crafting-Station Level needed to craft small potions.", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            mediumPotionCraftingStationLevel = Config.Bind<int>("Quality", "MediumPotionCraftingStationLevel", 1, new ConfigDescription("The Crafting-Station Level needed to craft medium potions.", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            hugePotionCraftingStationLevel = Config.Bind<int>("Quality", "HugePotionCraftingStationLevel", 2, new ConfigDescription("The Crafting-Station Level needed to craft Huge potions.", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            godlyPotionCraftingStationLevel = Config.Bind<int>("Quality", "GodlyPotionCraftingStationLevel", 3, new ConfigDescription("The Crafting-Station Level needed to craft Godly potions.", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));


            smallPotionQualityItemName = Config.Bind<string>("Quality", "SmallPotionQualityItemName", "Mushroom", new ConfigDescription("Name of the Item for identifying a potion as small quality.", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            smallPotionQualityItemAmount = Config.Bind<int>("Quality", "SmallPotionQualityItemAmount", 5, new ConfigDescription("Amount of small quality item required.", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));

            mediumPotionQualityItemName = Config.Bind<string>("Quality", "MediumPotionQualityItemName", "Thistle", new ConfigDescription("Name of the Item for identifying a potion as medium quality.", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            mediumPotionQualityItemAmount = Config.Bind<int>("Quality", "MediumPotionQualityItemAmount", 5, new ConfigDescription("Amount of medium quality item required.", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));

            hugePotionQualityItemName = Config.Bind<string>("Quality", "HugePotionQualityItemName", "Obsidian", new ConfigDescription("Name of the Item for identifying a potion as huge quality.", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            hugePotionQualityItemAmount = Config.Bind<int>("Quality", "HugePotionQualityItemAmount", 5, new ConfigDescription("Amount of huge quality item required.", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));

            godlyPotionQualityItemName = Config.Bind<string>("Quality", "GodlyPotionQualityItemName", "Flax", new ConfigDescription("Name of the Item for identifying a potion as godly quality.", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            godlyPotionQualityItemAmount = Config.Bind<int>("Quality", "GodlyPotionQualityItemAmount", 5, new ConfigDescription("Amount of godly quality item required.", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));

            // Potion Type Items:
            healingPotionTypeItemName = Config.Bind<string>("Type", "HealthTypeItemName", "Raspberry", new ConfigDescription("Name of the Item for identifying a potion as HEALING type.", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            healingPotionTypeItemAmount = Config.Bind<int>("Type", "HealthTypeItemAmount", 5, new ConfigDescription("Amount of Item for identifying a potion as HEALING type.", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));

            staminaPotionTypeItemName = Config.Bind<string>("Type", "StaminaTypeItemName", "Blueberries", new ConfigDescription("Name of the Item for identifying a potion as STAMINA type.", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            staminaPotionTypeItemAmount = Config.Bind<int>("Type", "StaminaTypeItemAmount", 5, new ConfigDescription("Amount of Item for identifying a potion as STAMINA type.", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));

            manaPotionTypeItemName = Config.Bind<string>("Type", "ManaTypeItemName", "MushroomYellow", new ConfigDescription("Name of the Item for identifying a potion as MANA/EITR type.", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            manaPotionTypeItemAmount = Config.Bind<int>("Type", "ManaTypeItemAmount", 5, new ConfigDescription("Amount of Item for identifying a potion as MANA/EITR type.", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            #endregion

            PrefabManager.OnVanillaPrefabsAvailable += VanillaPrefabsAvailable;

            Config.SettingChanged += (s, ea) =>
            {
                Logger.LogInfo("Config changed, reloading recipes");

                ConfigChangeRecipeUpdate(healingBottleAssetNameBase, healingPotionTypeItemName, healingPotionTypeItemAmount);
                ConfigChangeRecipeUpdate(staminaBottleAssetNameBase, staminaPotionTypeItemName, staminaPotionTypeItemAmount);
                ConfigChangeRecipeUpdate(manaBottleAssetNameBase, manaPotionTypeItemName, manaPotionTypeItemAmount);
            };
        }

        /// <summary>
        /// Updates all 4 Qualitiy Recipes of the provided bottle
        /// </summary>
        /// <param name="bottleAssetName"></param>
        /// <param name="typeItemName"></param>
        /// <param name="typeItemAmount"></param>
        private void ConfigChangeRecipeUpdate(string bottleAssetName, ConfigEntry<string> typeItemName, ConfigEntry<int> typeItemAmount)
        {
            var bottleName = ExtractBottleName(bottleAssetName).Split('0')[0];

            UpdateExistingRecipe($"{bottleName}00", smallPotionQualityItemName, smallPotionQualityItemAmount, typeItemName, typeItemAmount, smallPotionCraftingStationLevel);
            UpdateExistingRecipe($"{bottleName}01", mediumPotionQualityItemName, mediumPotionQualityItemAmount, typeItemName, typeItemAmount, mediumPotionCraftingStationLevel);
            UpdateExistingRecipe($"{bottleName}02", hugePotionQualityItemName, hugePotionQualityItemAmount, typeItemName, typeItemAmount, hugePotionCraftingStationLevel);
            UpdateExistingRecipe($"{bottleName}03", godlyPotionQualityItemName, godlyPotionQualityItemAmount, typeItemName, typeItemAmount, godlyPotionCraftingStationLevel);
        }

        /// <summary>
        /// Updates the Recipe for the given bottleName
        /// </summary>
        /// <param name="bottleName"></param>
        /// <param name="qualityItemName"></param>
        /// <param name="qualityItemAmount"></param>
        /// <param name="typeItemName"></param>
        /// <param name="typeItemAmount"></param>
        private void UpdateExistingRecipe(string bottleName, 
            ConfigEntry<string> qualityItemName, ConfigEntry<int> qualityItemAmount, 
            ConfigEntry<string> typeItemName, ConfigEntry<int> typeItemAmount,
            ConfigEntry<int> stationLevel)
        {
            // var bottleName = ExtractBottleName(assetName);
            var bottleCustomItem = ItemManager.Instance.GetItem(bottleName);
            if (bottleCustomItem == null || !ObjectDB.instance.m_items.Any())
                return;

            if (bottleCustomItem.Recipe == null)
            {
                Recipe m_Recipe = ScriptableObject.CreateInstance<Recipe>();
                m_Recipe.name = $"Recipe_{bottleName}";
                m_Recipe.m_item = bottleCustomItem.ItemDrop;

                var hsRecipe = new CustomRecipe(m_Recipe, true, true)
                {
                    FixRequirementReferences = true
                };

                bottleCustomItem.Recipe = hsRecipe;
                hsRecipe.Recipe.m_minStationLevel = stationLevel.Value;
            }

            var rqs = new List<Piece.Requirement>();

            // Helperfunction to check for correct name of item, name existence and amount
            void AddRq(ConfigEntry<string> item, ConfigEntry<int> amount)
            {
                try
                {
                    if (item != null && !string.IsNullOrEmpty(item.Value) && amount.Value > 0)
                    {

                        var itemDrop = ObjectDB.instance.GetItemPrefab(item.Value).GetComponent<ItemDrop>(); // Thanks to Margmas!
                        if (itemDrop != null)
                        {
                            rqs.Add(new Piece.Requirement
                            {
                                m_amount = amount.Value,
                                m_resItem = itemDrop
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex.Message);
                }
            }

            AddRq(baseItemName, baseItemAmount);
            AddRq(qualityItemName, qualityItemAmount);
            AddRq(typeItemName, typeItemAmount);

            if (bottleCustomItem?.Recipe != null && bottleCustomItem.Recipe.Recipe != null)
            {
                bottleCustomItem.Recipe.Recipe.m_resources = rqs.ToArray();
                bottleCustomItem.Recipe.Recipe.m_minStationLevel = stationLevel.Value;                
            }
        }

        /// <summary>
        /// simply extracts the real name from the .prefab filename
        /// </summary>
        /// <param name="assetName"></param>
        /// <returns></returns>
        private string ExtractBottleName(string assetName)
        {
            return assetName.Replace(".prefab", "").Split('/').Last(); // Assets/_Custom/HealingBottle00.prefab
        }

        private void VanillaPrefabsAvailable()
        {
            // 1st thing: opt out of VanillaPrefabsAvailable:
            PrefabManager.OnVanillaPrefabsAvailable -= VanillaPrefabsAvailable;

            // assetBundle points to the Assets/bottles file. make sure it has Build-Action "embedded Resource":
            if (assetBundle == null)
                assetBundle = AssetUtils.LoadAssetBundleFromResources("bottles", typeof(HealingBottles).Assembly);

            // --- REQUIREMENTS ---
            // basic requirement for *every* potion
            var rcBaseAllPotions = new RequirementConfig(baseItemName.Value, baseItemAmount.Value);

            // requirements for the different qualities:
            var rcSmallPotion = new RequirementConfig(smallPotionQualityItemName.Value, smallPotionQualityItemAmount.Value);
            var rcMediumPotion = new RequirementConfig(mediumPotionQualityItemName.Value, mediumPotionQualityItemAmount.Value);
            var rcHugePotion = new RequirementConfig(hugePotionQualityItemName.Value, hugePotionQualityItemAmount.Value);
            var rcGodlyPotion = new RequirementConfig(godlyPotionQualityItemName.Value, godlyPotionQualityItemAmount.Value);

            # region helper functions:            
            // Load the Prefab asset from the bundle and add the requirements for the item ath the cauldron
            ItemConfig LoadCustomItemFromPrefab(string prefabName, int craftingStationLevel, params RequirementConfig[] requirements)
            {
                var prefab = assetBundle.LoadAsset<GameObject>(prefabName);
                var drop = prefab.GetComponent<ItemDrop>();
                var shared = drop.m_itemData.m_shared;

                if (!bottleNames.Contains(drop.gameObject.name))
                    bottleNames.Add(drop.gameObject.name);
                
                var result = new ItemConfig
                {
                    Name = shared.m_name,
                    Icons = shared.m_icons,
                    Description = shared.m_description,
                    CraftingStation = craftingStationItemName.Value,
                    MinStationLevel = craftingStationLevel, // shared.m_value,
                    Requirements = requirements
                };

                return result;
            }

            // simple wrapper for one line item creation
            void AddItem(string assetName, int craftingStationLevel, params RequirementConfig[] requirements)
            {
                var name = ExtractBottleName(assetName);
                var prefab = LoadCustomItemFromPrefab(assetName, craftingStationLevel, requirements);
                CustomItem item = ItemManager.Instance.GetItem(name);
                if (item == null)
                {
                    item = new CustomItem(assetBundle, assetName, true, prefab);
                    if (item?.Recipe?.Recipe != null)
                        item.Recipe.Recipe.m_minStationLevel = craftingStationLevel;

                    ItemManager.Instance.AddItem(item);
                }
            }
            #endregion

            #region Healing-Bottles
            // requirement for healing potion:
            if (healingPotionsEnabled.Value)
            {
                var rcHealingPotion = new RequirementConfig(healingPotionTypeItemName.Value, healingPotionTypeItemAmount.Value);
                AddItem(healingBottleAssetName0, smallPotionCraftingStationLevel.Value, rcBaseAllPotions, rcSmallPotion, rcHealingPotion);
                AddItem(healingBottleAssetName1, mediumPotionCraftingStationLevel.Value, rcBaseAllPotions, rcMediumPotion, rcHealingPotion);
                AddItem(healingBottleAssetName2, hugePotionCraftingStationLevel.Value, rcBaseAllPotions, rcHugePotion, rcHealingPotion);
                AddItem(healingBottleAssetName3, godlyPotionCraftingStationLevel.Value, rcBaseAllPotions, rcGodlyPotion, rcHealingPotion);
            }
            #endregion

            #region Stamina-Bottles
            // requirement for stamina potion:
            if (staminaPotionsEnabled.Value)
            {
                var rcStaminaPotion = new RequirementConfig(staminaPotionTypeItemName.Value, staminaPotionTypeItemAmount.Value);
                AddItem(staminaBottleAssetName0, smallPotionCraftingStationLevel.Value, rcBaseAllPotions, rcSmallPotion, rcStaminaPotion);
                AddItem(staminaBottleAssetName1, mediumPotionCraftingStationLevel.Value, rcBaseAllPotions, rcMediumPotion, rcStaminaPotion);
                AddItem(staminaBottleAssetName2, hugePotionCraftingStationLevel.Value, rcBaseAllPotions, rcHugePotion, rcStaminaPotion);
                AddItem(staminaBottleAssetName3, godlyPotionCraftingStationLevel.Value, rcBaseAllPotions, rcGodlyPotion, rcStaminaPotion);
            }
            #endregion

            #region mana bottles
            // requirement for mana potion:
            if (manaPotionsEnabled.Value)
            {
                var rcManaPotion = new RequirementConfig(manaPotionTypeItemName.Value, manaPotionTypeItemAmount.Value);
                AddItem(manaBottleAssetName0, smallPotionCraftingStationLevel.Value, rcBaseAllPotions, rcSmallPotion, rcManaPotion);
                AddItem(manaBottleAssetName1, mediumPotionCraftingStationLevel.Value, rcBaseAllPotions, rcMediumPotion, rcManaPotion);
                AddItem(manaBottleAssetName2, hugePotionCraftingStationLevel.Value, rcBaseAllPotions, rcHugePotion, rcManaPotion);
                AddItem(manaBottleAssetName3, godlyPotionCraftingStationLevel.Value, rcBaseAllPotions, rcGodlyPotion, rcManaPotion);
            }
            #endregion
        }

        /// <summary>
        /// Heals the given % amount
        /// </summary>
        /// <param name="amount"></param>
        /// <returns></returns>
        public static bool FillHealth(float amount)
        {
            if (Player.m_localPlayer == null)
                return false;
            
            if ((DateTime.Now - lastHealDateTime).TotalSeconds <= healingPotionTimeOut.Value)
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, drinkNotPossibleMessage);
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

        /// <summary>
        /// Fills Eitr with the given % amount
        /// </summary>
        /// <param name="amount"></param>
        /// <returns></returns>
        public static bool FillMana(float amount)
        {
            if (Player.m_localPlayer == null)
                return false;

            if ((DateTime.Now - lastEitrDateTime).TotalSeconds <= manaPotionTimeOut.Value)
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, drinkNotPossibleMessage);
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

        /// <summary>
        /// Fills Stamina with the given % amount
        /// </summary>
        /// <param name="amount"></param>
        /// <returns></returns>
        public static bool FillStamina(float amount)
        {
            if (Player.m_localPlayer == null)
                return false;

            if ((DateTime.Now - lastStaminaDateTime).TotalSeconds <= staminaPotionTimeOut.Value)
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, drinkNotPossibleMessage);
                return false;
            }

            var maxStamina = Player.m_localPlayer.GetMaxStamina();
            var restoreAmount = (maxStamina / 100) * amount;

            Player.m_localPlayer.AddStamina(restoreAmount);
            lastStaminaDateTime = DateTime.Now;

            return true;
        }

        /// <summary>
        /// Gets a value indicating whether the given itemdata is created by this mod. Used when performing item stand attachment possible calculation.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        private static bool IsBottle(ItemDrop.ItemData item)
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
                    __result = __instance.m_supportedTypes.Contains(item.m_shared.m_itemType) && IsBottle(item);
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
                        if (name.EndsWith("00")) return FillHealth(25);
                        if (name.EndsWith("01")) return FillHealth(50);
                        if (name.EndsWith("02")) return FillHealth(75);
                        if (name.EndsWith("03")) return FillHealth(100);
                    } else if (name.ToUpper().StartsWith("STAM"))
                    {
                        if (name.EndsWith("00")) return FillStamina(25);
                        if (name.EndsWith("01")) return FillStamina(50);
                        if (name.EndsWith("02")) return FillStamina(75);
                        if (name.EndsWith("03")) return FillStamina(100);
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
