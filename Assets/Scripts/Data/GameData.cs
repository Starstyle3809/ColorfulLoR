using UnityEngine;
using System.Collections.Generic;

// 擴充為 5 種骰子類型
public enum DiceType
{
    MeleeAttack,  // 近戰攻擊
    RangedAttack, // 遠程攻擊
    Block,        // 防禦
    Evade,        // 閃避
    Heal          // 回血
}

[System.Serializable]
public class DiceData
{
    public DiceType type;
    public int minVal;
    public int maxVal;

    // ★ 補回遺失的 Roll() 擲骰功能
    public int Roll()
    {
        return Random.Range(minVal, maxVal + 1);
    }

    public string GetTypeName()
    {
        switch (type)
        {
            case DiceType.MeleeAttack: return "近戰";
            case DiceType.RangedAttack: return "遠程";
            case DiceType.Block: return "防禦";
            case DiceType.Evade: return "閃避";
            case DiceType.Heal: return "回血";
            default: return "未知";
        }
    }
}

// 戰鬥書頁 (單張卡牌)
[CreateAssetMenu(fileName = "NewCard", menuName = "Ruina/Card (戰鬥書頁)")]
public class CardData : ScriptableObject
{
    public string cardName = "新卡牌";

    // ★ 補回遺失的卡圖與敘述
    public Sprite artwork;
    [TextArea(3, 5)]
    public string description;

    public int cost = 1;
    public List<DiceData> diceList = new List<DiceData>();
}

// 牌組 (固定 10 張)
[CreateAssetMenu(fileName = "NewDeck", menuName = "Ruina/Deck (牌組)")]
public class DeckData : ScriptableObject
{
    public string deckName = "預設牌組";
    [Tooltip("請放入剛好 10 張卡牌")]
    public List<CardData> cards = new List<CardData>(10);

    public bool IsValid()
    {
        return cards.Count == 10;
    }

    // ★ 確保洗牌功能也在裡面
    public List<CardData> GetShuffledDeck()
    {
        List<CardData> shuffled = new List<CardData>(cards);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int rand = Random.Range(0, i + 1);
            CardData temp = shuffled[i];
            shuffled[i] = shuffled[rand];
            shuffled[rand] = temp;
        }
        return shuffled;
    }
}