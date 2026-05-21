using UnityEngine;
using Unity.Cinemachine;
using System.Collections;

/// <summary>
/// 攝影機導演：負責調度所有鏡頭，並動態修改過渡(Blend)時間以配合人物衝刺。
/// 新增：FOV 瞬間縮放特效 (FOV Punch)，補足並強化打擊震動感！
/// </summary>
public class CameraDirector : MonoBehaviour
{
    public static CameraDirector Instance { get; private set; }

    [Header("三台虛擬攝影機")]
    public CinemachineCamera vcamExplore;
    public CinemachineCamera vcamBattleOverview;
    public CinemachineCamera vcamBattleClash;

    [Header("拼點 TargetGroup")]
    public CinemachineTargetGroup clashTargetGroup;

    [Header("螢幕震動")]
    public CinemachineImpulseSource impulseSource;

    private const int PRIORITY_ACTIVE = 20;
    private const int PRIORITY_INACTIVE = 10;

    // 攔截主攝影機的大腦，用來動態修改過渡時間
    private CinemachineBrain _brain;
    // 紀錄特寫鏡頭預設的視野大小
    private float _defaultClashFOV;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // 尋找主攝影機上的 CinemachineBrain
        if (Camera.main != null)
        {
            _brain = Camera.main.GetComponent<CinemachineBrain>();
        }

        if (vcamBattleClash != null)
        {
            _defaultClashFOV = vcamBattleClash.Lens.FieldOfView;
        }

        // 如果沒有手動綁定震動源，嘗試自動抓取
        if (impulseSource == null)
        {
            impulseSource = GetComponent<CinemachineImpulseSource>();
        }
    }

    public void SwitchToExplore()
    {
        SetBlendTime(1.0f); // 探索切換可以慢一點，比較自然
        SetPriority(vcamExplore, PRIORITY_ACTIVE);
        SetPriority(vcamBattleOverview, PRIORITY_INACTIVE);
        SetPriority(vcamBattleClash, PRIORITY_INACTIVE);
    }

    public void SwitchToBattleOverview()
    {
        // 戰鬥中退回總覽要快，與人物退回的時間 (0.2s) 匹配
        SetBlendTime(0.3f);
        SetPriority(vcamExplore, PRIORITY_INACTIVE);
        SetPriority(vcamBattleOverview, PRIORITY_ACTIVE);
        SetPriority(vcamBattleClash, PRIORITY_INACTIVE);
    }

    public void SwitchToBattleClash(Transform target1, Transform target2)
    {
        if (clashTargetGroup != null)
        {
            clashTargetGroup.Targets.Clear();
            // 將半徑設定為 2f，這樣特寫時鏡頭邊緣才不會把角色裁切掉
            clashTargetGroup.AddMember(target1, 1f, 2f);
            clashTargetGroup.AddMember(target2, 1f, 2f);
        }

        // 極速運鏡！將過渡時間設為與人物衝刺時間 (0.2s) 相同
        SetBlendTime(0.2f);

        SetPriority(vcamExplore, PRIORITY_INACTIVE);
        SetPriority(vcamBattleOverview, PRIORITY_INACTIVE);
        SetPriority(vcamBattleClash, PRIORITY_ACTIVE);
    }

    // ── 觸發打擊視覺回饋 (實體震動 + 鏡頭縮放) ──
    public void TriggerImpulse(float force = 1f)
    {
        // 1. 實體螢幕震動 (Cinemachine Impulse)
        if (impulseSource != null)
        {
            impulseSource.GenerateImpulse(force);
        }
        else
        {
            Debug.LogWarning("[Camera] 缺乏 CinemachineImpulseSource，無法觸發硬體震動！");
        }

        // 2. 觸發強烈的 FOV 縮放打擊特效 (Zoom Punch)
        if (gameObject.activeInHierarchy && vcamBattleClash != null)
        {
            StartCoroutine(FOVPunchRoutine(force));
        }
    }

    // 利用 FOV 瞬間縮小再放大的「頓挫感」，做出《廢墟圖書館》拼點時的強烈視覺張力
    private IEnumerator FOVPunchRoutine(float force)
    {
        // 根據攻擊力道決定鏡頭要拉多近
        float punchAmount = 8f * force;
        float targetFOV = _defaultClashFOV - punchAmount;

        // 瞬間把鏡頭拉近 (Hit)
        var lens = vcamBattleClash.Lens;
        lens.FieldOfView = targetFOV;
        vcamBattleClash.Lens = lens;

        // 頓幀維持一小段時間 (Hitstop 卡肉感)
        yield return new WaitForSeconds(0.05f);

        // 迅速但平滑地恢復原本的視角
        float elapsed = 0f;
        float recoverTime = 0.15f;
        while (elapsed < recoverTime)
        {
            elapsed += Time.deltaTime;
            lens.FieldOfView = Mathf.Lerp(targetFOV, _defaultClashFOV, elapsed / recoverTime);
            vcamBattleClash.Lens = lens;
            yield return null;
        }

        // 確保精準復原
        lens.FieldOfView = _defaultClashFOV;
        vcamBattleClash.Lens = lens;
    }

    private void SetPriority(CinemachineCamera vcam, int priority)
    {
        if (vcam != null) vcam.Priority = priority;
    }

    // 動態修改 Cinemachine 鏡頭切換的時間
    private void SetBlendTime(float time)
    {
        if (_brain != null)
        {
            // ★ 修正：Unity 6 (Cinemachine 3) 移除了舊版建構子，改為直接修改屬性
            var blend = _brain.DefaultBlend;
            blend.Time = time;
            _brain.DefaultBlend = blend;
        }
    }
}