using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Playables;
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

        [HarmonyPatch(typeof(InGameEntity), "RequestDamage", new Type[] { typeof(DamageInfo) })]
        [HarmonyPrefix]
        private static bool PatchRequestDamage(InGameEntity __instance, ref DamageInfo damageInfo)
        {
            if (__instance is EnemyInGameEntity enemy)
            {
                // 更新 UI 資訊
                DebugInfoView.SetLastEnemyInfo($"<color=red>[Hit]</color> {enemy.name}\nHP: {enemy.Health}/{enemy.MaximumHealth} | Dmg: {damageInfo.Amount}");

                if (DebugMod.IsOHKEnabeld.Value)
                {
                    var playerEntity = Singleton<GameManager>.Instance?.playerStateMachine?.Entity;

                    if (playerEntity != null && damageInfo.Source == playerEntity)
                    {
                        // 秒殺：直接設為最大血量
                        damageInfo.Amount = enemy.MaximumHealth;
                    }
                }
            }
            return true;
        }

        // --- Skip Tutorial ---
        [HarmonyPatch(typeof(TutorialHub), "ActivateStep")]
        [HarmonyPrefix]
        private static bool PatchActivateStep(TutorialHub __instance)
        {
            //if (!DebugMod.IsSkip.Value) return true;

            if (__instance.steps == null || __instance.steps.Count == 0) return true;

            // 直接跳到最後一步並執行結束邏輯
            __instance.index = __instance.steps.Count;
            __instance.FinishTutorial();
            __instance.Cleanup();

            // 【針對 03_1 後續教學的關鍵修正】
            // 強制將遊戲模式切回 GameMode，解開教學模式對 Input 的鎖定
            if (Singleton<GameManager>.Instance != null)
            {
                Singleton<GameManager>.Instance.PlayModeSetting(PlayMode.GameMode);
            }

            __instance.enabled = false;
            __instance.gameObject.SetActive(false); // 確保 UI 視窗徹底消失

            //DebugMod.Logger.LogInfo($"[DebugMod] 自動跳過教學: {__instance.name}");
            return false;
        }
        [HarmonyPatch(typeof(DialogueClipBehaviour), "ProcessFrame")]
        [HarmonyPrefix]
        private static bool PatchDialogueProcess(DialogueClipBehaviour __instance)
        {
            if (!DebugMod.IsSkip.Value) return true;

            // 強制把暫停邏輯關掉，讓 Timeline 像賽車一樣衝過去
            if (__instance.DialogueData != null)
            {
                __instance.DialogueData.StopOnComplete = false;
            }

            return true;
        }

        [HarmonyPatch(typeof(DialogueClipBehaviour), "OnInput")]
        [HarmonyPrefix]
        private static bool PatchDialogueInput(DialogueClipBehaviour __instance)
        {
            if (!DebugMod.IsSkip.Value) return true;
            
            if (__instance.director != null)
            {
                __instance.director.time = __instance.EndTime;
                __instance.director.Play();
            }

            Singleton<GameManager>.Instance.DialogueManager.EndDialogue(__instance);
            return false;
        }

        // Skip Timeline
        [HarmonyPatch(typeof(TimelineModulePlayer), "Play")]
        [HarmonyPrefix]
        private static bool PatchTimelineModulePlayer(TimelineModulePlayer __instance)
        {
            if (!DebugMod.IsSkip.Value) return true;
            //DebugMod.Logger.LogInfo($"[DebugMod] 偵測到 Timeline: {__instance.gameObject.name}，準備處理...");

            // 2. 啟動協程（不論是不是 HUB 都進去，由協程內部判斷速度）
            __instance.StartCoroutine(AutoSkipRoutine(__instance));

            return true;
        }

        // --- Skip Timeline ---
        private static IEnumerator AutoSkipRoutine(TimelineModulePlayer player)
        {
            yield return null;
            if (player == null) yield break;

            string name = player.gameObject.name;

            // 特例排除
            if (name.Contains("CUTSCENE_Captain_Simple") || name.Contains("CUTSCENE_Captain_Finish"))
            {
                yield break;
            }

            if (player.CanSkip)
            {
                player.pressedSkip = true;
                player.keyHoldTimer = 2.0f;
            }
            else
            {
                player.SetPlaybackSpeed(100f);
            }
        }

        // Skip Timer Attack 
        [HarmonyPatch(typeof(TimeAttackTrigger), "NotifyObservers")]
        [HarmonyPrefix]
        private static bool PatchTimeAttackTrigger(TimeAttackTrigger __instance)
        {
            if (!DebugMod.IsSkip.Value) return true;

            Singleton<GameManager>.Instance.PlayerStateMachine.InAreaTrigger = null;
            __instance.interactiveInGameUI.SetTrigger(false);
            __instance.GetComponent<Collider2D>().enabled = false;
            __instance.IsDataChanged = true;

            // 2. 紀錄存盤點
            Singleton<GameManager>.Instance.DataManager.OutGameData.SpawnPoint = __instance.transform.position;
            Singleton<GameManager>.Instance.DataManager.SaveInGameData();
            Singleton<GameManager>.Instance.DataManager.SaveOutGameData();

            // 3. 【關鍵：跳過相機移動，直接執行結果】
            // 這裡直接執行 PlayerInteract 協程原本最後會做的事

            // 開啟起始門
            if (__instance.startDoor != null)
            {
                __instance.startDoor.Notify(__instance, ObserverType.Default);
            }

            // 播放計時器動畫
            __instance.ani.Play("timer_run");

            // 啟動目的地與 UI 計時器
            __instance.dest.StartTimer();
            __instance.StartTimeAttack();

            //DebugMod.Logger.LogInfo($"[DebugMod] 已跳過 {__instance.name} 的相機過場，計時開始。");

            return false; // 返回 false 表示不執行原版代碼，從而跳過相機協程
        }
    }
}