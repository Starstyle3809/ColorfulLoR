using UnityEngine;

/// <summary>
/// 讓 2D 角色精靈在 3D 世界中完美呈現（紙片人效果）。
/// 自動相容：探索模式（俯視角）時圖片會平躺貼合地板；戰鬥模式（側視角）時會自動立起面向鏡頭！
/// </summary>
public class Billboard : MonoBehaviour
{
    private Camera _mainCam;

    [Tooltip("只鎖定 Y 軸旋轉（等距視角建議開啟）")]
    public bool lockYAxisOnly = false;

    private void Start()
    {
        _mainCam = Camera.main;

        // ★ 修正：將訂閱移到 Start
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged += HandleStateChanged;
            HandleStateChanged(GameManager.Instance.CurrentState);
        }
    }

    private void OnDestroy()
    {
        // ★ 修正：改在物件被銷毀時取消訂閱
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleStateChanged;
        }
    }

    private void HandleStateChanged(GameManager.GameState newState)
    {
        if (newState == GameManager.GameState.Explore)
        {
            // 【探索模式 - 俯視角】強制平躺
            this.enabled = false;
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
        else
        {
            // 【戰鬥模式 - 側視角】啟動面向鏡頭
            this.enabled = true;
        }
    }

    private void LateUpdate()
    {
        if (_mainCam == null) return;

        if (_mainCam.orthographic)
        {
            // ★ 正交視角專用：絕對平行對齊！直接複製攝影機的旋轉角度，紙片人絕對不會變形
            transform.rotation = _mainCam.transform.rotation;
        }
        else
        {
            // 透視視角專用：面向鏡頭
            if (lockYAxisOnly)
            {
                Vector3 dir = _mainCam.transform.forward;
                dir.y = 0f;
                if (dir != Vector3.zero) transform.rotation = Quaternion.LookRotation(dir);
            }
            else
            {
                transform.rotation = Quaternion.LookRotation(transform.position - _mainCam.transform.position);
            }
        }
    }
}