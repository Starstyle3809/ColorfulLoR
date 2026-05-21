using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 戰鬥狀態機核心。
/// 修正運鏡：拼點時瞬間推特寫鏡頭，退回時同步切回總覽。
/// 人物初始對峙位置加寬，重現廢墟圖書館的拉扯感。
/// </summary>
public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    // ── 戰鬥狀態 ─────────────────────────────────────────
    public enum BattleState
    {
        Inactive,
        Setup,
        PlayerTurn,
        EnemyTurn,
        ClashResolution,
        BattleEnd
    }
    public BattleState CurrentState { get; private set; } = BattleState.Inactive;

    // ── 單位引用 ──────────────────────────────────────────
    public BattleUnit Player { get; private set; }
    public BattleUnit Enemy { get; private set; }

    // ── 戰場空間座標 ──────────────────────────────────────
    private Vector3 _battleCenter;
    private Vector3 _playerStandPos;
    private Vector3 _enemyStandPos;

    // ── 事件廣播 ──────────────────────────────────────────
    public event Action<BattleState> OnStateChanged;
    public event Action<BattleUnit> OnUnitStaggered;
    public event Action<string> OnBattleLog;
    public event Action<int, int> OnClashResult;
    public event Action<bool> OnBattleEnded;

    [Header("設定")]
    [Tooltip("每個骰子動畫等待時間（秒）")]
    public float clashDelay = 0.8f;
    [Tooltip("人物衝刺與退回的動畫時間（秒）")]
    public float moveDuration = 0.2f;

    private struct ClashAdvance { public int p; public int e; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void StartBattle(BattleUnit player, BattleUnit enemy, bool isAmbush)
    {
        Player = player;
        Enemy = enemy;

        Player.Initialize();
        Enemy.Initialize();

        Player.OnDied += () => EndBattle(playerWins: false);
        Enemy.OnDied += () => EndBattle(playerWins: true);

        ChangeState(BattleState.Setup);
        StartCoroutine(BattleSetupRoutine(isAmbush));
    }

    // ── 進場演出：雙方退向兩側極限對峙距離 ───────────────────
    private IEnumerator BattleSetupRoutine(bool isAmbush)
    {
        _battleCenter = (Player.transform.position + Enemy.transform.position) / 2f;
        _battleCenter.y = Player.transform.position.y;

        // ★ 寬度增加：讓角色站在畫面最左右兩端，重現圖書館大舞台感
        _playerStandPos = _battleCenter + new Vector3(-8.5f, 0, 0);
        _enemyStandPos = _battleCenter + new Vector3(8.5f, 0, 0);

        Player.transform.LookAt(_enemyStandPos);
        Enemy.transform.LookAt(_playerStandPos);

        Log("雙方拉開距離，準備交鋒！");

        // ★ 修正 1：在戰鬥開局時，雙方先各抽 4 張初始手牌
        Player.DrawCards(4);
        Enemy.DrawCards(4);

        StartCoroutine(MoveUnitRoutine(Player.transform, _playerStandPos, 0.4f));
        yield return StartCoroutine(MoveUnitRoutine(Enemy.transform, _enemyStandPos, 0.4f));

        if (isAmbush)
        {
            Enemy.ApplyAmbushPenalty();
            Log($"偷襲成功！{Enemy.unitName} 損失 20% HP！");
        }

        StartCoroutine(BattleLoop());
    }

    private IEnumerator MoveUnitRoutine(Transform target, Vector3 destination, float duration)
    {
        Vector3 startPos = target.position;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = 1f - (1f - t) * (1f - t); // EaseOut 曲線，衝刺有速度感
            target.position = Vector3.Lerp(startPos, destination, t);
            yield return null;
        }
        target.position = destination;
    }

    // ── 主戰鬥迴圈 ───────────────────────────────────────
    private IEnumerator BattleLoop()
    {
        while (CurrentState != BattleState.BattleEnd)
        {
            Player.ResetStaggerAtRoundStart();
            Enemy.ResetStaggerAtRoundStart();

            // ★ 修正 2：每回合只抽 1 張牌！(原本每回合抽4張導致手牌無限繁殖)
            Player.DrawCards(1);
            Enemy.DrawCards(1);

            ChangeState(BattleState.PlayerTurn);
            Log("--- 玩家回合：請選擇書頁 ---");
            yield return new WaitUntil(() => Player.SelectedCard != null);

            ChangeState(BattleState.EnemyTurn);
            EnemySelectCard();

            ChangeState(BattleState.ClashResolution);
            yield return StartCoroutine(ResolveClash());

            // ★ 修正：結算完畢後，將打出的卡牌放入各自的棄牌堆，以供循環使用
            if (Player.SelectedCard != null) Player.Discard(Player.SelectedCard);
            if (Enemy.SelectedCard != null) Enemy.Discard(Enemy.SelectedCard);

            Player.SelectedCard = null;
            Enemy.SelectedCard = null;

            if (CurrentState == BattleState.BattleEnd) break;
            yield return new WaitForSeconds(0.3f);
        }
    }

    // ── 拼點結算 Coroutine (★ 運鏡與退避動態整合) ───────────
    private IEnumerator ResolveClash()
    {
        var playerCard = Player.SelectedCard;
        var enemyCard = Enemy.SelectedCard;

        Log($"{Player.unitName} 打出「{playerCard.cardName}」vs {Enemy.unitName} 打出「{enemyCard.cardName}」");

        int pIdx = 0, eIdx = 0;

        while (pIdx < playerCard.diceList.Count || eIdx < enemyCard.diceList.Count)
        {
            DiceData pDice = pIdx < playerCard.diceList.Count ? playerCard.diceList[pIdx] : null;
            DiceData eDice = eIdx < enemyCard.diceList.Count ? enemyCard.diceList[eIdx] : null;

            // ── 先結算回血骰（不衝刺，原地結算）─────────────────
            if (pDice?.type == DiceType.Heal)
            {
                int heal = pDice.Roll();
                Player.Heal(heal);
                Log($"{Player.unitName} 回復 {heal} HP！");
                pIdx++;
                yield return new WaitForSeconds(clashDelay);
                continue;
            }
            if (eDice?.type == DiceType.Heal)
            {
                int heal = eDice.Roll();
                Enemy.Heal(heal);
                Log($"{Enemy.unitName} 回復 {heal} HP！");
                eIdx++;
                yield return new WaitForSeconds(clashDelay);
                continue;
            }

            // ── 雙方都有骰子 (正面交鋒) ────────────────────────
            if (pDice != null && eDice != null)
            {
                // ★ 1. 衝刺前一瞬間切換特寫：讓鏡頭隨角色衝刺同步拉近
                CameraDirector.Instance?.SwitchToBattleClash(Player.transform, Enemy.transform);

                Vector3 pClashPos = _battleCenter + new Vector3(-1.2f, 0, 0);
                Vector3 eClashPos = _battleCenter + new Vector3(1.2f, 0, 0);

                // 雙方往前高速衝刺
                StartCoroutine(MoveUnitRoutine(Player.transform, pClashPos, moveDuration));
                yield return StartCoroutine(MoveUnitRoutine(Enemy.transform, eClashPos, moveDuration));

                // 2. 碰撞結算 (觸發純程式碼與 FOV 雙重打擊感震動)
                ClashAdvance adv = ResolveDicePair(pDice, eDice);
                pIdx += adv.p;
                eIdx += adv.e;

                // 3. 在碰撞點停留展示數值
                yield return new WaitForSeconds(clashDelay);

                // ★ 4. 退回前一瞬間切回總覽：讓鏡頭隨著角色退回同步拉遠 Zoom Out
                CameraDirector.Instance?.SwitchToBattleOverview();

                // 雙方退回兩端極限站位
                StartCoroutine(MoveUnitRoutine(Player.transform, _playerStandPos, moveDuration));
                yield return StartCoroutine(MoveUnitRoutine(Enemy.transform, _enemyStandPos, moveDuration));

                yield return new WaitForSeconds(moveDuration); // 等待退避完成
            }
            // ── 只有玩家骰子剩餘 (單方面追擊) ───────────────────
            else if (pDice != null)
            {
                CameraDirector.Instance?.SwitchToBattleClash(Player.transform, Enemy.transform);

                Vector3 targetPos = _enemyStandPos + new Vector3(-1.2f, 0, 0);
                yield return StartCoroutine(MoveUnitRoutine(Player.transform, targetPos, moveDuration));

                yield return StartCoroutine(ResolveSingleDice(pDice, Player, Enemy));
                pIdx++;

                yield return new WaitForSeconds(clashDelay);

                CameraDirector.Instance?.SwitchToBattleOverview();
                yield return StartCoroutine(MoveUnitRoutine(Player.transform, _playerStandPos, moveDuration));
            }
            // ── 只有敵人骰子剩餘 (單方面追擊) ───────────────────
            else
            {
                CameraDirector.Instance?.SwitchToBattleClash(Player.transform, Enemy.transform);

                Vector3 targetPos = _playerStandPos + new Vector3(1.2f, 0, 0);
                yield return StartCoroutine(MoveUnitRoutine(Enemy.transform, targetPos, moveDuration));

                yield return StartCoroutine(ResolveSingleDice(eDice, Enemy, Player));
                eIdx++;

                yield return new WaitForSeconds(clashDelay);

                CameraDirector.Instance?.SwitchToBattleOverview();
                yield return StartCoroutine(MoveUnitRoutine(Enemy.transform, _enemyStandPos, moveDuration));
            }

            if (Player.IsStaggered) OnUnitStaggered?.Invoke(Player);
            if (Enemy.IsStaggered) OnUnitStaggered?.Invoke(Enemy);
        }

        // 確保整張卡牌打完，鏡頭完全重置為總覽
        CameraDirector.Instance?.SwitchToBattleOverview();
        yield return new WaitForSeconds(0.2f);
    }

    private ClashAdvance ResolveDicePair(DiceData pDice, DiceData eDice)
    {
        int pVal = pDice.Roll();
        int eVal = eDice.Roll();

        Log($"🎲 {Player.unitName}[{pDice.type}:{pVal}] vs {Enemy.unitName}[{eDice.type}:{eVal}]");
        CameraDirector.Instance?.TriggerImpulse(0.3f); // 觸發震動
        OnClashResult?.Invoke(pVal, eVal);

        bool pIsAtk = pDice.type is DiceType.Melee or DiceType.Ranged;
        bool eIsAtk = eDice.type is DiceType.Melee or DiceType.Ranged;

        if (pIsAtk && eIsAtk)
        {
            if (pVal >= eVal)
            {
                Enemy.TakeDamage(pVal);
                Enemy.TakeStaggerDamage(pVal - eVal);
                Log($"▶ {Player.unitName} 勝！造成 {pVal} 傷害");
            }
            else
            {
                Player.TakeDamage(eVal);
                Player.TakeStaggerDamage(eVal - pVal);
                Log($"▶ {Enemy.unitName} 勝！造成 {eVal} 傷害");
            }
            return new ClashAdvance { p = 1, e = 1 };
        }

        if (pIsAtk && eDice.type == DiceType.Block)
        {
            if (eVal >= pVal)
            {
                Player.TakeStaggerDamage(eVal - pVal);
                Log($"▶ {Enemy.unitName} 格擋成功！對玩家造成 {eVal - pVal} 混亂傷害");
            }
            else
            {
                Enemy.TakeDamage(pVal - eVal);
                Log($"▶ {Player.unitName} 破防！造成 {pVal - eVal} 傷害");
            }
            return new ClashAdvance { p = 1, e = 1 };
        }

        if (pDice.type == DiceType.Block && eIsAtk)
        {
            if (pVal >= eVal)
            {
                Enemy.TakeStaggerDamage(pVal - eVal);
                Log($"▶ {Player.unitName} 格擋成功！對敵人造成 {pVal - eVal} 混亂傷害");
            }
            else
            {
                Player.TakeDamage(eVal - pVal);
                Log($"▶ {Enemy.unitName} 破防！造成 {eVal - pVal} 傷害");
            }
            return new ClashAdvance { p = 1, e = 1 };
        }

        if (pDice.type == DiceType.Evade && eIsAtk)
        {
            if (pVal >= eVal)
            {
                Log($"▶ {Player.unitName} 完美閃避！閃避骰保留對陣下一顆");
                return new ClashAdvance { p = 0, e = 1 };
            }
            else
            {
                Player.TakeDamage(eVal);
                Log($"▶ 閃避失敗！{Player.unitName} 受到 {eVal} 傷害");
                return new ClashAdvance { p = 1, e = 1 };
            }
        }

        if (pIsAtk && eDice.type == DiceType.Evade)
        {
            if (eVal >= pVal)
            {
                Log($"▶ {Enemy.unitName} 完美閃避！");
                return new ClashAdvance { p = 1, e = 0 };
            }
            else
            {
                Enemy.TakeDamage(pVal);
                Log($"▶ 閃避失敗！{Enemy.unitName} 受到 {pVal} 傷害");
                return new ClashAdvance { p = 1, e = 1 };
            }
        }

        return new ClashAdvance { p = 1, e = 1 };
    }

    private IEnumerator ResolveSingleDice(DiceData dice, BattleUnit attacker, BattleUnit defender)
    {
        int val = dice.Roll();
        Log($"🎲 {attacker.unitName}[{dice.type}:{val}] 無對手骰，直接命中");
        CameraDirector.Instance?.TriggerImpulse(0.5f);

        if (dice.type is DiceType.Melee or DiceType.Ranged)
            defender.TakeDamage(val);

        yield return null;
    }

    private void EnemySelectCard()
    {
        if (Enemy.Hand.Count == 0) return;
        int idx = UnityEngine.Random.Range(0, Enemy.Hand.Count);
        Enemy.SelectedCard = Enemy.Hand[idx];
        Enemy.Hand.RemoveAt(idx);
        Log($"{Enemy.unitName} 選擇了「{Enemy.SelectedCard.cardName}」");
    }

    public void PlayerPlayCard(CardData card)
    {
        if (CurrentState != BattleState.PlayerTurn) return;
        if (!Player.Hand.Contains(card)) return;
        Player.Hand.Remove(card);
        Player.SelectedCard = card;
        Log($"{Player.unitName} 選擇了「{card.cardName}」");
    }

    private void EndBattle(bool playerWins)
    {
        if (CurrentState == BattleState.BattleEnd) return;
        ChangeState(BattleState.BattleEnd);
        Log(playerWins ? "=== 玩家勝利！===" : "=== 玩家敗北... ===");
        OnBattleEnded?.Invoke(playerWins);
        GameManager.Instance?.OnBattleFinished(playerWins);
    }

    private void ChangeState(BattleState newState)
    {
        CurrentState = newState;
        OnStateChanged?.Invoke(newState);
    }

    private void Log(string msg)
    {
        Debug.Log($"[Battle] {msg}");
        OnBattleLog?.Invoke(msg);
    }
}