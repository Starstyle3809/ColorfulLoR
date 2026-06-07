using System;
using System.Collections.Generic;
using UnityEngine;

public class BattleUnit : MonoBehaviour
{
    [Header("基本資料")]
    public string unitName = "未知單位";
    public int maxHP = 100;
    public int maxStagger = 50;

    [Header("戰鬥屬性")]
    public int speedDiceCount = 1;
    public int minSpeed = 1;
    public int maxSpeed = 4;

    public int CurrentHP { get; private set; }
    public int CurrentStagger { get; private set; }
    public bool IsStaggered { get; private set; }
    public int staggerTurnsLeft = 0;

    [Header("牌組")]
    public DeckData deckData;

    [NonSerialized] public List<CardData> Hand = new();
    [NonSerialized] public List<CardData> DrawPile = new();
    [NonSerialized] public List<CardData> DiscardPile = new();
    [NonSerialized] public List<string> activeBuffs = new List<string>();

    public event Action<int, int> OnHPChanged;
    public event Action<int, int> OnStaggerChanged;
    public event Action<List<string>> OnBuffsChanged;
    public event Action OnStaggered;
    public event Action OnDied;

    public void Initialize()
    {
        CurrentHP = maxHP; CurrentStagger = maxStagger;
        IsStaggered = false; staggerTurnsLeft = 0;
        DrawPile = deckData != null ? deckData.GetShuffledDeck() : new();
        DiscardPile.Clear(); Hand.Clear(); activeBuffs.Clear();
    }

    // ★ 修正：回傳最終真實傷害 (包含混亂的雙倍計算)
    public int TakeDamage(int amount)
    {
        int finalDamage = IsStaggered ? amount * 2 : amount;
        CurrentHP = Mathf.Max(0, CurrentHP - finalDamage);
        OnHPChanged?.Invoke(CurrentHP, maxHP);
        if (CurrentHP <= 0) OnDied?.Invoke();

        if (!IsStaggered) TakeStaggerDamage(finalDamage);

        return finalDamage;
    }

    public void TakeStaggerDamage(int amount)
    {
        if (IsStaggered || amount <= 0) return;
        CurrentStagger = Mathf.Max(0, CurrentStagger - amount);
        OnStaggerChanged?.Invoke(CurrentStagger, maxStagger);

        if (CurrentStagger <= 0)
        {
            IsStaggered = true;
            staggerTurnsLeft = 2;
            OnStaggered?.Invoke();
        }
    }

    public void HealStagger(int amount)
    {
        if (IsStaggered || amount <= 0) return;
        CurrentStagger = Mathf.Min(maxStagger, CurrentStagger + amount);
        OnStaggerChanged?.Invoke(CurrentStagger, maxStagger);
    }

    public void Heal(int amount)
    {
        CurrentHP = Mathf.Min(maxHP, CurrentHP + amount);
        OnHPChanged?.Invoke(CurrentHP, maxHP);
    }

    public void ResetStaggerAtRoundStart()
    {
        if (IsStaggered)
        {
            staggerTurnsLeft--;
            if (staggerTurnsLeft <= 0)
            {
                IsStaggered = false; CurrentStagger = maxStagger;
                OnStaggerChanged?.Invoke(CurrentStagger, maxStagger);
            }
        }
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