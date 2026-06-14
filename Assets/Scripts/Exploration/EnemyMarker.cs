using UnityEngine;
using System.Collections.Generic;

public class EnemyMarker : MonoBehaviour
{
    public BattleUnit battleUnit;

    [Header("群組戰鬥")]
    [Tooltip("此群組內的其他敵人標記，進入戰鬥時會一併拉入戰鬥場景")]
    public List<EnemyMarker> linkedEnemies = new List<EnemyMarker>();

    [Header("地圖逃跑行為 (客製化)")]
    public bool isCowardly = false;        // 勾選後，怪物才會逃跑
    public float fleeTriggerDistance = 8f; // 玩家進入多近的距離內開始逃
    public float escapeSpeed = 5f;         // 逃跑移動速度

    private Transform _playerTransform;
    private Vector3 _currentFleeDir;
    private float _directionChangeTimer;
    private float _gracePeriodTimer = 2f;  // ★ 戰鬥結束後的緩衝時間，避免一出來就跑

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) _playerTransform = playerObj.transform;
    }

    private void Update()
    {
        // 嚴格限制：如果沒勾選、找不到玩家、或者「不是在探索模式」，絕對不執行逃跑
        if (!isCowardly || _playerTransform == null || GameManager.Instance == null ||
            GameManager.Instance.CurrentState != GameManager.GameState.Explore)
        {
            _gracePeriodTimer = 2f; // 只要不在探索狀態就重置冷卻
            return;
        }

        // 戰後冷卻時間
        if (_gracePeriodTimer > 0)
        {
            _gracePeriodTimer -= Time.deltaTime;
            return;
        }

        float dist = Vector3.Distance(transform.position, _playerTransform.position);

        // 玩家靠近時觸發逃跑
        if (dist < fleeTriggerDistance && dist > 0.1f)
        {
            _directionChangeTimer -= Time.deltaTime;

            if (_directionChangeTimer <= 0)
            {
                // 反向向量
                Vector3 awayDir = (transform.position - _playerTransform.position).normalized;

                // ★ 隨機亂竄向量：混入一個側向或隨機角度，讓敵人像無頭蒼蠅
                Vector3 randomTangent = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;

                _currentFleeDir = (awayDir * 1.5f + randomTangent).normalized;
                _currentFleeDir.y = 0; // 鎖定 Y 軸

                _directionChangeTimer = Random.Range(0.4f, 1.2f);
            }

            transform.position += _currentFleeDir * escapeSpeed * Time.deltaTime;

            if (_currentFleeDir != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(_currentFleeDir), Time.deltaTime * 8f);
            }
        }
    }
}