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
    private float _velocityY;
    private float _gravity = -9.81f;

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

        Vector3 camForward = Camera.main.transform.forward;
        camForward.y = 0f;

        if (camForward.sqrMagnitude < 0.001f)
        {
            camForward = Camera.main.transform.up;
            camForward.y = 0f;
        }
        camForward.Normalize();

        Vector3 camRight = Camera.main.transform.right;
        camRight.y = 0f;
        camRight.Normalize();

        Vector3 dir = (camForward * v + camRight * h).normalized;

        // ==========================================
        // ★ 核心修復：加入重力與下樓梯的拉力
        // ==========================================
        if (_cc.isGrounded)
        {
            // 當踩在地上或台階上時，給予一個輕微向下的力量
            // 這能確保玩家在「下樓梯」時，會被死死吸住地板跟著往下走
            _velocityY = -2f;
        }
        else
        {
            // 如果真的懸空了，就執行自由落體
            _velocityY += _gravity * Time.deltaTime;
        }

        // 將你的水平移動與垂直重力結合
        Vector3 moveVelocity = dir * moveSpeed;
        moveVelocity.y = _velocityY;

        // 使用帶有重力的速度來移動
        _cc.Move(moveVelocity * Time.deltaTime);

        // 轉向邏輯維持不變
        if (dir.magnitude > 0.1f)
        {
            Quaternion target = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, rotationSpeed * Time.deltaTime);
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