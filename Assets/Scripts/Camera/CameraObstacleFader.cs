using System.Collections.Generic;
using UnityEngine;

public class CameraObstacleFader : MonoBehaviour
{
    [Header("要透視的圖層 (例如 Environment)")]
    public LayerMask obstacleLayer;

    private HashSet<MeshRenderer> _hiddenObjects = new HashSet<MeshRenderer>();
    private HashSet<MeshRenderer> _currentlyHitObjects = new HashSet<MeshRenderer>();

    private void Update()
    {
        // 確保戰鬥系統存在
        if (BattleManager.Instance == null) return;

        _currentlyHitObjects.Clear();

        // 1. 蒐集場上所有目標 (玩家 + 存活的敵人)
        List<Transform> targets = new List<Transform>();
        if (BattleManager.Instance.Player != null) targets.Add(BattleManager.Instance.Player.transform);
        foreach (var enemy in BattleManager.Instance.Enemies)
        {
            if (enemy != null && enemy.CurrentHP > 0) targets.Add(enemy.transform);
        }

        // 2. 對每一個目標發射透視射線
        foreach (var target in targets)
        {
            Vector3 dirToTarget = target.position - transform.position;
            float dist = Vector3.Distance(transform.position, target.position);
            RaycastHit[] hits = Physics.RaycastAll(transform.position, dirToTarget, dist, obstacleLayer);

            foreach (var hit in hits)
            {
                MeshRenderer mr = hit.collider.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    _currentlyHitObjects.Add(mr);
                    mr.enabled = false; // 隱藏擋住視線的物件
                }
            }
        }

        // 3. 把已經不在任何射線內的物件恢復顯示
        HashSet<MeshRenderer> toRestore = new HashSet<MeshRenderer>();
        foreach (var mr in _hiddenObjects)
        {
            if (!_currentlyHitObjects.Contains(mr))
            {
                if (mr != null) mr.enabled = true;
                toRestore.Add(mr);
            }
        }

        foreach (var mr in toRestore) _hiddenObjects.Remove(mr);
        foreach (var mr in _currentlyHitObjects) _hiddenObjects.Add(mr);
    }
}