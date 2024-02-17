using BepInEx;
using Aki.Reflection.Patching;
using System.Reflection;
using UnityEngine;
using UnityEngine.Playables;
using EFT.Communications;
using EFT.UI;
using EFT;
using System;
using System.Reflection;
using Aki.Reflection.Patching;
using EFT;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Configuration;
using Comfort.Common;

namespace BossNotifier {
    [BepInPlugin("Mattdokn.BossNotifier", "BossNotifier", "1.0.0")]
    public class BossNotifierPlugin : BaseUnityPlugin {
        public static ConfigEntry<KeyboardShortcut> showBossesKeyCode;
        public static ConfigEntry<bool> showNotificationsOnRaidStart;

        private void Awake() {
            showBossesKeyCode = Config.Bind("Boss Notifier", "Keyboard Shortcut", new KeyboardShortcut(KeyCode.O), "Key to show boss notifications.");
            showNotificationsOnRaidStart = Config.Bind("Boss Notifier", "Show Bosses on Raid Start", true, "Show bosses on raid start.");

            new BossLocationSpawnPatch().Enable();
            new NewGamePatch().Enable();

            Logger.LogInfo($"Plugin BossNotifier is loaded!");
        }
    }
    internal class BossLocationSpawnPatch : ModulePatch {
        protected override MethodBase GetTargetMethod() => typeof(BossLocationSpawn).GetMethod("Init");

        private static string BotBossName(WildSpawnType type) {
            /*
             * Customs:
             * X Reshala (bossBully)
             * X Rogues (bossKnight)
             * X Cultists (sectantPriest -> assault/sectantWarrior)
             * 
             * Factory:
             * X Tagilla (bossTagilla)
             * X Cultists (sectantPriest -> assault/sectantWarrior)
             * 
             * GroundZero:
             * 
             * Interchange:
             * X Killa (bossKilla)
             * 
             * Lighthouse:
             * X Rogues (bossKnight)
             * X Zryachiy (bossZryachiy)
             * 
             * Reserve:
             * X Glukhar (bossGlukhar)
             * 
             * Shoreline:
             * X Sanitar (bossSanitar)
             * X Rogues (bossKnight)
             * X Cultists (sectantPriest -> assault/sectantWarrior)
             * 
             * Streets
             * X Kaban (bossKaban)
             * Kollantay (bossKollontay) 0.14 only :(
             * 
             * Woods
             * X Shturman (bossShturman)
             * X Rogues (bossKnight)
             * X Cultists (sectantPriest -> assault/sectantWarrior)
             */
            switch (type) {
                case WildSpawnType.bossBully:
                    return "Reshala";
                case WildSpawnType.bossKnight:
                    return "Goons";
                case WildSpawnType.sectantPriest:
                    return "Cultists";
                case WildSpawnType.bossTagilla:
                    return "Tagilla";
                case WildSpawnType.bossKilla:
                    return "Killa";
                case WildSpawnType.bossZryachiy:
                    return "Zryachiy";
                case WildSpawnType.bossGluhar:
                    return "Glukhar";
                case WildSpawnType.bossSanitar:
                    return "Sanitar";
                case WildSpawnType.bossKojaniy:
                    return "Shturman";
                case WildSpawnType.bossBoar:
                    return "Kaban";
                case WildSpawnType.gifter:
                    return "Santa Claus";
                case WildSpawnType.arenaFighterEvent:
                    return "Blood Hounds";
                case WildSpawnType.crazyAssaultEvent:
                    return "Crazy Scavs";
                case WildSpawnType.exUsec:
                    return "Rogues";
                /* case WildSpawnType: return "Kollantay"; 0.14 patch */
                case WildSpawnType.marksman:
                    // Ignore marksman as they are used for creating artificial borders around the maps.
                    return null;
                default:
                    return null;
            }
        }

        public static HashSet<string> bossesInRaid = new HashSet<string>();

        [PatchPostfix]
        private static void PatchPostfix(BossLocationSpawn __instance) {
            if (__instance.ShallSpawn) {
                string name = BotBossName(__instance.BossType);
                if (name == null) return;
                bossesInRaid.Add(name);
            }
        }
    }

    internal class NewGamePatch : ModulePatch {
        protected override MethodBase GetTargetMethod() => typeof(GameWorld).GetMethod("OnGameStarted");

        [PatchPrefix]
        public static void PatchPrefix() {
            BossNotifierMono.Init();
        }
    }

    class BossNotifierMono : MonoBehaviour {
        private void SendBossNotifications() {
            foreach (var boss in BossLocationSpawnPatch.bossesInRaid) {
                NotificationManagerClass.DisplayMessageNotification($"{boss} will spawn.", ENotificationDurationType.Long);
            }
        }

        public static void Init() {
            if (Singleton<IBotGame>.Instantiated) {
                GClass5.GetOrAddComponent<BossNotifierMono>(Singleton<GameWorld>.Instance);
            }
        }

        public void Awake() {
            if (!BossNotifierPlugin.showNotificationsOnRaidStart.Value) return;
            SendBossNotifications();
        }

        public void Update() {
            if (BossNotifierPlugin.showBossesKeyCode.Value.IsDown()) {
                SendBossNotifications();
            }
        }

        public void OnDestroy() {
            BossLocationSpawnPatch.bossesInRaid.Clear();
        }
    }
}
