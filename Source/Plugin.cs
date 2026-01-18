using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using System.IO;

namespace DebugMod;

[BepInPlugin("com.yuki.inari.debugmod", "DebugMod", "1.0.0")]
public class DebugMod : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
    private Harmony harmony = null!;

    public static ConfigEntry<KeyCode> DebugInfoKey;
    public static ConfigEntry<KeyCode> NoCostStaminaKey;
    public static ConfigEntry<KeyCode> NoDecreaseHpKey;
    public static ConfigEntry<KeyCode> TeleportToSavePosKey;
    public static ConfigEntry<KeyCode> OHKKey;
    public static ConfigEntry<KeyCode> ToggleSkipKey;
    public static ConfigEntry<KeyCode> SaveStateKey;
    public static ConfigEntry<KeyCode> LoadStateKey;
    public static ConfigEntry<KeyCode> FreeCamKey;
    public static ConfigEntry<KeyCode> ColliderViewKey;
    public static ConfigEntry<KeyCode> TelelportToSpawnPointKey;
    public static ConfigEntry<bool> IsNoCostStaminaEnabled;
    public static ConfigEntry<bool> IsNoDecreaseHpEnabled;
    public static ConfigEntry<bool> IsOHKEnabeld;
    public static ConfigEntry<bool> IsSkip;

    private FreeCamController _freeCam;
    private ColliderView _colliderView;
    private DebugInfoView _debugInfoView;

    // 改用字典存儲：Key 是場景名，Value 是座標
    public static Dictionary<string, Vector3> sceneSavedPositions = new Dictionary<string, Vector3>();

    // 用來記錄手動設定的時間倍率
    public static float forcedTS = 1f;

    private void Awake()
    {
        gameObject.hideFlags = HideFlags.HideAndDontSave;
        harmony = Harmony.CreateAndPatchAll(typeof(DebugMod).Assembly);
        Logger = base.Logger;

        IsNoCostStaminaEnabled = Config.Bind<bool>("", "NoCostStamina", false, "");
        IsNoDecreaseHpEnabled = Config.Bind<bool>("", "NoDecreaseHp", false, "");
        IsOHKEnabeld = Config.Bind<bool>("", "OneHitKill", false, "");
        IsSkip = Config.Bind<bool>("", "IsSkip", true, "");
        DebugInfoKey = Config.Bind<KeyCode>("", "DebugInfoKey", KeyCode.F1, "");
        NoCostStaminaKey = Config.Bind<KeyCode>("", "NoCostStaminaKey", KeyCode.F2, "");
        NoDecreaseHpKey = Config.Bind<KeyCode>("", "NoDecreaseHpKey", KeyCode.F3, "");
        TeleportToSavePosKey = Config.Bind<KeyCode>("", "TeleportToSavePosKey", KeyCode.F4, "");
        OHKKey = Config.Bind<KeyCode>("", "OHKKey", KeyCode.F5, "");
        ToggleSkipKey = Config.Bind<KeyCode>("", "ToggleSkipKey", KeyCode.F6, "");
        SaveStateKey = Config.Bind<KeyCode>("", "SaveStateKey", KeyCode.F8, "");
        LoadStateKey = Config.Bind<KeyCode>("", "LoadStateKey", KeyCode.F9, "");
        FreeCamKey = Config.Bind<KeyCode>("", "FreeCamKey", KeyCode.F10, "");
        ColliderViewKey = Config.Bind<KeyCode>("", "ColliderViewKey", KeyCode.F11, "");
        TelelportToSpawnPointKey = Config.Bind<KeyCode>("", "TelelportToSpawnPointKey", KeyCode.F12, "");

        _freeCam = gameObject.AddComponent<FreeCamController>();
        _colliderView = gameObject.AddComponent<ColliderView>();
        _debugInfoView = gameObject.AddComponent<DebugInfoView>();

        Logger.LogInfo($"DebugMod is loaded and working!");
    }

    private void Update()
    {
        // --- 原有的功能 ---
        if (Input.GetKeyDown(DebugMod.NoCostStaminaKey.Value))
            IsNoCostStaminaEnabled.Value = !IsNoCostStaminaEnabled.Value;

        if (Input.GetKeyDown(DebugMod.NoDecreaseHpKey.Value))
            IsNoDecreaseHpEnabled.Value = !IsNoDecreaseHpEnabled.Value;

        var teleportKey = DebugMod.TeleportToSavePosKey.Value;

        // 1. 判定 Shift + Key (存檔)
        if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(teleportKey))
        {
            // 按下按鍵時才檢查資料是否存在 (防止報錯)
            if (GameManager.instance?.DataManager?.OutGameData != null)
            {
                string currentScene = GameManager.instance.DataManager.OutGameData.CurrentSceneName;
                Vector3 pos = GameManager.instance.playerStateMachine.transform.position;
                sceneSavedPositions[currentScene] = pos;
                DebugMod.Logger.LogInfo($"[SavePos] Scene: {currentScene}, Pos: {pos}");
            }
        }
        // 2. 判定單鍵 (傳送)
        else if (Input.GetKeyDown(teleportKey))
        {
            // 按下按鍵時才檢查資料是否存在 (防止報錯)
            if (GameManager.instance?.DataManager?.OutGameData != null)
            {
                string currentScene = GameManager.instance.DataManager.OutGameData.CurrentSceneName;
                if (sceneSavedPositions.TryGetValue(currentScene, out Vector3 targetPos))
                {
                    GameManager.instance.playerStateMachine.transform.position = targetPos;
                    DebugMod.Logger.LogInfo($"[Teleport] To {currentScene} Saved Pos: {targetPos}");
                }
            }
        }

        if (Input.GetKeyDown(DebugMod.OHKKey.Value))
            IsOHKEnabeld.Value = !IsOHKEnabeld.Value;

        if (Input.GetKeyDown(DebugMod.ToggleSkipKey.Value))
            IsSkip.Value = !IsSkip.Value;

        if (Input.GetKeyDown(FreeCamKey.Value))
        {
            _freeCam.Toggle();
            Logger.LogInfo($"FreeCam: {FreeCamController.IsEnabled}");
        }

        if (Input.GetKeyDown(TelelportToSpawnPointKey.Value))
        {
            GameManager.instance.SceneTranslationManager.CurrentSceneRoot.TeleportToRespawnPoint();
        }

        // --- 時間加速功能 (模仿 SpeedrunTools) ---

        // 1. 按住 RShift 加速 (放開還原)
        if (Input.GetKeyDown(KeyCode.RightShift))
        {
            Time.timeScale = 2.0f; // 你可以自定義按住時的加速倍率
        }
        else if (Input.GetKeyUp(KeyCode.RightShift))
        {
            Time.timeScale = forcedTS; // 放開後回到你原本設定的速度
        }

        // 2. 使用 + / - 永久調整基礎速度
        if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus)) // Equals 鍵就是 += 鍵
        {
            forcedTS = Mathf.Clamp(forcedTS + 0.15f, 0f, 500f);
            Time.timeScale = forcedTS;
            //Logger.LogInfo($"當前遊戲速度: {forcedTS:F2}x");
        }
        else if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
        {
            forcedTS = Mathf.Clamp(forcedTS - 0.15f, 0f, 500f);
            Time.timeScale = forcedTS;
            //Logger.LogInfo($"當前遊戲速度: {forcedTS:F2}x");
        }
        else if (Input.GetKeyDown(KeyCode.Alpha0)) // 按 0 快速還原 1.0 倍速
        {
            forcedTS = 1f;
            Time.timeScale = 1f;
            //Logger.LogInfo("遊戲速度已重置為 1.0x");
        }

        // --- 傳送邏輯 ---
        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyUp(KeyCode.Mouse0))
        {
            Vector3 worldPos = GameManager.instance.playerStateMachine.inputController.WorldScreenMousePosition();
            GameManager.instance.playerStateMachine.transform.position = worldPos;
        }

        if (Input.GetKeyDown(SaveStateKey.Value))
        {
            string saveFilePath = GetSavePath();
            var gm = Singleton<GameManager>.Instance;
            var dm = gm.DataManager;
            var rt = gm.PlayerStateMachine.PlayerRunTimeData;

            // 直接更新字典
            var data = dm.currentInGameSaveData;
            data["ManualSavedHP"] = rt.PlayerHp;
            data["ManualSavedStamina"] = rt.Stamina;
            data["PlayerPos"] = gm.playerStateMachine.transform.position;

            // 批量保存其餘 Persistence 對象
            foreach (var dp in dm.DataPersistences)
            {
                var dict = dp.SaveData();
                if (dict == null) continue;
                foreach (var item in dict) data[item.Key] = item.Value;
            }

            ES3.Save("InGameData", data, saveFilePath);
            Logger.LogInfo($"[F8] Saved HP:{rt.PlayerHp}, Stamina:{rt.Stamina:F2}");
        }

        if (Input.GetKeyDown(LoadStateKey.Value))
        {
            string saveFilePath = GetSavePath();
            if (!ES3.FileExists(saveFilePath)) return;

            var gm = Singleton<GameManager>.Instance;
            var loadedData = ES3.Load("InGameData", saveFilePath, new Dictionary<string, object>());
            gm.DataManager.currentInGameSaveData = loadedData;

            // 提取數據 (使用模式匹配簡化轉換)
            float targetS = 90f;
            int targetH = 3;
            Vector3 savedPos = Vector3.zero;

            if (loadedData.TryGetValue("ManualSavedStamina", out var s)) targetS = System.Convert.ToSingle(s);
            if (loadedData.TryGetValue("ManualSavedHP", out var h)) targetH = System.Convert.ToInt32(h);
            if (loadedData.TryGetValue("PlayerPos", out object p) && p is Vector3 v) savedPos = v;

            // 獲取當前場景
            SceneRoot targetScene = gm.CurrentGameSequenceManager.CurrentSceneRoot;

            // 安全啟動：先停止所有相關協程防止衝突
            StopAllCoroutines();
            StartCoroutine(LoadAndRestoreRoutine(targetScene, savedPos, targetH, targetS));
        }
    }

    private string GetSavePath()
    {
        string dllPath = Assembly.GetExecutingAssembly().Location;
        string folder = Path.GetDirectoryName(dllPath);
        return Path.Combine(folder, "testSave");
    }

    private IEnumerator LoadAndRestoreRoutine(SceneRoot scene, Vector3 pos, int hp, float stamina)
    {
        var gm = Singleton<GameManager>.Instance;
        var stm = gm.SceneTranslationManager;

        // --- 第一階段：場景重載 ---
        stm.IsLoadingScene = true;
        gm.DataManager.ClearPersistenceData();
        yield return stm.UnloadAllScene();

        stm.CurrentSceneRoot = null;
        Ref<GameSequenceManager> newRootWrapper = new Ref<GameSequenceManager>();
        yield return stm.LoadScene(scene.SceneName, newRootWrapper);

        // 更新引用
        stm.CurrentSceneRoot = newRootWrapper.Value.CurrentSceneRoot;
        gm.CurrentGameSequenceManager = newRootWrapper.Value;
        stm.AddSceneRoot(newRootWrapper.Value);
        yield return stm.PerLoadNearScene(stm.CurrentSceneRoot);

        gm.ResetGameSetting();

        // --- 第二階段：玩家初始化 ---
        while (gm.PlayerStateMachine == null) yield return null;
        yield return new WaitForEndOfFrame();

        gm.PlayerStateMachine.OnTeleport(pos);
        gm.PlayerStateMachine.transform.position = pos;

        // 恢復遊戲環境
        gm.UpdateStreamScenes();
        gm.TimeManager.SetTimeScale(1f);
        stm.IsLoadingScene = false;
        gm.InputManager.EnablePlayerControlsIfNeeded(stm.CurrentSceneRoot);

        // --- 第三階段：數值守護鎖 (Duration: 1.0s) ---
        // 這部分取代了 ExecuteAfterReload，確保數值不會被載入後的 Start() 覆蓋
        yield return StartCoroutine(ValueLockRoutine(hp, stamina, 1.0f));
    }

    private IEnumerator ValueLockRoutine(int targetHp, float targetStam, float duration)
    {
        float timer = 0;
        var gm = Singleton<GameManager>.Instance;

        Logger.LogInfo($"[ValueLock] Locking stats for {duration}s");

        while (timer < duration)
        {
            if (gm.PlayerStateMachine?.PlayerRunTimeData != null)
            {
                var rt = gm.PlayerStateMachine.PlayerRunTimeData;

                // 檢查是否需要同步 (增加微小容差防止過度運算)
                if (rt.PlayerHp != targetHp || Mathf.Abs(rt.Stamina - targetStam) > 0.05f)
                {
                    rt.Stamina = targetStam;
                    rt.SetPlayerHp(targetHp);

                    // UI 刷新事件
                    rt.OnEvent(ModelEventType.StaminaCharged);
                    rt.OnEvent(ModelEventType.HpChanged);
                    rt.AddStamina(0f);
                }
            }
            timer += Time.unscaledDeltaTime;
            yield return null;
        }
        Logger.LogInfo("[ValueLock] Synchronization finished.");
    }

    private void OnDestroy()
    {
        harmony.UnpatchSelf();
    }
}