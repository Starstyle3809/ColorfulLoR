using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    public enum BattleState { Setup, AssignPhase, ClashResolution, BattleEnd }
    public BattleState CurrentState { get; private set; }

    [Header("戰鬥單位")]
    public BattleUnit Player;
    public List<BattleUnit> Enemies = new List<BattleUnit>();
    public int currentRound = 1;

    [Header("打擊感參數")]
    public float clashFovShake = 4f;
    public float preClashWaitTime = 0.8f;
    public float normalHitStopTime = 0.05f;

    public event System.Action<BattleState> OnStateChanged;
    public event System.Action<string> OnBattleLog;
    public event System.Action<bool> OnBattleEnded;
    public event System.Action OnRoundStart;

    private List<SpeedDiceSlot> _allActiveSlots = new List<SpeedDiceSlot>();
    private BattleUIController _ui;
    private bool _isRangedClash = false;

    private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }
    private void Start() { _ui = FindAnyObjectByType<BattleUIController>(); }
    private void Log(string msg) => OnBattleLog?.Invoke(msg);

    public void StartBattle(BattleUnit player, List<BattleUnit> enemies, bool isAmbush)
    {
        if (_ui != null) _ui.ShowCombatUI();
        Player = player; Enemies = enemies; currentRound = 1;
        Player.Initialize(); foreach (var e in Enemies) e.Initialize();
        ChangeState(BattleState.Setup); StartCoroutine(BattleSetupRoutine());
    }

    private void ChangeState(BattleState newState) { CurrentState = newState; OnStateChanged?.Invoke(CurrentState); }

    private IEnumerator BattleSetupRoutine()
    {
        Log("<color=#AAAAAA>[系統] 準備交鋒。</color>");
        Vector3 center = Vector3.zero;
        StartCoroutine(MoveUnitRoutine(Player.transform, center + new Vector3(-8f, 0, 0), 0.5f));
        for (int i = 0; i < Enemies.Count; i++) StartCoroutine(MoveUnitRoutine(Enemies[i].transform, center + new Vector3(8f + (i * 2f), 0, (i * -1f)), 0.5f));
        yield return new WaitForSeconds(0.6f);

        if (Enemies.Count > 0) { Player.transform.LookAt(Enemies[0].transform); foreach (var e in Enemies) e.transform.LookAt(Player.transform); }
        Player.DrawCards(4); foreach (var e in Enemies) e.DrawCards(4);
        yield return new WaitForSeconds(1f); StartRound();
    }

    private void StartRound()
    {
        CurrentState = BattleState.Setup;
        int totalEnemySlots = 0; foreach (var e in Enemies) totalEnemySlots += e.speedDiceCount;
        int playerSlotCount = Mathf.Max(Player.speedDiceCount, totalEnemySlots);

        if (Player != null && Player.CurrentHP > 0) Player.DrawCards(playerSlotCount);
        foreach (var enemy in Enemies) { if (enemy != null && enemy.CurrentHP > 0) enemy.DrawCards(enemy.speedDiceCount); }

        if (_ui != null)
        {
            _ui.ClearAllSlots(); _allActiveSlots.Clear();
            for (int i = 0; i < playerSlotCount; i++) _allActiveSlots.Add(_ui.CreateSlot(Player, Random.Range(Player.minSpeed, Player.maxSpeed + 1), true));
            var pSlots = _allActiveSlots.Where(s => s.IsPlayerSlot).ToList();

            foreach (var enemy in Enemies)
            {
                for (int i = 0; i < enemy.speedDiceCount; i++)
                {
                    SpeedDiceSlot slot = _ui.CreateSlot(enemy, Random.Range(enemy.minSpeed, enemy.maxSpeed + 1), false);
                    _allActiveSlots.Add(slot);
                    if (enemy.Hand.Count > 0) slot.SetAction(enemy.Hand[Random.Range(0, enemy.Hand.Count)], pSlots.Count > 0 ? pSlots[Random.Range(0, pSlots.Count)] : null, null);
                }
            }
        }
        OnRoundStart?.Invoke(); ChangeState(BattleState.AssignPhase);
    }

    public void OnStartClashButtonClicked()
    {
        if (CurrentState != BattleState.AssignPhase) return;
        StartCoroutine(ExecuteAllSlotsRoutine());
    }

    private IEnumerator ExecuteAllSlotsRoutine()
    {
        ChangeState(BattleState.ClashResolution);
        var sortedSlots = _allActiveSlots.Where(s => s.AssignedCard != null).OrderByDescending(s => s.Speed).ToList();

        foreach (var slot in sortedSlots)
        {
            if (slot.IsResolved || slot.Owner.CurrentHP <= 0) continue;
            SpeedDiceSlot targetSlot = slot.TargetSlot; BattleUnit targetUnit = slot.TargetUnit;

            if (targetSlot != null && !targetSlot.IsResolved && targetSlot.Owner.CurrentHP > 0)
            {
                yield return StartCoroutine(ResolveClash(slot.Owner, slot.AssignedCard, targetSlot.Owner, targetSlot.AssignedCard));
                slot.Owner.Discard(slot.AssignedCard); slot.Owner.Hand.Remove(slot.AssignedCard); slot.IsResolved = true;
                if (targetSlot.AssignedCard != null) { targetSlot.Owner.Discard(targetSlot.AssignedCard); targetSlot.Owner.Hand.Remove(targetSlot.AssignedCard); }
                targetSlot.IsResolved = true;
            }
            else
            {
                BattleUnit originalTarget = targetUnit;
                if (originalTarget == null && targetSlot != null) originalTarget = targetSlot.Owner;
                if (originalTarget != null && originalTarget.CurrentHP > 0) yield return StartCoroutine(ResolveClash(slot.Owner, slot.AssignedCard, originalTarget, null));

                slot.Owner.Discard(slot.AssignedCard); slot.Owner.Hand.Remove(slot.AssignedCard); slot.IsResolved = true;
            }
        }

        CameraDirector.Instance?.SwitchToBattleOverview();
        if (!CheckBattleEnd()) { currentRound++; StartRound(); }
    }

    private IEnumerator ResolveClash(BattleUnit unitA, CardData cardA, BattleUnit unitB, CardData cardB)
    {
        Log($"<b>[交鋒] {unitA.unitName} vs {unitB.unitName}</b>");
        CameraDirector.Instance?.SwitchToBattleClash(unitA.transform, unitB.transform);

        unitA.currentShield = 0; unitB.currentShield = 0;

        Vector3 startPosA = unitA.transform.position; Vector3 startPosB = unitB.transform.position;
        Vector3 currentPosA = startPosA; Vector3 currentPosB = startPosB;
        int idxA = 0, idxB = 0;
        List<DiceData> diceA = cardA != null ? cardA.diceList : new List<DiceData>();
        List<DiceData> diceB = cardB != null ? cardB.diceList : new List<DiceData>();

        while (idxA < diceA.Count || idxB < diceB.Count)
        {
            if (unitA.CurrentHP <= 0 || unitB.CurrentHP <= 0) break;

            DiceData dA = idxA < diceA.Count ? diceA[idxA] : null; DiceData dB = idxB < diceB.Count ? diceB[idxB] : null;

            bool aIsAtk = dA != null && (dA.type == DiceType.MeleeAttack || dA.type == DiceType.RangedAttack);
            bool bIsAtk = dB != null && (dB.type == DiceType.MeleeAttack || dB.type == DiceType.RangedAttack);
            bool aIsBlk = dA != null && dA.type == DiceType.Block; bool bIsBlk = dB != null && dB.type == DiceType.Block;
            bool aIsEvd = dA != null && dA.type == DiceType.Evade; bool bIsEvd = dB != null && dB.type == DiceType.Evade;
            bool aIsHeal = dA != null && dA.type == DiceType.Heal; bool bIsHeal = dB != null && dB.type == DiceType.Heal;
            bool aIsMelee = dA != null && dA.type == DiceType.MeleeAttack; bool bIsMelee = dB != null && dB.type == DiceType.MeleeAttack;

            Vector3 currentDir = (currentPosB - currentPosA).normalized; if (currentDir == Vector3.zero) currentDir = Vector3.right; currentDir.y = 0;
            float dist = Vector3.Distance(currentPosA, currentPosB);
            Vector3 dashTargetA = currentPosA; Vector3 dashTargetB = currentPosB;

            if (dist > 3f)
            {
                if (aIsMelee && bIsMelee) { Vector3 center = (currentPosA + currentPosB) / 2f; dashTargetA = center - currentDir * 1.2f; dashTargetB = center + currentDir * 1.2f; }
                else if (aIsMelee) dashTargetA = currentPosB - currentDir * 2f;
                else if (bIsMelee) dashTargetB = currentPosA + currentDir * 2f;
            }

            if (dashTargetA != currentPosA) StartCoroutine(MoveUnitRoutine(unitA.transform, dashTargetA, 0.15f));
            if (dashTargetB != currentPosB) StartCoroutine(MoveUnitRoutine(unitB.transform, dashTargetB, 0.15f));
            if (dashTargetA != currentPosA || dashTargetB != currentPosB) yield return new WaitForSeconds(0.2f);

            // ==========================================
            // ★ 補回前搖文字
            // ==========================================
            if (dA != null && dB != null) { Log($"<color=#00FFFF>【拼點準備】 {unitA.unitName}({dA.GetTypeName()}) vs {unitB.unitName}({dB.GetTypeName()})</color>"); yield return new WaitForSeconds(preClashWaitTime); }
            else if (dA != null) { Log($"<color=#FFA500>【單方準備】 {unitA.unitName}({dA.GetTypeName()}) 發動！</color>"); yield return new WaitForSeconds(preClashWaitTime * 0.8f); }
            else if (dB != null) { Log($"<color=#FFA500>【單方準備】 {unitB.unitName}({dB.GetTypeName()}) 發動！</color>"); yield return new WaitForSeconds(preClashWaitTime * 0.8f); }

            int valA = dA != null ? dA.Roll() : 0; int valB = dB != null ? dB.Roll() : 0;
            int finalDmgA = 0; int finalDmgB = 0;

            // ==========================================
            // ★ 動畫連動區塊 + 補回所有 Log 文字
            // ==========================================
            // ==========================================
            // ★ 動畫連動區塊 + 補回所有 Log 文字 (已修復回復變成護盾的 Bug)
            // ==========================================
            if (dA != null && dB != null)
            {
                if (valA > valB)
                {
                    idxB++;
                    Log($"<color=#FFA500>[拚點勝利] {unitA.unitName} 勝出！({valA} vs {valB})</color>");

                    if (aIsAtk) { finalDmgB = dA.effectValue; unitA.GetComponent<UnitAnimator>()?.PlayAttack(); }
                    else if (aIsBlk) { unitA.GainShield(dA.effectValue); unitA.GetComponent<UnitAnimator>()?.PlayGuard(); Log($"<color=#44AAFF>└ 獲得 {dA.effectValue} 點護盾</color>"); }
                    else if (aIsHeal) { unitA.Heal(dA.effectValue); unitA.GetComponent<UnitAnimator>()?.PlayGuard(); Log($"<color=#00FF00>└ 回復 {dA.effectValue} 點生命</color>"); }
                    else if (aIsEvd) { unitA.GetComponent<UnitAnimator>()?.PlayDodge(); Log($"<color=#00FF00>└ 閃避成功</color>"); }

                    if (!aIsEvd) idxA++;
                }
                else if (valB > valA)
                {
                    idxA++;
                    Log($"<color=#FFA500>[拚點勝利] {unitB.unitName} 勝出！({valB} vs {valA})</color>");

                    if (bIsAtk) { finalDmgA = dB.effectValue; unitB.GetComponent<UnitAnimator>()?.PlayAttack(); }
                    else if (bIsBlk) { unitB.GainShield(dB.effectValue); unitB.GetComponent<UnitAnimator>()?.PlayGuard(); Log($"<color=#44AAFF>└ 獲得 {dB.effectValue} 點護盾</color>"); }
                    else if (bIsHeal) { unitB.Heal(dB.effectValue); unitB.GetComponent<UnitAnimator>()?.PlayGuard(); Log($"<color=#00FF00>└ 回復 {dB.effectValue} 點生命</color>"); }
                    else if (bIsEvd) { unitB.GetComponent<UnitAnimator>()?.PlayDodge(); Log($"<color=#00FF00>└ 閃避成功</color>"); }

                    if (!bIsEvd) idxB++;
                }
                else
                {
                    idxA++; idxB++;
                    Log($"<color=#AAAAAA>[平手] 武器相交，火花四濺！({valA} vs {valB})</color>");

                    if (aIsAtk) unitA.GetComponent<UnitAnimator>()?.PlayAttack(); else unitA.GetComponent<UnitAnimator>()?.PlayGuard();
                    if (bIsAtk) unitB.GetComponent<UnitAnimator>()?.PlayAttack(); else unitB.GetComponent<UnitAnimator>()?.PlayGuard();
                    CameraDirector.Instance?.TriggerImpulse(0.5f);
                }
            }
            else if (dA != null)
            {
                idxA++;
                Log($"<color=#FFA500>[單方行動] 執行！(骰: {valA})</color>");

                if (aIsAtk) { finalDmgB = dA.effectValue; unitA.GetComponent<UnitAnimator>()?.PlayAttack(); }
                else if (aIsBlk) { unitA.GainShield(dA.effectValue); unitA.GetComponent<UnitAnimator>()?.PlayGuard(); Log($"<color=#44AAFF>└ 獲得 {dA.effectValue} 點護盾</color>"); }
                else if (aIsHeal) { unitA.Heal(dA.effectValue); unitA.GetComponent<UnitAnimator>()?.PlayGuard(); Log($"<color=#00FF00>└ 回復 {dA.effectValue} 點生命</color>"); }
            }
            else if (dB != null)
            {
                idxB++;
                Log($"<color=#FFA500>[單方行動] 執行！(骰: {valB})</color>");

                if (bIsAtk) { finalDmgA = dB.effectValue; unitB.GetComponent<UnitAnimator>()?.PlayAttack(); }
                else if (bIsBlk) { unitB.GainShield(dB.effectValue); unitB.GetComponent<UnitAnimator>()?.PlayGuard(); Log($"<color=#44AAFF>└ 獲得 {dB.effectValue} 點護盾</color>"); }
                else if (bIsHeal) { unitB.Heal(dB.effectValue); unitB.GetComponent<UnitAnimator>()?.PlayGuard(); Log($"<color=#00FF00>└ 回復 {dB.effectValue} 點生命</color>"); }
            }

            if (finalDmgA > 0) { int actA = unitA.TakeDamage(finalDmgA); Log($"<color=#FF0000>└ 造成 {actA} 點實質傷害！</color>"); CameraDirector.Instance?.TriggerImpulse(1.2f); }
            if (finalDmgB > 0) { int actB = unitB.TakeDamage(finalDmgB); Log($"<color=#FF0000>└ 造成 {actB} 點實質傷害！</color>"); CameraDirector.Instance?.TriggerImpulse(1.2f); }

            yield return StartCoroutine(HitStopRoutine(normalHitStopTime, 0.1f));
            yield return new WaitForSeconds(0.4f);
        }

        StartCoroutine(MoveUnitRoutine(unitA.transform, startPosA, 0.3f));
        yield return StartCoroutine(MoveUnitRoutine(unitB.transform, startPosB, 0.3f));
        yield return new WaitForSeconds(0.1f);
    }

    // ==========================================
    // ★ 移動時連動「衝刺」與「待機」動作
    // ==========================================
    private IEnumerator MoveUnitRoutine(Transform target, Vector3 destination, float duration)
    {
        var anim = target.GetComponent<UnitAnimator>();
        anim?.PlayDash(); // 開始移動時切換衝刺圖

        Vector3 start = target.position; destination.y = start.y;
        float elapsed = 0f;
        while (elapsed < duration) { target.position = Vector3.Lerp(start, destination, elapsed / duration); elapsed += Time.deltaTime; yield return null; }
        target.position = destination;

        anim?.PlayIdle(); // 到達定點後切回站立
    }

    private IEnumerator HitStopRoutine(float duration, float timeScale) { Time.timeScale = timeScale; yield return new WaitForSecondsRealtime(duration); Time.timeScale = 1f; }

    private bool CheckBattleEnd()
    {
        if (Player.CurrentHP <= 0) { StartCoroutine(EndBattleSequence(false)); return true; }
        List<BattleUnit> deadEnemies = Enemies.Where(e => e.CurrentHP <= 0).ToList();
        bool keyTargetDefeated = false;

        foreach (var dead in deadEnemies)
        {
            if (dead.isKeyTarget) keyTargetDefeated = true;
            dead.gameObject.SetActive(false);
            Enemies.Remove(dead);
        }

        if (keyTargetDefeated || Enemies.Count == 0) { StartCoroutine(EndBattleSequence(true)); return true; }
        return false;
    }

    private bool _lastBattleResult = false;
    public void ConfirmBattleEnd()
    {
        if (CurrentState != BattleState.BattleEnd) return;
        _ui.DisableEntireBattleUI();
        var gm = FindAnyObjectByType<GameManager>();
        if (gm != null) gm.SendMessage("OnBattleFinished", _lastBattleResult, SendMessageOptions.DontRequireReceiver);
    }

    private IEnumerator EndBattleSequence(bool playerWins)
    {
        _lastBattleResult = playerWins;
        ChangeState(BattleState.BattleEnd);
        OnBattleEnded?.Invoke(playerWins);
        yield break;
    }
}