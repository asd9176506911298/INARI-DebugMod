using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace DebugMod;

[BepInPlugin("com.yuki.inari.debugmod", "DebugMod", "1.0.0")]
public class DebugMod : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
    private Harmony harmony = null!;

    public static ConfigEntry<KeyCode> DebugInfoKey;
    public static ConfigEntry<KeyCode> NoCostStaminaKey;
    public static ConfigEntry<KeyCode> NoDecreaseHpKey;
    public static ConfigEntry<KeyCode> FreeCamKey;
    public static ConfigEntry<KeyCode> TelelportToSpawnPointKey;
    public static ConfigEntry<bool> IsNoCostStaminaEnabled;
    public static ConfigEntry<bool> IsNoDecreaseHpEnabled;

    private FreeCamController _freeCam;
    private ColliderView _colliderView;
    private DebugInfoView _debugInfoView;

    // 用來記錄手動設定的時間倍率
    public static float forcedTS = 1f;

    private void Awake()
    {
        gameObject.hideFlags = HideFlags.HideAndDontSave;
        harmony = Harmony.CreateAndPatchAll(typeof(DebugMod).Assembly);
        Logger = base.Logger;

        IsNoCostStaminaEnabled = Config.Bind<bool>("", "NoCostStamina", false, "");
        IsNoDecreaseHpEnabled = Config.Bind<bool>("", "NoDecreaseHp", false, "");
        DebugInfoKey = Config.Bind<KeyCode>("", "DebugInfoKey", KeyCode.F1, "");
        NoCostStaminaKey = Config.Bind<KeyCode>("", "NoCostStaminaKey", KeyCode.F2, "");
        NoDecreaseHpKey = Config.Bind<KeyCode>("", "NoDecreaseHpKey", KeyCode.F3, "");
        TelelportToSpawnPointKey = Config.Bind<KeyCode>("", "TelelportToSpawnPointKey", KeyCode.F4, "");
        FreeCamKey = Config.Bind<KeyCode>("", "FreeCamKey", KeyCode.F10, "");

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
    }

    private void OnDestroy()
    {
        harmony.UnpatchSelf();
    }
}