using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace DebugMod
{
    [HarmonyPatch(typeof(InGameDebugTool))]
    public static class InGameDebugTool_UI_Patch
    {
        private static Font _customFont;
        private static bool _fontAttempted = false;

        private static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            ["무적 모드"] = "God Mode",
            ["자원들 충전"] = "Refill Resources",
            ["상태 해금"] = "Unlock Status",
            ["수리검 대쉬 해금"] = "Unlock Shuriken Dash",
            ["강공격 해금"] = "Unlock Strong Attack",
            ["스토리모드 여부"] = "Story Mode",
            ["스태미나 충전"] = "Refill Stamina",
            ["스킬게이지 충전"] = "Refill Skill Gauge",
            ["씬 재시작"] = "Restart Scene",
            ["씬 관리자"] = "Scene Manager",
            ["씬 선택하기"] = "Select Scene",
            ["언어 설정"] = "Language Settings",
            ["언어 선택하기"] = "Select Language",
            ["씬 목록이 비어있습니다"] = "Scene list is empty"
        };

        private static readonly Dictionary<string, string> Ja = new Dictionary<string, string>
        {
            ["무적 모드"] = "無敵モード",
            ["자원들 충전"] = "リソース回復",
            ["상태 해금"] = "ステータス解禁",
            ["수리검 대쉬 해금"] = "手裏剣ダッシュ解禁",
            ["강공격 해금"] = "強攻撃解禁",
            ["스토리모드 여부"] = "ストーリーモード",
            ["스태미나 충전"] = "スタ미ナ回復",
            ["스킬게이지 충전"] = "スキルゲージ回復",
            ["씬 재시작"] = "リスタート",
            ["씬 관리자"] = "シーンマネージャー",
            ["씬 선택하기"] = "シーン選択",
            ["언어 설정"] = "言語設定",
            ["언어 선택하기"] = "言語選択"
        };

        [HarmonyPatch(typeof(GameManager), "OnEnable")]
        [HarmonyPostfix]
        private static void PatchGameManager(GameManager __instance)
        {
            var debugTool = __instance.GetComponent<InGameDebugTool>();
            if (debugTool != null) debugTool.enabled = true;
        }

        [HarmonyPatch("BuildUI")]
        [HarmonyPostfix]
        private static void AfterBuildUI(InGameDebugTool __instance) => ApplyFullTranslation(__instance);

        [HarmonyPatch("ChangeLocale")]
        [HarmonyPostfix]
        private static void AfterChangeLocale(InGameDebugTool __instance) => ApplyFullTranslation(__instance);

        [HarmonyPatch("ToggleGodMode")]
        [HarmonyPostfix]
        private static void AfterToggleGodMode(InGameDebugTool __instance) => ApplyFullTranslation(__instance);

        private static void ApplyFullTranslation(InGameDebugTool __instance)
        {
            if (__instance.rootElement == null) return;

            // 使用 schedule 延遲執行 (例如延遲 100 毫秒)，避開切換語言時的組件銷毀衝突
            __instance.rootElement.schedule.Execute(() =>
            {
                // 再次檢查防止延遲執行時對象已被銷毀
                if (__instance == null || __instance.rootElement == null) return;

                if (!_fontAttempted) LoadExternalFont();

                // 安全獲取語言代碼
                string code = "en";
                try
                {
                    code = LocalizationSettings.SelectedLocale.Identifier.Code.ToLower();
                }
                catch { }

                var dict = code.StartsWith("ko") ? null : (code.StartsWith("ja") ? Ja : En);

                UpdateUIRecursive(__instance.rootElement, dict);
            }).ExecuteLater(100);
        }

        private static void LoadExternalFont()
        {
            _fontAttempted = true;
            try
            {
                string dllPath = Assembly.GetExecutingAssembly().Location;
                string folder = Path.GetDirectoryName(dllPath);

                // 請確認你的檔案名稱是這個，或者改成 NotoSansJP-VariableFont_wght.ttf
                string fontPath = Path.Combine(folder, "NotoSansJP-Regular.ttf");

                if (File.Exists(fontPath))
                {
                    _customFont = new Font(fontPath);
                }
            }
            catch (Exception) { /* 忽略載入錯誤 */ }
        }

        private static void UpdateUIRecursive(VisualElement element, Dictionary<string, string> dict)
        {
            // --- 字體處理策略 ---
            if (dict != null && _customFont != null)
            {
                // 非韓文模式：強制套用我們載入的 NotoSans（解決英日文亂碼/缺字）
                element.style.unityFont = _customFont;
                element.style.unityFontDefinition = new StyleFontDefinition(_customFont);
            }
            else
            {
                // 韓文模式 (dict 為 null)：清除 Mod 的字體設定，讓它吃 Unity 遊戲原本的 Theme/USS 設定
                element.style.unityFont = new StyleFont(StyleKeyword.Null);
                element.style.unityFontDefinition = new StyleFontDefinition(StyleKeyword.Null);
            }

            // --- 文本翻譯處理 ---
            if (element is Button btn) UpdateText(btn, dict);
            else if (element is Label lbl) UpdateText(lbl, dict);
            else if (element is Toggle tgl) UpdateText(tgl, dict);

            // 遞迴處理子物件
            foreach (var child in element.Children()) UpdateUIRecursive(child, dict);
        }

        private static void UpdateText(VisualElement el, Dictionary<string, string> dict)
        {
            string current = GetElementText(el);
            if (string.IsNullOrEmpty(current)) return;

            string key = FindOriginalKey(current);

            if (dict == null)
            {
                // 如果是韓文，直接把 Key (也就是原本的韓文) 塞回去
                // 處理特殊的「無敵模式: ON/OFF」後綴狀況
                if (key == "무적 모드")
                {
                    bool isOn = InGameDebugTool.Instance.playModeTestTool.IsPlayerInvincible;
                    SetElementText(el, $"{key}: {(isOn ? "ON" : "OFF")}");
                }
                else
                {
                    SetElementText(el, key);
                }
            }
            else if (dict.ContainsKey(key))
            {
                // 英文或日文模式，從字典取值
                string txt = dict[key];
                if (key == "무적 모드")
                {
                    bool isOn = InGameDebugTool.Instance.playModeTestTool.IsPlayerInvincible;
                    txt += ": " + (isOn ? "ON" : "OFF");
                }
                SetElementText(el, txt);
            }
        }

        private static string FindOriginalKey(string current)
        {
            string clean = current.Split(':')[0].Trim();
            if (En.ContainsKey(clean)) return clean;

            // 修正點：將 "=" 改為 "in"
            foreach (var kvp in En) if (current.Contains(kvp.Value)) return kvp.Key;
            foreach (var kvp in Ja) if (current.Contains(kvp.Value)) return kvp.Key;

            return clean;
        }

        private static string GetElementText(VisualElement el)
        {
            if (el is Button b) return b.text;
            if (el is Label l) return l.text;
            if (el is Toggle t) return t.label;
            return null;
        }

        private static void SetElementText(VisualElement el, string txt)
        {
            if (el is Button b) b.text = txt;
            else if (el is Label l) l.text = txt;
            else if (el is Toggle t) t.label = txt;
        }

        //No Decrease Stamina
        [HarmonyPatch(typeof(PlayerRunTimeData), "AddStamina")]
        [HarmonyPrefix]
        private static bool PatchAddStamina(PlayerRunTimeData __instance, float _value, bool _isKill = false)
        {
            if (_value < 0 && DebugMod.IsNoCostStaminaEnabled.Value)
                return false;

            return true;
        }

        //No Decrease Health
        [HarmonyPatch(typeof(PlayerRunTimeData), "AddPlayerHp")]
        [HarmonyPrefix]
        private static bool PatchAddPlayerHp(PlayerRunTimeData __instance, int _value)
        {
            if (_value < 0 && DebugMod.IsNoDecreaseHpEnabled.Value)
                return false;

            return true;
        }

        [HarmonyPatch(typeof(EnemyInGameEntity), "OnDamaged")]
        [HarmonyPostfix]
        private static void PatchOnDamaged(EnemyInGameEntity __instance, DamageInfo damageInfo)
        {
            // 組合資訊字串
            string info = $"<color=red>[Hit]</color> {__instance.name}\nHP: {__instance.Health}/{__instance.MaximumHealth} | Dmg: {damageInfo.Amount}";

            // 傳送到 Debug 面板
            DebugInfoView.SetLastEnemyInfo(info);

            // 原有的 Log 保持不變B
            //DebugMod.Logger.LogInfo($"{__instance.name} {__instance.Health} / {__instance.MaximumHealth} {damageInfo.Amount}");
        }
    }
}