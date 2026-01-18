using UnityEngine;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;

namespace DebugMod
{
    // 1. 監聽 Boss 的閃現動作
    [HarmonyPatch(typeof(BossBlackboard), "OnTeleport")]
    public class BossTeleportHook
    {
        // 關鍵修正：將參數名稱從 position 改為 targetPosition 以匹配原版方法
        static void Postfix(BossBlackboard __instance, Vector3 targetPosition)
        {
            // 當 Boss 執行 OnTeleport 時
            var player = Object.FindObjectOfType<PlayerStateMachine>();
            if (player != null)
            {
                // 計算指向 Boss 剛傳送到的新位置 (targetPosition)
                // 這裡稍微加一點高度補償 (+0.5f)，確保射向中心
                Vector3 targetCenter = targetPosition + new Vector3(0, 0.5f, 0);
                Vector3 dir = targetCenter - player.transform.position;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

                // 角色立刻回敬一發飛鏢
                player.CreateShuriken(angle);

                DebugMod.Logger.LogInfo($"[Counter] Boss Teleported to {targetPosition}. Auto-Counter Fired!");
            }
        }
    }

    // 2. 自動瞄準補丁 (維持不變)
    [HarmonyPatch(typeof(PlayerStateMachine), "CreateShuriken")]
    public class ShurikenAutoAimPatch
    {
        static void Prefix(PlayerStateMachine __instance, ref float angle)
        {
            var bb = Object.FindObjectOfType<BossBlackboard>();
            if (bb == null) return;

            // 鎖定 Boss 中心點
            Vector3 targetPos = bb.transform.position + new Vector3(0, 0.5f, 0);
            Vector3 dir = targetPos - __instance.transform.position;

            angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }
    }

    // 解決編譯錯誤的預測器類別
    public class TeleportPredictor : MonoBehaviour { }
}