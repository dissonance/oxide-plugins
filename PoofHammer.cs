// This is a simplified version of MagicHammer
using System;
using System.Linq;
using System.Collections.Generic;
using Rust;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.Reflection;
using UnityEngine;

namespace Oxide.Plugins {
    [Info("PoofHammer", "Norn/Werkrat and modified by dissonance", "1.0.0")]
    [Description("Hit stuff with the hammer to delete.")]
    public class PoofHammer : RustPlugin {

        [PluginReference]
        Plugin PopupNotifications;
        class StoredData {
            public Dictionary<ulong, PoofHammerInfo> Users = new Dictionary<ulong, PoofHammerInfo>();
            public StoredData() {
            }
        }

        class PoofHammerInfo {
            public ulong UserId;
            public bool Enabled;
            public bool Messages_Enabled;
            public PoofHammerInfo() {
            }
        }

        StoredData hammerUserData;
        static FieldInfo buildingPrivilege = typeof(BasePlayer).GetField("buildingPrivilege", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
        
        void Loaded() {
            if (!permission.PermissionExists("PoofHammer.allowed")) {
                permission.RegisterPermission("PoofHammer.allowed", this);
            }

            hammerUserData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>(this.Title + "_users");
        }

        void OnPlayerInit(BasePlayer player) {
            InitPlayerData(player);
        }

        bool InitPlayerData(BasePlayer player) {
            if(CanPoofHammer(player)) {
                PoofHammerInfo p = null;
                if (hammerUserData.Users.TryGetValue(player.userID, out p) == false) {
                    var info = new PoofHammerInfo();
                    info.Enabled = false;
                    info.UserId = player.userID;
                    info.Messages_Enabled = true;
                    hammerUserData.Users.Add(player.userID, info);
                    Interface.GetMod().DataFileSystem.WriteObject(this.Title + "_users", hammerUserData);
                    Puts("Adding entry " + player.userID.ToString());
                }
            } else {
                PoofHammerInfo p = null;
                if (hammerUserData.Users.TryGetValue(player.userID, out p)) {
                    Puts("Removing " + player.userID + " from magic hammer data, cleaning up...");
                    hammerUserData.Users.Remove(player.userID);
                }
            }
            return false;
        }

        protected override void LoadDefaultConfig() {
            Puts("Updating configuration file...");
            Config.Clear();
            Config["iProtocol"] = Protocol.network;
            Config["bUsePopupNotifications"] = false;
            Config["bMessagesEnabled"] = true;
            Config["bChargeForRepairs"] = true;
            Config["nTimeSinceAttacked"] = 8;
            Config["nModesEnabled(1=repair only, 2=destroy only, 3=both enabled)"] = 1;
            Config["tMessageRepaired"] = "Entity: <color=#F2F5A9>{entity_name}</color> health <color=#2EFE64>updated</color> from <color=#FF4000>{current_hp}</color>/<color=#2EFE64>{new_hp}</color>.";
            Config["tMessageDestroyed"] = "Entity: <color=#F2F5A9>{entity_name}</color> <color=#FF4000>destroyed</color>.";
            Config["tMessageUsage"] = "/poof <enabled/mode>.";
            Config["tHammerEnabled"] = "Status: {hammer_status}.";
            Config["tHammerMode"] = "You have switched to: {hammer_mode} mode.";
            Config["tMessageModeDisabled"] = "{disabled_mode} mode is currently <color=#FF4000>disabled</color>";
            Config["tHammerModeText"] = "Choose your mode: 1 = <color=#2EFE64>repair</color>, 2 = <color=#FF4000>destroy</color>.";
            Config["tNoAccessCupboard"] = "You <color=#FF4000>don't</color> have access to all the tool cupboards around you.";
            Config["bDestroyCupboardCheck"] = true;
            SaveConfig();
        }

        bool CanPoofHammer(BasePlayer player) {
            if (permission.UserHasPermission(player.userID.ToString(), "PoofHammer.allowed")) { return true; }
            return false;
        }

        private void PrintToChatEx(BasePlayer player, string result, string tcolour = "#F5A9F2") {
            if (Convert.ToBoolean(Config["bMessagesEnabled"])) {
                if (Convert.ToBoolean(Config["bUsePopupNotifications"])) {
                    PopupNotifications?.Call("CreatePopupNotification", "<color=" + tcolour + ">" + this.Title.ToString() + "</color>\n" + result, player);
                } else {
                    PrintToChat(player, "<color=\"" + tcolour + "\">[" + this.Title.ToString() + "]</color> " + result);
                }
            }
        }

        void Unload() {
            Puts("Saving PoofHammer database...");
            SaveData();
        }

        void SaveData() {
            Interface.Oxide.DataFileSystem.WriteObject(this.Title+"_users", hammerUserData);
        }

        bool SetPlayerHammerStatus(BasePlayer player, bool enabled) {
            PoofHammerInfo p = null;
            if (hammerUserData.Users.TryGetValue(player.userID, out p)) {
                p.Enabled = enabled;
                return true;
            }
            return false;
        }

        bool PoofHammerEnabled(BasePlayer player) {
            PoofHammerInfo p = null;
            if (hammerUserData.Users.TryGetValue(player.userID, out p)) {
                return p.Enabled;
            }
            return false;
        }

        [ChatCommand("poof")]
        void cmdMH(BasePlayer player, string cmd, string[] args) {
            if (CanPoofHammer(player)) {
                PoofHammerInfo p = null;

                if (hammerUserData.Users.TryGetValue(player.userID, out p) == false) {
                    InitPlayerData(player);
                }

                if (PoofHammerEnabled(player)) {
                    string parsed_config = Config["tHammerEnabled"].ToString();
                    parsed_config = parsed_config.Replace("{hammer_status}", "<color=#FF4000>disabled</color>");
                    PrintToChatEx(player, parsed_config);
                    SetPlayerHammerStatus(player, false);
                } else {
                    string parsed_config = Config["tHammerEnabled"].ToString();
                    parsed_config = parsed_config.Replace("{hammer_status}", "<color=#2EFE64>enabled</color>");
                    PrintToChatEx(player, parsed_config);
                    SetPlayerHammerStatus(player, true);
                }
            }
        }

        void OnStructureRepairEx(BaseCombatEntity entity, BasePlayer player) {
            if (CanPoofHammer(player) && PoofHammerEnabled(player)) {
                string block_shortname = entity.ShortPrefabName;
                string block_displayname = entity.ShortPrefabName;

                if (Convert.ToBoolean(Config["bDestroyCupboardCheck"])) {
                    if (hasTotalAccess(player)) {
                        string parsed_config = Config["tMessageDestroyed"].ToString();
                        if (block_displayname.Length == 0) {
                            parsed_config = parsed_config.Replace("{entity_name}", block_shortname);
                        } else {
                            parsed_config = parsed_config.Replace("{entity_name}", block_displayname);
                        }
                        PrintToChatEx(player, parsed_config);
                        RemoveEntity(entity);
                    } else {
                        PrintToChatEx(player, Config["tNoAccessCupboard"].ToString());
                    }
                } else {
                    string parsed_config = Config["tMessageDestroyed"].ToString();
                    if (block_displayname.Length == 0) {
                        parsed_config = parsed_config.Replace("{entity_name}", block_shortname);
                    } else {
                        parsed_config = parsed_config.Replace("{entity_name}", block_displayname);
                    }
                    PrintToChatEx(player, parsed_config);
                    RemoveEntity(entity);
                }
            }
        }
        static void RemoveEntity(BaseCombatEntity entity) {
            if (entity == null) { return; }
            entity.KillMessage();
        }

        static bool hasTotalAccess(BasePlayer player) {
            List<BuildingPrivlidge> playerpriv = buildingPrivilege.GetValue(player) as List<BuildingPrivlidge>;
            if (playerpriv.Count == 0) {
                return false;
            }
            foreach (BuildingPrivlidge priv in playerpriv.ToArray()) {
                List<ProtoBuf.PlayerNameID> authorized = priv.authorizedPlayers;
                bool flag1 = false;
                foreach (ProtoBuf.PlayerNameID pni in authorized.ToArray()) {
                    if (pni.userid == player.userID) {
                        flag1 = true;
                    }
                }
                if (!flag1) {
                    return false;
                }
            }
            return true;
        }

        private void OnServerInitialized() {
            if (Config["tMessageModeDisabled"] == null) {
                Puts("Resetting configuration file (out of date)...");
                LoadDefaultConfig();
            }
        }

        private object OnStructureRepair(BaseCombatEntity entity, BasePlayer player) {
            if (CanPoofHammer(player) && PoofHammerEnabled(player)) {
                OnStructureRepairEx(entity, player);
                return false;
            } else {
                return null; //user not allowed to use PoofHammer -OR- they have it disabled (so regular repairing isn't blocked)
            }
        }
    }
}
