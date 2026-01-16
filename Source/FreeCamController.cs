using UnityEngine;
using Cinemachine;

namespace DebugMod;

public class FreeCamController : MonoBehaviour
{
    private CinemachineVirtualCamera vCam;
    private Transform originalTarget;
    private GameObject proxyTarget;

    public static bool IsEnabled = false;
    private Vector3 lastMousePos;

    public void Toggle()
    {
        // 每次切換都重新抓取當前活耀的虛擬相機，防止引用失效
        vCam = FindObjectOfType<CinemachineVirtualCamera>();
        if (vCam == null) return;

        IsEnabled = !IsEnabled;

        if (IsEnabled)
        {
            originalTarget = vCam.Follow;
            if (proxyTarget == null) proxyTarget = new GameObject("FreeCamProxy");

            proxyTarget.transform.position = originalTarget.position;
            vCam.Follow = proxyTarget.transform;
            vCam.LookAt = proxyTarget.transform;

            // 禁用邊界限制組件 (如果有的話)，防止無法移動或縮放
            var confiner = vCam.GetComponent<CinemachineConfiner>();
            if (confiner != null) confiner.enabled = false;
        }
        else
        {
            if (originalTarget != null)
            {
                vCam.Follow = originalTarget;
                vCam.LookAt = originalTarget;
            }

            var confiner = vCam.GetComponent<CinemachineConfiner>();
            if (confiner != null) confiner.enabled = true;
        }
    }

    private void Update()
    {
        // 確保相機引用始終有效（放在最前面，不論是否開啟自由相機）
        if (vCam == null) vCam = FindObjectOfType<CinemachineVirtualCamera>();
        if (vCam == null) return;

        // --- 1. 處理縮放 (移到 IsEnabled 判斷之前，讓平常也能縮放) ---
        if (Input.GetKey(KeyCode.LeftControl))
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0f)
            {
                if (vCam.m_Lens.Orthographic)
                {
                    float zoomSpeed = vCam.m_Lens.OrthographicSize * 0.2f;
                    vCam.m_Lens.OrthographicSize -= scroll * zoomSpeed * 5f;
                    vCam.m_Lens.OrthographicSize = Mathf.Clamp(vCam.m_Lens.OrthographicSize, 0.1f, 500f);
                }
                else
                {
                    vCam.m_Lens.FieldOfView -= scroll * 50f;
                    vCam.m_Lens.FieldOfView = Mathf.Clamp(vCam.m_Lens.FieldOfView, 1f, 170f);
                }
            }
        }

        // --- 2. 重置縮放 (平常也能重置) ---
        if (Input.GetKeyDown(KeyCode.Mouse2) || Input.GetKeyDown(KeyCode.Backspace))
        {
            if (vCam.m_Lens.Orthographic)
                vCam.m_Lens.OrthographicSize = 10f;
            else
                vCam.m_Lens.FieldOfView = 60f;

            // 如果自由相機開著，通知代理點；如果沒開，通知玩家目標
            Transform target = IsEnabled ? proxyTarget.transform : originalTarget;
            if (target != null) vCam.OnTargetObjectWarped(target, Vector3.zero);

            //DebugMod.Logger.LogInfo("相機縮放已重置");
        }

        // --- 只有在自由相機開啟時才執行的邏輯 (移動) ---
        if (!IsEnabled || proxyTarget == null) return;

        // --- 3. 滑鼠拖拽移動 (左 Alt + 滑鼠左鍵) ---
        if (Input.GetKey(KeyCode.LeftAlt))
        {
            if (Input.GetKeyDown(KeyCode.Mouse0))
            {
                lastMousePos = Input.mousePosition;
            }

            if (Input.GetKey(KeyCode.Mouse0))
            {
                Vector3 mouseDelta = Input.mousePosition - lastMousePos;
                lastMousePos = Input.mousePosition;

                float sensitivity = vCam.m_Lens.OrthographicSize * 0.003f;
                Vector3 worldMove = new Vector3(mouseDelta.x, mouseDelta.y, 0) * sensitivity;

                proxyTarget.transform.position -= worldMove;
                vCam.PreviousStateIsValid = false;
                vCam.OnTargetObjectWarped(proxyTarget.transform, -worldMove);
            }
        }
    }
}