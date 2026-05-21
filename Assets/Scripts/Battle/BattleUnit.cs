using System;
using System.Collections.Generic;
using UnityEngine;

public class BattleUnit : MonoBehaviour
{
    [Header("單位資訊")]
    public string unitName = "Unknown";
    public bool isPlayer = false;

    [Header("數值")]
    public int maxHP = 100;
    public int maxStagger = 50;

    // ── 執行期狀態 ────────────────────────────────────────
    public int CurrentHP { get; private set; }
    public int CurrentStagger { get; private set; }
    public bool IsStaggered { get; private set; }
    public bool IsDead => CurrentHP <= 0;

    [Header("牌組")]
    public DeckData deckData;

    [NonSerialized] public List<CardData> Hand = new();
    [NonSerialized] public List<CardData> DrawPile = new();
    [NonSerialized] public List<CardData> DiscardPile = new(); // ★ 新增：棄牌堆
    [NonSerialized] public CardData SelectedCard;

    [NonSerialized] public List<string> activeBuffs = new List<string>();

    // ── 事件（供 UI 訂閱）────────────────────────────────
    public event Action<int, int> OnHPChanged;
    public event Action<int, int> OnStaggerChanged;
    public event Action OnStaggered;
    public event Action OnDied;
    public event Action<List<string>> OnBuffsChanged; // ★ 新增：Buff 更新廣播

    public void Initialize()
    {
        CurrentHP = maxHP;
        CurrentStagger = maxStagger;
        IsStaggered = false;
        DrawPile = deckData != null ? deckData.GetShuffledDeck() : new();
        DiscardPile.Clear(); // ★ 新增：戰鬥開始時清空棄牌堆
        Hand.Clear();
        SelectedCard = null;
        activeBuffs.Clear();
    }

    public void TakeDamage(int amount)
    {
        if (IsStaggered) amount *= 2;

        CurrentHP = Mathf.Max(0, CurrentHP - amount);
        OnHPChanged?.Invoke(CurrentHP, maxHP);
        Debug.Log($"[{unitName}] 受到 {amount} 傷害，剩餘 HP: {CurrentHP}");

        if (IsDead) OnDied?.Invoke();
    }

    public void TakeStaggerDamage(int amount)
    {
        CurrentStagger = Mathf.Max(0, CurrentStagger - amount);
        OnStaggerChanged?.Invoke(CurrentStagger, maxStagger);

        if (CurrentStagger <= 0 && !IsStaggered)
        {
            IsStaggered = true;
            OnStaggered?.Invoke();
            Debug.Log($"[{unitName}] 進入混亂狀態！");

            // 進入混亂時給予一個 Debuff 標示
            AddBuff("<color=yellow>混亂</color>");
        }
    }

    public void ResetStaggerAtRoundStart()
    {
        if (IsStaggered)
        {
            IsStaggered = false;
            CurrentStagger = maxStagger;
            OnStaggerChanged?.Invoke(CurrentStagger, maxStagger);

            // 恢復混亂時移除標示
            RemoveBuff("<color=yellow>混亂</color>");
        }
    }

    public void Heal(int amount)
    {
        CurrentHP = Mathf.Min(maxHP, CurrentHP + amount);
        OnHPChanged?.Invoke(CurrentHP, maxHP);
        Debug.Log($"[{unitName}] 回復 {amount} HP，目前 HP: {CurrentHP}");
    }

    public void ApplyAmbushPenalty()
    {
        int penalty = Mathf.RoundToInt(maxHP * 0.2f);
        CurrentHP = Mathf.Max(1, CurrentHP - penalty);
        OnHPChanged?.Invoke(CurrentHP, maxHP);
        Debug.Log($"[{unitName}] 遭到偷襲，扣除 {penalty} HP！");

        AddBuff("<color=red>被偷襲 (減血)</color>");
    }

    // ── 抽牌與棄牌邏輯 ──────────────────────────────────
    public void DrawCards(int count = 1)
    {
        for (int i = 0; i < count; i++)
        {
            // ★ 修正：加入手牌上限防呆機制 (限制最多 8 張)，滿了就不再抽
            if (Hand.Count >= 8)
            {
                Debug.Log($"[{unitName}] 手牌已達上限 (8張)，停止抽牌！");
                break;
            }

            if (DrawPile.Count == 0)
            {
                if (DiscardPile.Count == 0) break; // 如果連棄牌堆都沒牌了才停止抽牌

                DrawPile = new List<CardData>(DiscardPile);
                DiscardPile.Clear();
                ShuffleList(DrawPile);
                Debug.Log($"[{unitName}] 牌庫已空，已將棄牌堆洗勻並重新加入牌庫！");
            }

            Hand.Add(DrawPile[0]);
            DrawPile.RemoveAt(0);
        }
    }

    // ★ 新增：洗牌工具
    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int rand = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[rand]) = (list[rand], list[i]);
        }
    }

    // ★ 新增：將卡牌丟入棄牌堆
    public void Discard(CardData card)
    {
        if (card != null)
        {
            DiscardPile.Add(card);
        }
    }

    // ── ★ 新增：Buff 控制器 ───────────────────────────────
    public void AddBuff(string buffName)
    {
        activeBuffs.Add(buffName);
        OnBuffsChanged?.Invoke(activeBuffs);
    }

    public void RemoveBuff(string buffName)
    {
        if (activeBuffs.Contains(buffName))
        {
            activeBuffs.Remove(buffName);
            OnBuffsChanged?.Invoke(activeBuffs);
        }
    }
}