using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DebugMod
{
    public class DebugInfoView : MonoBehaviour
    {
        private bool _uiShowDebug = true;
        private Rect _boxPos = new Rect(10f, 10f, 320f, 450f);

        private GameObject _player;
        private StringBuilder _sb = new StringBuilder(512);
        private string _displayString = "";
        private string _cachedVersion = "";

        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;
        private bool _stylesInitialized = false;

        public static Rect CurrentWindowRect { get; private set; }

        private void Awake()
        {
            _cachedVersion = Application.version;
        }

        private void Update()
        {
            if (Input.GetKeyDown(DebugMod.DebugInfoKey.Value)) _uiShowDebug = !_uiShowDebug;
            if (!_uiShowDebug) return;

            // 僅抓取 GameObject 用於座標取得
            if (_player == null)
            {
                var gm = GameManager.instance;
                if (gm != null && gm.playerStateMachine != null)
                {
                    _player = gm.playerStateMachine.gameObject;
                }
            }

            SampleData();
        }

        // 在 DebugInfoView 類別內
        private static string _lastEnemyHitInfo = "No Enemy Hit";

        public static void SetLastEnemyInfo(string info)
        {
            _lastEnemyHitInfo = info;
        }

        private void SampleData()
        {
            _sb.Clear();
            _sb.Append("<color=#00FFCC>[INARI DEBUG INFO]</color> "); // 改用你喜歡的青綠色
            _sb.AppendLine($"<size=14>v{_cachedVersion}</size>");

            // 1. 先安全取得 GameManager 實例
            var gm = GameManager.instance;

            // 2. 徹底的安全檢查：確保 gm, DataManager, OutGameData 都不是 null
            if (gm != null && gm.DataManager != null && gm.DataManager.OutGameData != null)
            {
                var outData = gm.DataManager.OutGameData;

                if (gm.CurrentGameSequenceManager != null &&
                gm.CurrentGameSequenceManager.currentSceneRoot != null)
                {
                    string sceneName = gm.CurrentGameSequenceManager.currentSceneRoot.SceneName;
                    _sb.Append("Scene: ").AppendLine(!string.IsNullOrEmpty(sceneName) ? sceneName : "Unknown Scene");
                }
                else
                {
                    _sb.AppendLine("Scene: [Manager or Root is Null]");
                }

                // 3. 檢查玩家相關實體是否存在
                if (_player != null && gm.playerStateMachine != null)
                {
                    var psm = gm.playerStateMachine;
                    var runTime = psm.PlayerRunTimeData;

                    // 取得開關狀態
                    bool infiniteStamina = DebugMod.IsNoCostStaminaEnabled?.Value ?? false;
                    bool godMode = DebugMod.IsNoDecreaseHpEnabled?.Value ?? false;

                    string staColor = infiniteStamina ? "<color=#FF0000>" : "<color=white>";
                    string hpColor = godMode ? "<color=#FF0000>" : "<color=white>";

                    // 取得數值 (安全存取 runTime)
                    float curSta = runTime.Stamina;
                    float maxSta = runTime.MaxStamina;
                    int curHp = runTime.playerHp;

                    // 基礎資訊
                    _sb.Append("Stamina: ").Append(staColor).Append(curSta.ToString("F0")).Append("/").Append(maxSta.ToString("F0")).Append("</color>")
                       .Append(" | Hp: ").Append(hpColor).Append(curHp).AppendLine("</color>");

                    // 座標與速度
                    Vector3 pos = _player.transform.position;
                    _sb.Append("Pos: x:").Append(pos.x.ToString("F2")).Append(", y:").AppendLine(pos.y.ToString("F2"));

                    Vector2 vel = psm.velocity;
                    Vector2 walVel = psm.wallClimbJumpVelocity;
                    _sb.Append("Vel: x:").Append(vel.x.ToString("F2")).Append(", y:").AppendLine(vel.y.ToString("F2"));
                    _sb.Append("WallJump: x:").Append(walVel.x.ToString("F2")).Append(", y:").AppendLine(walVel.y.ToString("F2"));

                    // 狀態與重生點
                    _sb.AppendLine($"PlayerState: <color=yellow>{psm.CurrentStateType}</color>");
                    _sb.AppendLine($"GameState: {gm.PlayMode}");
                    _sb.AppendLine($"Dead Count: {outData.PlayerDeadCount}");

                    _sb.AppendLine("<color=orange>--- Respawn Data ---</color>");

                    // 1. 存檔點名稱：增加醒目的標籤
                    string triggerName = string.IsNullOrEmpty(outData.SaveTriggerName) ? "None" : outData.SaveTriggerName;
                    _sb.Append("SaveNode: ").Append("<color=yellow>").Append(triggerName).AppendLine("</color>");

                    // 2. 當前重生點：增加距離監控 (方便確認是否真的有記錄到正確座標)
                    Vector3 curSpawn = outData.SpawnPoint;
                    float distToSpawn = Vector3.Distance(_player.transform.position, curSpawn);

                    _sb.Append("Current: ").Append("<color=cyan>")
                       .Append($"{curSpawn.x:F1}, {curSpawn.y:F1}")
                       .Append("</color>")
                       .Append($" <size=10>(Dist: {distToSpawn:F1})</size>").AppendLine();

                    // 3. 上一個重生點：只有在有值的時候才顯示，避免 0,0 佔空間
                    if (outData.BeforeSpawnPoint != Vector3.zero)
                    {
                        _sb.Append("LastScene: ").Append("<color=gray>")
                           .Append($"{outData.BeforeSpawnPoint.x:F1}, {outData.BeforeSpawnPoint.y:F1}")
                           .AppendLine("</color>");
                    }

                    // 4. 關卡初始點：同樣僅在非零時顯示
                    if (outData.StageSpawnPoint != Vector3.zero)
                    {
                        _sb.Append("Stage: ").Append("<color=#AAAAAA>")
                           .Append($"{outData.StageSpawnPoint.x:F1}, {outData.StageSpawnPoint.y:F1}")
                           .AppendLine("</color>");
                    }

                    _sb.AppendLine("<color=yellow>--- Last Enemy Hit ---</color>");
                    _sb.AppendLine(_lastEnemyHitInfo);
                    _sb.AppendLine("<color=cyan>--- Debug Mod States ---</color>");
                    string green = "<color=#00FF00>";
                    string end = "</color>";

                    _sb.AppendLine($"{DebugMod.DebugInfoKey.Value} DebugInfoMenu");

                    // 只有開啟的功能會套用綠色，關閉的維持原色
                    _sb.AppendLine($"{DebugMod.NoCostStaminaKey.Value} NoCostStamina: {(DebugMod.IsNoCostStaminaEnabled.Value ? $"{green}ON{end}" : "OFF")}");
                    _sb.AppendLine($"{DebugMod.NoDecreaseHpKey.Value} NoDecreaseHp: {(DebugMod.IsNoDecreaseHpEnabled.Value ? $"{green}ON{end}" : "OFF")}");
                    _sb.AppendLine($"{DebugMod.TeleportToSavePosKey.Value} TeleportToSavePos (Shift+{DebugMod.TeleportToSavePosKey.Value}: Save)");
                    _sb.AppendLine($"{DebugMod.OHKKey.Value} OHK: {(DebugMod.IsOHKEnabeld.Value ? $"{green}ON{end}" : "OFF")}");
                    _sb.AppendLine($"{DebugMod.ToggleSkipKey.Value} IsSkipCutScene: {(DebugMod.IsSkip.Value ? $"{green}ON{end}" : "OFF")}");
                    _sb.AppendLine($"{DebugMod.SaveStateKey.Value} SaveState");
                    _sb.AppendLine($"{DebugMod.LoadStateKey.Value} LoadState");
                    _sb.AppendLine($"{DebugMod.FreeCamKey.Value} FreeCam: {(FreeCamController.IsEnabled ? $"{green}ON{end}" : "OFF")}");
                    _sb.AppendLine($"{DebugMod.ColliderViewKey.Value} ColliderView (Ctrl: Config)");
                    _sb.AppendLine($"{DebugMod.TelelportToSpawnPointKey.Value} TelelportToSpawnPoint");

                    // --- 遊戲速度監控 ---
                    float currentTS = Time.timeScale;
                    float baseTS = DebugMod.forcedTS;

                    _sb.Append("GameSpeed: ");
                    string tsColor = currentTS > 1.05f ? "<color=#00FFCC>" : (currentTS < 0.95f ? "<color=#FFA500>" : "<color=white>");
                    _sb.Append(tsColor).Append(currentTS.ToString("F2")).Append("x</color>");

                    if (Mathf.Abs(currentTS - baseTS) > 0.01f)
                        _sb.Append(" (Base: ").Append(baseTS.ToString("F2")).Append("x)");
                    _sb.AppendLine();
                }
                else
                {
                    _sb.AppendLine("<color=yellow>Waiting for Player Entity...</color>");
                }
            }
            else
            {
                _sb.AppendLine("<color=red>Waiting for GameManager/Data...</color>");
            }

            _displayString = _sb.ToString();
        }

        // ... InitializeStyles 與 OnGUI 保持不變 ...
        private void InitializeStyles()
        {
            if (_stylesInitialized) return;
            _boxStyle = new GUIStyle(GUI.skin.box);
            Texture2D bg = new Texture2D(1, 1);
            bg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.8f));
            bg.Apply();
            _boxStyle.normal.background = bg;

            _labelStyle = new GUIStyle();
            _labelStyle.fontSize = 18;
            _labelStyle.fontStyle = FontStyle.Bold;
            _labelStyle.normal.textColor = Color.white;
            _labelStyle.padding = new RectOffset(10, 10, 10, 10);
            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            if (!_uiShowDebug || string.IsNullOrEmpty(_displayString))
            {
                CurrentWindowRect = Rect.zero; // 沒顯示時重置
                return;
            }

            InitializeStyles();
            float contentHeight = _labelStyle.CalcHeight(new GUIContent(_displayString), _boxPos.width);
            _boxPos.height = contentHeight + 10f;

            // 更新靜態變數供 ColliderView 參考
            CurrentWindowRect = _boxPos;

            GUI.Box(_boxPos, "", _boxStyle);
            GUI.Label(_boxPos, _displayString, _labelStyle);
        }
    }
}