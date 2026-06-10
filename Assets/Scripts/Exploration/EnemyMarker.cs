using System.Collections.Generic;
using UnityEngine;

public class EnemyMarker : MonoBehaviour
{
    public BattleUnit battleUnit;

    [Header("地圖逃跑行為 (客製化數值)")]
    public bool isCowardly = false;        // 是否是膽小會逃跑的怪物
    [Tooltip("玩家進入此距離內，敵人會開始逃離")]
    public float fleeTriggerDistance = 8f;
    [Tooltip("逃跑時的移動速度")]
    public float escapeSpeed = 5f;
    [Tooltip("此群組內的其他敵人標記，進入戰鬥時會一併拉入戰鬥場景")]
    public List<EnemyMarker> linkedEnemies = new List<EnemyMarker>();

    private Transform _playerTransform;

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) _playerTransform = playerObj.transform;
    }

    private void Update()
    {
        // 如果沒勾選會逃跑、找不到玩家，或正在戰鬥中，就不執行逃跑邏輯
        if (!isCowardly || _playerTransform == null ||
            (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.Battle))
        {
            return;
        }

        float dist = Vector3.Distance(transform.position, _playerTransform.position);

        // 當玩家距離小於「觸發逃離的距離」時，怪物會往反方向遠離
        if (dist < fleeTriggerDistance)
        {
            Vector3 escapeDir = (transform.position - _playerTransform.position).normalized;
            escapeDir.y = 0;

            transform.position += escapeDir * escapeSpeed * Time.deltaTime;
            transform.rotation = Quaternion.LookRotation(escapeDir);
        }
    }
}