using UnityEngine;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;

namespace DebugMod
{
    public class CounterMod
    {
        public static bool IsProcessingTeleport = false;
        // 在這裡設定 BOSS 的速度倍率 (1.0 是原速，2.5 是極快)
        public static float BossSpeedMultiplier = 10f;
    }

    // --- 新增：BOSS 動作加速補丁 ---
    [HarmonyPatch(typeof(BossBlackboard), "LateUpdate")]
    public class BossSpeedPatch
    {
        static void Prefix(BossBlackboard __instance)
        {
            if (__instance == null || __instance.IsDead) return;

            // 強制設定位移與動畫速度
            __instance.MovementSpeed = CounterMod.BossSpeedMultiplier;

            // 如果有 Animator，同步縮放動畫播放速度
            if (__instance.Animator != null)
            {
                __instance.Animator.speed = CounterMod.BossSpeedMultiplier;
            }
        }
    }

    // --- 1. 監聽 Boss 的傳送動作並反擊 ---
    [HarmonyPatch(typeof(BossBlackboard), "OnTeleport")]
    public class BossTeleportHook
    {
        static void Postfix(BossBlackboard __instance, Vector3 targetPosition)
        {
            var gameManager = Singleton<GameManager>.Instance;
            var player = gameManager?.playerStateMachine;
            if (player == null) return;

            Vector3 targetCenter = targetPosition + new Vector3(0, 0.5f, 0);
            Vector3 dir = targetCenter - player.GetColliderCenter();
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            player.CreateShuriken(angle);
        }
    }

    // --- 2. 當 Boss 受傷時，自動觸發飛鏢瞬移 ---
    [HarmonyPatch(typeof(BossBlackboard), "OnDamaged")]
    public class BossDamagedHook
    {
        static void Postfix(BossBlackboard __instance, DamageInfo damageInfo)
        {
            if (CounterMod.IsProcessingTeleport) return;

            var gameManager = Singleton<GameManager>.Instance;
            var player = gameManager?.playerStateMachine;
            if (player == null) return;

            // 核心變動：一受傷就強制噴出一發飛鏢
            // 這樣可以確保連擊不會中斷
            Vector3 diff = (Vector3)__instance.Collision2D.GetCenter() - (Vector3)player.GetColliderCenter();
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
            player.CreateShuriken(angle);

            if (player.ThrowingShurikens == null || player.ThrowingShurikens.Count == 0) return;

            var lastShuriken = player.ThrowingShurikens.LastOrDefault();
            // 只要飛鏢射出去了（不管有沒有 Hit），我們就嘗試瞬移
            if (lastShuriken == null) return;

            try
            {
                CounterMod.IsProcessingTeleport = true;

                // 忽略體力限制，強制執行瞬移以跟上加速後的 BOSS
                player.PlayerRunTimeData.AddStamina(100f);
                player.ShurikenTeleport();
            }
            catch (System.Exception e)
            {
                Debug.Log($"[Teleport Error] {e.Message}");
            }
            finally
            {
                CounterMod.IsProcessingTeleport = false;
            }
        }
    }

    // --- 3. 自動瞄準補丁 ---
    [HarmonyPatch(typeof(PlayerStateMachine), "CreateShuriken")]
    public class ShurikenAutoAimPatch
    {
        static void Prefix(PlayerStateMachine __instance, ref float angle)
        {
            var boss = Object.FindObjectOfType<BossBlackboard>();
            if (boss == null) return;

            Vector3 targetPos = boss.transform.position + new Vector3(0, 0.5f, 0);
            Vector2 dir = (targetPos - __instance.GetColliderCenter());

            if (dir.magnitude < 50f) // 擴大搜尋範圍以配合加速後的 BOSS
            {
                angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            }
        }
    }
}