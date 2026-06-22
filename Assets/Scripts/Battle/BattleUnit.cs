using System;
using System.Collections.Generic;
using UnityEngine;

public class BattleUnit : MonoBehaviour
{
    [Header("基本資料")]
    public string unitName = "未知單位";
    public int maxHP = 100;
    public int currentShield = 0;
    public bool isKeyTarget = false;

    [Header("戰鬥屬性")]
    public int speedDiceCount = 1;
    public int minSpeed = 1;
    public int maxSpeed = 4;

    public int CurrentHP { get; private set; }

    [Header("牌組")]
    public DeckData deckData;

    [NonSerialized] public List<CardData> Hand = new();
    [NonSerialized] public List<CardData> DrawPile = new();
    [NonSerialized] public List<CardData> DiscardPile = new();
    [NonSerialized] public List<string> activeBuffs = new List<string>();

    public event Action<int, int> OnHPChanged;
    public event Action<List<string>> OnBuffsChanged;
    public event Action OnDied;

    public void Initialize()
    {
        CurrentHP = maxHP; 
        DrawPile = deckData != null ? deckData.GetShuffledDeck() : new();
        DiscardPile.Clear(); Hand.Clear(); activeBuffs.Clear();
    }

    // ★ 修正：回傳最終真實傷害 (包含混亂的雙倍計算)
    public int TakeDamage(int damage)
    {
        if (damage <= 0) return 0;

        // 優先扣除護盾
        if (currentShield > 0)
        {
            if (currentShield >= damage) { currentShield -= damage; return 0; }
            else { damage -= currentShield; currentShield = 0; }
        }

        CurrentHP -= damage;
        if (CurrentHP < 0) CurrentHP = 0;

        // ★ 更新 5 格血條
        GetComponentInChildren<CustomHPBar>()?.UpdateHP(CurrentHP);

        return damage;
    }


    public void GainShield(int amount)
    {
        currentShield += amount;
    }


    public void Heal(int amount)
    {
        CurrentHP = Mathf.Min(maxHP, CurrentHP + amount);
        OnHPChanged?.Invoke(CurrentHP, maxHP);
    }

    

    public void ApplyAmbushPenalty()
    {
        int penalty = Mathf.RoundToInt(maxHP * 0.2f);
        CurrentHP = Mathf.Max(1, CurrentHP - penalty);
        OnHPChanged?.Invoke(CurrentHP, maxHP);
    }

    public void DrawCards(int count = 1)
    {
        for (int i = 0; i < count; i++)
        {
            if (Hand.Count >= 8) break;
            if (DrawPile.Count == 0) { if (DiscardPile.Count == 0) break; DrawPile = new List<CardData>(DiscardPile); DiscardPile.Clear(); ShuffleList(DrawPile); }
            Hand.Add(DrawPile[0]); DrawPile.RemoveAt(0);
        }
    }

    private void ShuffleList<T>(List<T> list) { for (int i = list.Count - 1; i > 0; i--) { int rand = UnityEngine.Random.Range(0, i + 1); (list[i], list[rand]) = (list[rand], list[i]); } }
    public void Discard(CardData card) { if (card != null) DiscardPile.Add(card); }
    public void AddBuff(string buffName) { if (!activeBuffs.Contains(buffName)) { activeBuffs.Add(buffName); OnBuffsChanged?.Invoke(activeBuffs); } }
}