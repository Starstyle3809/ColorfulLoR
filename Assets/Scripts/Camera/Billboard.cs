using UnityEngine;

/// <summary>
/// 讓 2D 角色精靈在 3D 世界中永遠面向主攝影機（紙片人效果）。
/// 掛在每個含有 SpriteRenderer 的角色根物件上。
/// </summary>
public class Billboard : MonoBehaviour
{
    private Camera _mainCam;

    [Tooltip("只鎖定 Y 軸旋轉（等距視角建議開啟）")]
    public bool lockYAxisOnly = false;

    private void Start()
    {
        _mainCam = Camera.main;
    }

    // LateUpdate 確保攝影機已經移動完畢再更新朝向
    private void LateUpdate()
    {
        if (_mainCam == null) return;

        if (lockYAxisOnly)
        {
            // 僅水平旋轉（適合 Isometric 探索視角）
            Vector3 dir = _mainCam.transform.forward;
            dir.y = 0f;
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(dir);
        }
        else
        {
            // 完整面向攝影機（適合 2.5D 側視戰鬥視角）
            transform.rotation = Quaternion.LookRotation(
                transform.position - _mainCam.transform.position
            );
        }
    }
}
