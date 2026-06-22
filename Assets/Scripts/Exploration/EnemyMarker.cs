using UnityEngine;
using System.Collections.Generic;

public class EnemyMarker : MonoBehaviour
{
    public BattleUnit battleUnit;

    [Header("群組戰鬥")]
    public List<EnemyMarker> linkedEnemies = new List<EnemyMarker>();

    [Header("地圖逃跑行為 (客製化)")]
    public bool isCowardly = false;
    public float fleeTriggerDistance = 6f;
    public float escapeSpeed = 5f;

    [HideInInspector] public bool isDefeated = false; // 由 GameManager 判定是否已戰敗
    private Transform _playerTransform;
    private bool _isFleeing = false;

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) _playerTransform = playerObj.transform;
    }

    private void Update()
    {
        
    }

    public void ForceFlee()
    {
        if (isDefeated || _isFleeing) return;
        StartCoroutine(EnemyEscapeRoutine(this));
    }

    private System.Collections.IEnumerator EnemyEscapeRoutine(EnemyMarker enemy)
    {
        _isFleeing = true;
        if (enemy == null || _playerTransform == null) yield break;

        // 1. 抓取我們剛剛在 Unity 掛上去的導航元件
        var agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();

        float speed = enemy.escapeSpeed > 0 ? enemy.escapeSpeed : 8f;

        // ★ 移除了原本的 elapsed 計時器，因為我們現在要無限逃跑！

        // ==========================================
        // 主方案：智能尋路逃跑 (絕對不穿牆 + 智能滑牆)
        // ==========================================
        if (agent != null && agent.isOnNavMesh)
        {
            agent.speed = speed;
            agent.isStopped = false; // 確保導航是啟動的

            float panicTimer = 0f;
            Vector3 panicDir = Vector3.zero;

            // ★ 無限迴圈：只要敵人還活著，就會一直跑！
            while (enemy != null && !enemy.isDefeated)
            {
                // 1. 恐慌滑牆狀態
                if (panicTimer > 0)
                {
                    panicTimer -= 0.1f;
                    Vector3 targetPos = enemy.transform.position + panicDir * 3f;
                    UnityEngine.AI.NavMeshHit hit;
                    if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out hit, 4f, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        agent.SetDestination(hit.position);
                    }
                }
                else
                {
                    // 2. 正常狀態：動態計算背對玩家的絕對反方向
                    Vector3 fleeDir = (enemy.transform.position - _playerTransform.position).normalized;
                    fleeDir.y = 0;

                    // 3. 智能防卡死機制 (有路徑 + 距離還沒到 + 速度卻趨近 0 = 撞牆了)
                    if (agent.hasPath && agent.remainingDistance > 0.5f && agent.velocity.sqrMagnitude < 0.1f)
                    {
                        panicTimer = 1.5f; // 進入恐慌滑行 1.5 秒
                        // 旋轉 90 度貼牆溜走 (往左或往右)
                        float sign = Random.value > 0.5f ? 1f : -1f;
                        panicDir = Quaternion.Euler(0, 90 * sign, 0) * fleeDir;
                    }
                    else
                    {
                        // 正常持續往反方向跑
                        Vector3 targetPos = enemy.transform.position + fleeDir * 5f;
                        UnityEngine.AI.NavMeshHit hit;
                        if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out hit, 4f, UnityEngine.AI.NavMesh.AllAreas))
                        {
                            agent.SetDestination(hit.position);
                        }
                    }
                }

                // ★ 效能優化：每 0.1 秒重新掃描一次雷達就好，大幅節省效能
                yield return new WaitForSeconds(0.1f);
            }

            // 被抓到進入戰鬥，清除路徑停在原地
            if (agent != null && agent.isOnNavMesh) agent.ResetPath();
        }
        // ==========================================
        // 備用方案：無 NavMesh 的硬推模式 (一樣升級為無限追逐)
        // ==========================================
        else
        {
            float directionTimer = 0f;
            Vector3 currentFleeDir = Vector3.zero;

            // 改為無限迴圈
            while (enemy != null && !enemy.isDefeated)
            {
                directionTimer -= Time.deltaTime;
                if (directionTimer <= 0)
                {
                    Vector3 baseDir = (enemy.transform.position - _playerTransform.position).normalized;
                    baseDir.y = 0;
                    Vector3 randomTangent = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
                    currentFleeDir = (baseDir * 1.5f + randomTangent * 0.8f).normalized;
                    directionTimer = Random.Range(0.4f, 0.8f);
                }

                enemy.transform.position += currentFleeDir * speed * Time.deltaTime;
                enemy.transform.rotation = Quaternion.Slerp(enemy.transform.rotation, Quaternion.LookRotation(currentFleeDir), Time.deltaTime * 15f);

                // 因為直接改變 Transform，必須維持每幀執行才不會卡頓
                yield return null;
            }
        }

        _isFleeing = false;
    }
}