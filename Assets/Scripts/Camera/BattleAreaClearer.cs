using System.Collections.Generic;
using UnityEngine;

public class BattleAreaClearer : MonoBehaviour
{
    [Header("淨空區設定")]
    public LayerMask obstacleLayer;
    public Vector3 boxSize = new Vector3(25f, 10f, 10f); // 你可以在面板上自由調整這個大小

    private List<MeshRenderer> _hiddenObjects = new List<MeshRenderer>();

    private void Start()
    {
        // 訂閱戰鬥系統的狀態改變
        if (BattleManager.Instance != null)
            BattleManager.Instance.OnStateChanged += OnBattleStateChanged;
    }

    private void OnBattleStateChanged(BattleManager.BattleState state)
    {
        // 戰鬥開始時：淨空
        if (state == BattleManager.BattleState.Setup) ClearArea();
        // 戰鬥結束時：恢復
        else if (state == BattleManager.BattleState.BattleEnd) RestoreArea();
    }

    private void ClearArea()
    {
        RestoreArea(); // 防呆：確保不會重複隱藏

        // 抓出框框內的所有障礙物
        Collider[] hits = Physics.OverlapBox(transform.position, boxSize / 2f, transform.rotation, obstacleLayer);

        foreach (var hit in hits)
        {
            MeshRenderer mr = hit.GetComponent<MeshRenderer>();
            if (mr != null && mr.enabled)
            {
                mr.enabled = false;
                _hiddenObjects.Add(mr);
            }
        }
    }

    private void RestoreArea()
    {
        foreach (var mr in _hiddenObjects)
        {
            if (mr != null) mr.enabled = true;
        }
        _hiddenObjects.Clear();
    }

    // ★ 神奇魔法：在編輯器畫出半透明紅框，讓你直接「用眼睛」客製化範圍！
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.4f); // 半透明紅色
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.DrawCube(Vector3.zero, boxSize);

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(Vector3.zero, boxSize);
    }

    private void OnDestroy()
    {
        if (BattleManager.Instance != null)
            BattleManager.Instance.OnStateChanged -= OnBattleStateChanged;
    }
}