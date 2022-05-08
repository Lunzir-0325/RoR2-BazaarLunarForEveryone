using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using R2API.Utils;
using RoR2;
using RoR2.Networking;
using Unity;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace BazaarLunarForEveryone
{
    [BepInDependency("com.bepis.r2api")]
    [BepInPlugin("com.Lunzir.BazaarLunarForEveryone", "BazaarLunarForEveryone", "1.1.0")]
    [NetworkCompatibility(CompatibilityLevel.NoNeedForSync, VersionStrictness.DifferentModVersionsAreOk)]
    public class BazaarLunarForEveryone : BaseUnityPlugin
    {
        public static PluginInfo PluginInfo;
        public static string BAZAAR_LUNAR_ALREAD_BUY => Language.GetString("LUNAR_POD_ALREADY_PURCHASED");
        public void Awake()
        {
            ModConfig.InitConfig(Config);
            if (ModConfig.EnableMod.Value)
            {
                PluginInfo = Info;
                Tokens.RegisterLanguageTokens();

                On.RoR2.PurchaseInteraction.OnInteractionBegin += PurchaseInteraction_OnInteractionBegin;
                On.RoR2.SceneExitController.Begin += SceneExitController_Begin;

                On.RoR2.Run.Start += Run_Start;
            }
        }

        private void Run_Start(On.RoR2.Run.orig_Start orig, Run self)
        {
            orig(self);
            ModConfig.InitConfig(Config);
            Config.Reload();
        }
        //public void OnDestroy()
        //{
        //    On.RoR2.PurchaseInteraction.OnInteractionBegin -= PurchaseInteraction_OnInteractionBegin;
        //    On.RoR2.SceneExitController.Begin -= SceneExitController_Begin;
        //}

        public static void PurchaseInteraction_OnInteractionBegin(On.RoR2.PurchaseInteraction.orig_OnInteractionBegin orig, PurchaseInteraction self, Interactor activator)
        {
            if (IsCurrentMap(BAZAAR))
            {
                //Send($"self.name = {self.name }");
                if (self.CanBeAffordedByInteractor(activator))
                {
                    // 月店物品每个人只买一次
                    CharacterMaster master = activator.GetComponent<CharacterBody>().master;
                    if (self.name.StartsWith("LunarShopTerminal") || self.name.StartsWith("MultiShopEquipmentTerminal"))
                    {
                        NetworkUser networkUser = Util.LookUpBodyNetworkUser(activator.gameObject);
                        NetworkInstanceId netId = self.netId;
                        var IsBuy = ListPlayerBuyRecord.FirstOrDefault(x => x.NetworkUser == networkUser && x.NetID == netId);

                        // 如果没买过
                        if (IsBuy is null)
                        {
                            //string str_CN = Language.GetString(self.contextToken);
                            //string displayname_CN = Language.GetString(self.NetworkdisplayNameToken);
                            //ChatHelper.DebugSend($"netId = {netId}, contextToken = {str_CN}, NetworkdisplayNameToken = {displayname_CN}");

                            var shop = self.GetComponent<ShopTerminalBehavior>();
                            var characterBody = activator.GetComponent<CharacterBody>();
                            var inventory = characterBody.inventory;

                            var itemIndex = PickupCatalog.GetPickupDef(shop.CurrentPickupIndex()).itemIndex;
                            var equitIndex = PickupCatalog.GetPickupDef(shop.CurrentPickupIndex()).equipmentIndex;

                            // 购买特效
                            Vector3 effectPos = self.transform.position;
                            effectPos.y -= 1;
                            EffectManager.SpawnEffect(LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/ShrineUseEffect"), new EffectData()
                            {
                                origin = effectPos,
                                rotation = Quaternion.identity,
                                scale = 0.01f,
                                color = (Color32)Color.blue
                            }, true);

                            if (itemIndex != ItemIndex.None)
                            {
                                switch (ModConfig.PickupStyle.Value)
                                {
                                    case Pickup.Bag:
                                        PurchaseInteraction.CreateItemTakenOrb(self.gameObject.transform.position + Vector3.up * 5f, activator.GetComponent<CharacterBody>().gameObject, itemIndex);
                                        inventory.GiveItem(itemIndex);
                                        break;
                                    case Pickup.Ground:
                                        PickupDropletController.CreatePickupDroplet(shop.CurrentPickupIndex(),
                                            self.transform.position + Vector3.up * 1.5f, Vector3.up * 20f + self.transform.forward * 2f);
                                        break;
                                }
                            }

                            if (equitIndex != EquipmentIndex.None)
                            {
                                var IsHasEquip = inventory.GetEquipmentIndex();
                                if (IsHasEquip != EquipmentIndex.None) // 如果玩家身上有主动装备，掉落出来
                                    PickupDropletController.CreatePickupDroplet(PickupCatalog.FindPickupIndex(IsHasEquip),
                                        characterBody.gameObject.transform.position + Vector3.up * 1.5f,
                                        characterBody.gameObject.transform.position + Vector3.up * 20f);
                                inventory.SetEquipmentIndex(equitIndex);
                            }

                            self.gameObject.AddComponent<Canteen>(); // 购买过的摊位物品不消失
                            ListPlayerBuyRecord.Add(new PlayerBuyHepler(networkUser, netId));// 添加玩家摊位购买记录

                            if (self.name.StartsWith("LunarShopTerminal"))// 扣款  
                            {
                                networkUser.DeductLunarCoins((uint)self.Networkcost);
                                //ChatHelper.DebugSend("扣币");
                            }
                            if (self.name.StartsWith("MultiShopEquipmentTerminal"))
                            {
                                master.money -= (uint)self.Networkcost;
                                //ChatHelper.DebugSend("扣钱");
                            }
                            if (ModConfig.PickupStyle.Value == Pickup.Bag)
                            {
                                Lunzir.Helper.ChatHelper.Send(master, shop.CurrentPickupIndex());
                            }
                            //GenericPickupController.SendPickupMessage(player, shop.CurrentPickupIndex());
                            return;
                        }
                        else
                        {
                            Lunzir.Helper.ChatHelper.Send($"{BAZAAR_LUNAR_ALREAD_BUY}", master.networkIdentity.clientAuthorityOwner);
                            return;
                        }
                    }
                    else if (self.name.StartsWith("ShrineRestack"))
                    {
                        NetworkUser networkUser = Util.LookUpBodyNetworkUser(activator.gameObject);
                        NetworkInstanceId netId = self.netId;
                        CharacterMaster player = activator.GetComponent<CharacterBody>().master;
                        var IsBuy = ListPlayerBuyRecord.FirstOrDefault(x => x.NetworkUser == networkUser && x.NetID == netId);
                        if (IsBuy is null)
                        {
                            //self.cost = 1;
                            //self.GetComponent<ShrineRestackBehavior>().costMultiplierPerPurchase = 1;
                            ListPlayerBuyRecord.Add(new PlayerBuyHepler(networkUser, netId));
                            networkUser.DeductLunarCoins((uint)self.Networkcost);
                            //self.gameObject.AddComponent<Storage>();
                            orig(self, activator);
                            return;
                        }
                        else
                        {
                            Lunzir.Helper.ChatHelper.Send($"{BAZAAR_LUNAR_ALREAD_BUY}", master.networkIdentity.clientAuthorityOwner);
                            return;
                        }
                    }
                }
            }
            orig(self, activator);
        }
        public static void SceneExitController_Begin(On.RoR2.SceneExitController.orig_Begin orig, SceneExitController self)
        {
            if (NetworkServer.active)
            {
                if (self.destinationScene)
                {
                    if (self.destinationScene.baseSceneName.Contains(BAZAAR)
                        && !SceneInfo.instance.sceneDef.baseSceneName.Contains(BAZAAR))
                    {
                        ListPlayerBuyRecord.Clear();
                    }
                }
            }
            orig(self);
        }



        private static bool IsCurrentMap(string mapName)
        {
            return SceneManager.GetActiveScene().name == mapName;
        }
        private static List<PlayerBuyHepler> ListPlayerBuyRecord = new List<PlayerBuyHepler>();

        public static string BAZAAR = "bazaar";

        internal class PlayerBuyHepler
        {
            public PlayerBuyHepler(NetworkUser networkUser, NetworkInstanceId netID)
            {
                NetworkUser = networkUser;
                NetID = netID;
            }

            public NetworkUser NetworkUser { get; set; }
            public NetworkInstanceId NetID { get; set; }
        }
    }

    enum Pickup
    {
        Bag, Ground
    }
    public class Canteen : MonoBehaviour
    {

    }

    class ModConfig
    {
        public static ConfigEntry<bool> EnableMod;
        public static ConfigEntry<Pickup> PickupStyle;

        public static void InitConfig(ConfigFile config)
        {
            EnableMod = config.Bind("Setting设置", "EnableMod", true, "Enable the Mod.\n启用模组");
            PickupStyle = config.Bind("Setting设置", "PickupStyle", Pickup.Bag, "Item drop mode, Bag = directly into the Bag, Ground = drop to the Ground" +
                "\n摊位购买掉落方式，Bag = 直接包里，Ground = 掉地上");
        }
    }
    public static class Tokens
    {
        internal static string LanguageRoot
        {
            get
            {
                return System.IO.Path.Combine(AssemblyDir, "Language");
            }
        }

        internal static string AssemblyDir
        {
            get
            {
                return System.IO.Path.GetDirectoryName(BazaarLunarForEveryone.PluginInfo.Location);
            }
        }
        public static void RegisterLanguageTokens()
        {
            On.RoR2.Language.SetFolders += Language_SetFolders;
        }

        private static void Language_SetFolders(On.RoR2.Language.orig_SetFolders orig, Language self, IEnumerable<string> newFolders)
        {
            if (Directory.Exists(LanguageRoot))
            {
                IEnumerable<string> second = Directory.EnumerateDirectories(System.IO.Path.Combine(new string[]
                {
                    LanguageRoot
                }), self.name);
                orig.Invoke(self, newFolders.Union(second));
            }
            else
            {
                orig.Invoke(self, newFolders);
            }
        }


    }
}
