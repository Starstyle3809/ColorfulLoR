using UnityEngine;

/// <summary>
/// 掛在場景中的敵人物件上，作為遭遇觸發的識別標記。
/// 需搭配 Collider（Is Trigger = true）。
/// </summary>
public class EnemyMarker : MonoBehaviour
{
    [Tooltip("對應的 BattleUnit 資料（可放在同物件或子物件）")]
    public BattleUnit battleUnit;

    private void Reset()
    {
        // 自動嘗試抓取同物件的 BattleUnit
        battleUnit = GetComponent<BattleUnit>();
    }
}
