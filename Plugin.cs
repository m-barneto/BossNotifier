using BepInEx;
using Aki.Reflection.Patching;
using System.Reflection;
using UnityEngine;

namespace BossNotifier {
    [BepInPlugin("Mattdokn.BossNotifier", "BossNotifier", "1.0.0")]
    public class Plugin : BaseUnityPlugin {
        WaitForSeconds five_seconds = new WaitForSeconds(5f);

        private void Awake() {
            Logger.LogInfo($"Plugin BossNotifier is loaded!");
            new BossLocationSpawnPatch().Enable();
        }
    }
    internal class BossLocationSpawnPatch : ModulePatch {
        protected override MethodBase GetTargetMethod() => typeof(BossLocationSpawn).GetMethod("Init");

        [PatchPostfix]
        private static void PatchPostfix(BossLocationSpawn __instance) {
            if (__instance.ShallSpawn) {
                NotificationManagerClass.DisplayMessageNotification($"Boss {__instance.BossName} spawned.", EFT.Communications.ENotificationDurationType.Long);
            }
        }
    }
}
