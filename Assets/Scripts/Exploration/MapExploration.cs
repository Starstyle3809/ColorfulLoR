using UnityEngine;

/// <summary>
/// 地圖探索模式：WASD 移動、角色面向、偷襲判定。
/// 掛在玩家物件上；敵人物件需有 EnemyMarker 組件。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class MapExploration : MonoBehaviour
{
    [Header("移動設定")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 720f;

    [Header("偷襲角度閾值（度）")]
    [Tooltip("玩家接近方向與敵人背面法線夾角小於此值 → 偷襲")]
    public float ambushAngleThreshold = 60f;

    private CharacterController _cc;
    private bool _canMove = true;

    private void Awake() => _cc = GetComponent<CharacterController>();

    // ── GameManager 呼叫此函式啟用/停用探索輸入 ──────────
    public void SetMovementEnabled(bool enabled) => _canMove = enabled;

    private void Update()
    {
        if (!_canMove) return;
        HandleMovement();
    }

    private void HandleMovement()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        // ★ 修正：動態適應攝影機角度
        // 取得主攝影機的正前方與右方，並消除 Y 軸（確保角色不會往天上飛）
        Vector3 camForward = Camera.main.transform.forward;
        camForward.y = 0f;
        camForward.Normalize();

        Vector3 camRight = Camera.main.transform.right;
        camRight.y = 0f;
        camRight.Normalize();

        // 根據攝影機的視角來決定實際移動的方向
        Vector3 dir = (camForward * v + camRight * h).normalized;

        if (dir.magnitude > 0.1f)
        {
            // 移動
            _cc.Move(dir * moveSpeed * Time.deltaTime);

            // 面向
            Quaternion target = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, target, rotationSpeed * Time.deltaTime);
        }
    }

    // ── 遭遇判定 ─────────────────────────────────────────
    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent<EnemyMarker>(out var enemy)) return;
        if (!_canMove) return;

        bool isAmbush = CheckAmbush(other.transform);

        // 通知 GameManager 開始戰鬥
        GameManager.Instance?.StartBattle(enemy, isAmbush);
    }

    // ── 偷襲判定邏輯 ──────────────────────────────────────
    private bool CheckAmbush(Transform enemyTransform)
    {
        // 玩家接近方向（從玩家指向敵人）
        Vector3 approachDir = (enemyTransform.position - transform.position).normalized;

        // 敵人正面方向
        Vector3 enemyForward = enemyTransform.forward;

        // 夾角：approachDir 與 enemyForward 相同 → 從正面來
        //       夾角 > 120 度 → 接近方向與正面反向 → 從背後來
        float angle = Vector3.Angle(approachDir, enemyForward);

        // 從背後接近：夾角 > (180 - threshold)
        bool fromBehind = angle > (180f - ambushAngleThreshold);
        Debug.Log($"[探索] 遭遇敵人，接近角度: {angle:F1}° → {(fromBehind ? "偷襲！" : "正面遭遇")}");
        return fromBehind;
    }
}