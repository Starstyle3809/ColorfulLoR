using System.Collections.Generic;
using UnityEngine;

// ── 骰子類型 ──────────────────────────────────────────────
public enum DiceType { Melee, Ranged, Block, Evade, Heal }

// ── 單顆骰子資料 ──────────────────────────────────────────
[System.Serializable]
public class DiceData
{
    public DiceType type;
    [Tooltip("最低點數")]
    public int minValue = 1;
    [Tooltip("最高點數")]
    public int maxValue = 6;

    // 執行期擲出點數（由 BattleManager 填入）
    [System.NonSerialized] public int rolledValue;

    public int Roll()
    {
        rolledValue = Random.Range(minValue, maxValue + 1);
        return rolledValue;
    }
}

// ── 戰鬥書頁（卡牌）ScriptableObject ─────────────────────
[CreateAssetMenu(fileName = "New Card", menuName = "LibraryRuina/CardData")]
public class CardData : ScriptableObject
{
    [Header("基本資訊")]
    public string cardName = "未命名書頁";
    [TextArea] public string description;
    public Sprite artwork;

    [Header("費用")]
    [Tooltip("出牌所需光點（未來擴充用）")]
    public int cost = 1;

    [Header("骰子列表（由上到下依序結算）")]
    public List<DiceData> diceList = new();
}
