using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Deck", menuName = "LibraryRuina/DeckData")]
public class DeckData : ScriptableObject
{
    public const int DECK_SIZE = 10;

    [Header("牌組名稱")]
    public string deckName = "預設牌組";

    [Header("書頁列表（嚴格限制 10 張）")]
    [SerializeField] private List<CardData> cards = new();

    public IReadOnlyList<CardData> Cards => cards;

    // Editor 防呆：超過 10 張時警告
    private void OnValidate()
    {
        if (cards.Count > DECK_SIZE)
        {
            Debug.LogWarning($"[DeckData] {deckName} 超過 {DECK_SIZE} 張限制，請移除多餘書頁！");
        }
    }

    // 取得洗牌後的複本（不破壞原始資料）
    public List<CardData> GetShuffledDeck()
    {
        var copy = new List<CardData>(cards);
        for (int i = copy.Count - 1; i > 0; i--)
        {
            int rand = Random.Range(0, i + 1);
            (copy[i], copy[rand]) = (copy[rand], copy[i]);
        }
        return copy;
    }
}
