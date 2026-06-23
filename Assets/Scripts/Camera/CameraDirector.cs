using UnityEngine;
using Unity.Cinemachine;
using System.Collections;

public class CameraDirector : MonoBehaviour
{
    public static CameraDirector Instance { get; private set; }
    private float _defaultOrthoSize;

    [Header("三台虛擬攝影機")]
    public CinemachineCamera vcamExplore;
    public CinemachineCamera vcamBattleOverview;
    public CinemachineCamera vcamBattleClash;

    // ★ 移除了 clashTargetGroup，改用腳本直接記錄雙方位置
    private Transform _clashTarget1;
    private Transform _clashTarget2;

    [Header("螢幕震動")]
    public CinemachineImpulseSource impulseSource;

    private const int PRIORITY_ACTIVE = 20;
    private const int PRIORITY_INACTIVE = 10;

    private CinemachineBrain _brain;
    private float _defaultClashFOV;
    private bool _isPunching = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (Camera.main != null) _brain = Camera.main.GetComponent<CinemachineBrain>();

        if (vcamBattleClash != null)
        {
            _defaultClashFOV = vcamBattleClash.Lens.FieldOfView;
            _defaultOrthoSize = vcamBattleClash.Lens.OrthographicSize;
        }

        if (impulseSource == null) impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    private void Update()
    {
        // ★ 核心改動：手動精準控制拚點鏡頭，絕不改變原有的 Y、Z 與角度
        if (vcamBattleClash.Priority == PRIORITY_ACTIVE && !_isPunching)
        {
            if (_clashTarget1 != null && _clashTarget2 != null)
            {
                // 1. 拷貝主戰鬥鏡頭的絕對高度(Y)、深度(Z)與視角(Rotation)
                Vector3 overviewPos = vcamBattleOverview.transform.position;
                Quaternion overviewRot = vcamBattleOverview.transform.rotation;

                // 2. 計算兩人的中心點 X
                float midX = (_clashTarget1.position.x + _clashTarget2.position.x) / 2f;

                // 3. 強制賦予拚點鏡頭位置
                vcamBattleClash.transform.position = new Vector3(midX, overviewPos.y, overviewPos.z);
                vcamBattleClash.transform.rotation = overviewRot;

                // 4. 動態縮放畫面大小 (Orthographic Size)
                float dist = Vector3.Distance(_clashTarget1.position, _clashTarget2.position);
                float targetSize = Mathf.Clamp(3.5f + (dist * 0.3f), 3.5f, 7.5f); // 縮放極限設定

                _defaultOrthoSize = Mathf.Lerp(_defaultOrthoSize, targetSize, Time.deltaTime * 5f);
                var lens = vcamBattleClash.Lens;
                lens.OrthographicSize = _defaultOrthoSize;
                vcamBattleClash.Lens = lens;
            }
        }
    }

    public void SwitchToExplore()
    {
        SetBlendTime(1.0f);
        SetPriority(vcamExplore, PRIORITY_ACTIVE);
        SetPriority(vcamBattleOverview, PRIORITY_INACTIVE);
        SetPriority(vcamBattleClash, PRIORITY_INACTIVE);
    }

    public void SwitchToBattleOverview()
    {
        SetBlendTime(0.3f);
        SetPriority(vcamExplore, PRIORITY_INACTIVE);
        SetPriority(vcamBattleOverview, PRIORITY_ACTIVE);
        SetPriority(vcamBattleClash, PRIORITY_INACTIVE);
    }

    public void SwitchToBattleClash(Transform target1, Transform target2)
    {
        // 紀錄兩名交鋒目標
        _clashTarget1 = target1;
        _clashTarget2 = target2;

        SetBlendTime(0.2f);
        SetPriority(vcamExplore, PRIORITY_INACTIVE);
        SetPriority(vcamBattleOverview, PRIORITY_INACTIVE);
        SetPriority(vcamBattleClash, PRIORITY_ACTIVE);
    }

    public void TriggerImpulse(float force = 1f)
    {
        if (impulseSource != null) impulseSource.GenerateImpulse(force);
        if (gameObject.activeInHierarchy && vcamBattleClash != null) StartCoroutine(FOVPunchRoutine(force));
    }

    private IEnumerator FOVPunchRoutine(float force)
    {
        _isPunching = true;
        var lens = vcamBattleClash.Lens;
        bool isOrtho = lens.Orthographic;

        float targetFOV = _defaultClashFOV - (8f * force);
        float targetOrtho = _defaultOrthoSize - (0.8f * force);

        if (isOrtho) lens.OrthographicSize = targetOrtho;
        else lens.FieldOfView = targetFOV;
        vcamBattleClash.Lens = lens;

        yield return new WaitForSeconds(0.05f);

        float elapsed = 0f;
        float recoverTime = 0.15f;
        while (elapsed < recoverTime)
        {
            elapsed += Time.deltaTime;
            if (isOrtho) lens.OrthographicSize = Mathf.Lerp(targetOrtho, _defaultOrthoSize, elapsed / recoverTime);
            else lens.FieldOfView = Mathf.Lerp(targetFOV, _defaultClashFOV, elapsed / recoverTime);

            vcamBattleClash.Lens = lens;
            yield return null;
        }

        if (isOrtho) lens.OrthographicSize = _defaultOrthoSize;
        else lens.FieldOfView = _defaultClashFOV;
        vcamBattleClash.Lens = lens;
        _isPunching = false;
    }

    private void SetPriority(CinemachineCamera vcam, int priority) { if (vcam != null) vcam.Priority = priority; }
    private void SetBlendTime(float time)
    {
        if (_brain != null)
        {
            var blend = _brain.DefaultBlend;
            blend.Time = time;
            _brain.DefaultBlend = blend;
        }
    }
}