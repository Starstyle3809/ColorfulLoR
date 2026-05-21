using UnityEngine;

public class DebugCardPlayer : MonoBehaviour
{
    void Update()
    {
        // 如果目前是玩家回合，且按下空白鍵
        if (Input.GetKeyDown(KeyCode.Space))
        {
            var bm = BattleManager.Instance;
            if (bm != null && bm.CurrentState == BattleManager.BattleState.PlayerTurn)
            {
                // 自動打出玩家手牌的第一張卡
                if (bm.Player.Hand.Count > 0)
                {
                    bm.PlayerPlayCard(bm.Player.Hand[0]);
                }
            }
        }
    }
}