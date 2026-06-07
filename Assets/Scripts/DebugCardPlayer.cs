using UnityEngine;

public class DebugCardPlayer : MonoBehaviour
{
    void Update()
    {
        // 如果目前是配置階段，且按下空白鍵
        if (Input.GetKeyDown(KeyCode.Space))
        {
            var bm = BattleManager.Instance;
            if (bm != null && bm.CurrentState == BattleManager.BattleState.AssignPhase)
            {
                // 改為直接觸發「戰鬥開始」按鈕的邏輯
                bm.OnStartClashButtonClicked();
            }
        }
    }
}