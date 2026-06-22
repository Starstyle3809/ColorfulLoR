using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    public enum BattleState { Setup, AssignPhase, ClashResolution, BattleEnd }
    public BattleState CurrentState { get; private set; }

    [Header("特殊戰鬥規則")]
    public bool endBattleOnFirstKill = false;

    [Header("戰鬥單位")]
    public BattleUnit Player;
    public List<BattleUnit> Enemies = new List<BattleUnit>();
    public int currentRound = 1;

    [Header("打擊感參數 (可自由調整)")]
    public float clashFovShake = 4f;
    public float preClashWaitTime = 0.8f;
    public float normalHitStopTime = 0.05f;

    public event System.Action<BattleState> OnStateChanged;
    public event System.Action<string> OnBattleLog;
    public event System.Action<bool> OnBattleEnded;
    public event System.Action OnRoundStart;

    private List<SpeedDiceSlot> _allActiveSlots = new List<SpeedDiceSlot>();
    private BattleUIController _ui;

    private Vector3 _originalCamPos;
    private float _originalFOV = 60f;
    private Transform _camFocusA;
    private Transform _camFocusB;
    private MonoBehaviour _cinemachineBrain;
    private float _originalOrthoSize = 5f;

    private float _fovShakeOffset = 0f;
    private bool _isRangedClash = false;
    private bool _isCameraTracking = false;

    private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }

    private void Start()
    {
        _ui = FindAnyObjectByType<BattleUIController>();
        if (Camera.main != null) _cinemachineBrain = Camera.main.GetComponent("CinemachineBrain") as MonoBehaviour;
    }

    private void Log(string msg) => OnBattleLog?.Invoke(msg);

    public List<SpeedDiceSlot> GetPlayerSlots() { return _allActiveSlots.Where(s => s.IsPlayerSlot).ToList(); }

    public void StartBattle(BattleUnit player, List<BattleUnit> enemies, bool isAmbush)
    {
        if (_ui != null) _ui.ShowCombatUI();

        if (Camera.main != null)
        {
            Camera.main.transform.position = new Vector3(0, 2.5f, -12f);
            Camera.main.transform.rotation = Quaternion.Euler(5f, 0, 0);
            Camera.main.fieldOfView = 60f;
            _originalFOV = 60f;
            _originalOrthoSize = Camera.main.orthographicSize;
            _originalCamPos = Camera.main.transform.position;
        }

        Player = player; Enemies = enemies; currentRound = 1;
        Player.Initialize(); foreach (var e in Enemies) e.Initialize();
        ChangeState(BattleState.Setup); StartCoroutine(BattleSetupRoutine(isAmbush));
    }

    private void ChangeState(BattleState newState) { CurrentState = newState; OnStateChanged?.Invoke(CurrentState); }

    private IEnumerator BattleSetupRoutine(bool isAmbush)
    {
        Log("<color=#AAAAAA>[系統] 雙方拉開距離，準備交鋒。</color>");
        Vector3 center = Vector3.zero;
        StartCoroutine(MoveUnitRoutine(Player.transform, center + new Vector3(-8f, 0, 0), 0.5f));
        for (int i = 0; i < Enemies.Count; i++) StartCoroutine(MoveUnitRoutine(Enemies[i].transform, center + new Vector3(8f + (i * 2f), 0, (i * -1f)), 0.5f));
        yield return new WaitForSeconds(0.6f);

        if (Enemies.Count > 0) { Player.transform.LookAt(Enemies[0].transform); foreach (var e in Enemies) e.transform.LookAt(Player.transform); }
        Player.DrawCards(4); foreach (var e in Enemies) e.DrawCards(4);

        if (isAmbush && Enemies.Count > 0)
        {
            Enemies[0].ApplyAmbushPenalty();
            _ui.ShowFloatingText(Enemies[0], "被偷襲!", Color.red);
            Log($"<color=#FFA500>[偷襲] 成功！{Enemies[0].unitName} 受到先制傷害！</color>");
        }

        yield return new WaitForSeconds(1f); StartRound();
    }

    private void StartRound()
    {
        CurrentState = BattleState.Setup;

        int totalEnemySlots = 0;
        foreach (var e in Enemies) totalEnemySlots += e.speedDiceCount;
        int playerSlotCount = Mathf.Max(Player.speedDiceCount, totalEnemySlots);

        if (Player != null && Player.CurrentHP > 0) Player.DrawCards(playerSlotCount);

        foreach (var enemy in Enemies)
        {
            if (enemy == null || enemy.CurrentHP <= 0) continue;
            enemy.DrawCards(enemy.speedDiceCount);
        }

        if (_ui != null)
        {
            _ui.ClearAllSlots(); _allActiveSlots.Clear();

            for (int i = 0; i < playerSlotCount; i++)
            {
                _allActiveSlots.Add(_ui.CreateSlot(Player, Random.Range(Player.minSpeed, Player.maxSpeed + 1), true));
            }

            var pSlots = _allActiveSlots.Where(s => s.IsPlayerSlot).ToList();

            foreach (var enemy in Enemies)
            {
                int eSlotCount = enemy.speedDiceCount;
                for (int i = 0; i < eSlotCount; i++)
                {
                    SpeedDiceSlot slot = _ui.CreateSlot(enemy, Random.Range(enemy.minSpeed, enemy.maxSpeed + 1), false);
                    _allActiveSlots.Add(slot);
                    // 拔除 stagger 判斷
                    if (enemy.Hand.Count > 0)
                    {
                        CardData chosenCard = enemy.Hand[Random.Range(0, enemy.Hand.Count)];
                        SpeedDiceSlot randomTarget = pSlots.Count > 0 ? pSlots[Random.Range(0, pSlots.Count)] : null;
                        slot.SetAction(chosenCard, randomTarget, null);
                    }
                }
            }
        }
        OnRoundStart?.Invoke(); ChangeState(BattleState.AssignPhase); Log($"<color=#AAAAAA>[系統] --- 第 {currentRound} 幕 --- 請配置指令。</color>");
    }

    public void OnStartClashButtonClicked()
    {
        if (CurrentState != BattleState.AssignPhase) return;
        var playerSlots = _allActiveSlots.Where(s => s.Owner == Player).ToList();
        if (playerSlots.Any(s => s.AssignedCard == null)) { Log("<color=#FF4444>[警告] 您還有行動槽未配置書頁！</color>"); return; }
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

                if (originalTarget != null && originalTarget.CurrentHP > 0)
                {
                    yield return StartCoroutine(ResolveClash(slot.Owner, slot.AssignedCard, originalTarget, null));
                }
                else
                {
                    Log($"<color=#AAAAAA>[中斷] {slot.Owner.unitName} 的目標已倒下，取消攻擊。</color>");
                    yield return new WaitForSeconds(0.4f);
                }

                slot.Owner.Discard(slot.AssignedCard);
                slot.Owner.Hand.Remove(slot.AssignedCard);
                slot.IsResolved = true;
            }
        }

        _camFocusA = null; _camFocusB = null; _isRangedClash = false; _isCameraTracking = false;
        CameraDirector.Instance?.SwitchToBattleOverview();

        if (!CheckBattleEnd()) { currentRound++; StartRound(); }
    }

    private IEnumerator ResolveClash(BattleUnit unitA, CardData cardA, BattleUnit unitB, CardData cardB)
    {
        Log($"<b>[交鋒] {unitA.unitName} vs {unitB.unitName}</b>");
        _camFocusA = unitA.transform; _camFocusB = unitB.transform;
        CameraDirector.Instance?.SwitchToBattleClash(unitA.transform, unitB.transform);

        // ★ 每回合交鋒開始，清空舊護盾
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
            bool aIsRanged = dA != null && dA.type == DiceType.RangedAttack; bool bIsRanged = dB != null && dB.type == DiceType.RangedAttack;

            _isRangedClash = aIsRanged || bIsRanged;

            Vector3 currentDir = (currentPosB - currentPosA).normalized; if (currentDir == Vector3.zero) currentDir = Vector3.right; currentDir.y = 0;
            float dist = Vector3.Distance(currentPosA, currentPosB);
            Vector3 dashTargetA = currentPosA; Vector3 dashTargetB = currentPosB;

            if (dist > 3f)
            {
                if (aIsMelee && bIsMelee) { Vector3 center = (currentPosA + currentPosB) / 2f; dashTargetA = center - currentDir * 1.2f; dashTargetB = center + currentDir * 1.2f; }
                else if (aIsMelee && (bIsBlk || bIsEvd)) { dashTargetA = currentPosB - currentDir * 1.5f; }
                else if (bIsMelee && (aIsBlk || aIsEvd)) { dashTargetB = currentPosA + currentDir * 1.5f; }
                else if (aIsMelee && bIsRanged) { dashTargetA = currentPosA + currentDir * 1.0f; }
                else if (bIsMelee && aIsRanged) { dashTargetB = currentPosB - currentDir * 1.0f; }
                else if (aIsMelee) { dashTargetA = currentPosB - currentDir * 2f; }
                else if (bIsMelee) { dashTargetB = currentPosA + currentDir * 2f; }
            }

            if (dA != null && dB == null && aIsMelee && dist > 2f) { dashTargetA = currentPosB - currentDir * 1.5f; currentPosA = dashTargetA; }
            if (dB != null && dA == null && bIsMelee && dist > 2f) { dashTargetB = currentPosA + currentDir * 1.5f; currentPosB = dashTargetB; }

            bool aEffectivelyAlone = dA != null && aIsMelee && dB == null && dist > 2f;
            bool bEffectivelyAlone = dB != null && bIsMelee && dA == null && dist > 2f;
            if (aEffectivelyAlone && dashTargetA == currentPosA) { dashTargetA = currentPosB - currentDir * 1.5f; currentPosA = dashTargetA; }
            if (bEffectivelyAlone && dashTargetB == currentPosB) { dashTargetB = currentPosA + currentDir * 1.5f; currentPosB = dashTargetB; }

            if (dashTargetA != currentPosA) StartCoroutine(MoveUnitRoutine(unitA.transform, dashTargetA, 0.15f));
            if (dashTargetB != currentPosB) StartCoroutine(MoveUnitRoutine(unitB.transform, dashTargetB, 0.15f));
            if (dashTargetA != currentPosA || dashTargetB != currentPosB) yield return new WaitForSeconds(0.2f);

            if (dA != null && dB != null) { Log($"<color=#00FFFF>【拚點準備】 {unitA.unitName}({dA.GetTypeName()}) vs {unitB.unitName}({dB.GetTypeName()})</color>"); yield return new WaitForSeconds(preClashWaitTime); }
            else if (dA != null) { Log($"<color=#FFA500>【單方準備】 {unitA.unitName}({dA.GetTypeName()}) 發動！</color>"); yield return new WaitForSeconds(preClashWaitTime * 0.8f); }
            else if (dB != null) { Log($"<color=#FFA500>【單方準備】 {unitB.unitName}({dB.GetTypeName()}) 發動！</color>"); yield return new WaitForSeconds(preClashWaitTime * 0.8f); }

            // ★ 擲骰子：單純比大小決定勝負
            int valA = dA != null ? dA.Roll() : 0;
            int valB = dB != null ? dB.Roll() : 0;

            // ★ 紀錄要扣除的實質傷害
            int finalDmgA = 0;
            int finalDmgB = 0;

            if (dA != null && dB != null)
            {
                if (valA > valB) // A 贏
                {
                    idxB++;
                    if (aIsMelee && bIsRanged) { if (dist > 2.5f) currentPosA += currentDir * 2.5f; Log($"<color=#00FFFF>[彈開] {unitA.unitName} 劈開遠程攻擊逼近！(拚點 {valA} vs {valB})</color>"); }
                    else
                    {
                        Log($"<color=#FFA500>[拚點勝利] {unitA.unitName} 勝出！(拚點 {valA} vs {valB})</color>");

                        // ★ 根據卡牌設定的 effectValue 來結算！
                        if (aIsAtk) { finalDmgB = dA.effectValue; }
                        else if (aIsBlk) { unitA.GainShield(dA.effectValue); Log($"<color=#44AAFF>└ 獲得 {dA.effectValue} 點護盾</color>"); }
                        else if (aIsHeal) { unitA.Heal(dA.effectValue); Log($"<color=#00FF00>└ 恢復 {dA.effectValue} 點生命</color>"); }

                        if (!aIsEvd) idxA++;
                        if (aIsMelee && !bIsMelee && !bIsBlk && !bIsEvd && dist > 2.5f) currentPosA += currentDir * 1f;
                    }
                    StartCoroutine(FOVShakeRoutine(0.2f, clashFovShake));
                }
                else if (valB > valA) // B 贏
                {
                    idxA++;
                    if (bIsMelee && aIsRanged) { if (dist > 2.5f) currentPosB -= currentDir * 2.5f; Log($"<color=#00FFFF>[彈開] {unitB.unitName} 劈開遠程攻擊逼近！(拚點 {valB} vs {valA})</color>"); }
                    else
                    {
                        Log($"<color=#FFA500>[拚點勝利] {unitB.unitName} 勝出！(拚點 {valB} vs {valA})</color>");

                        // ★ 根據卡牌設定的 effectValue 來結算！
                        if (bIsAtk) { finalDmgA = dB.effectValue; }
                        else if (bIsBlk) { unitB.GainShield(dB.effectValue); Log($"<color=#44AAFF>└ 獲得 {dB.effectValue} 點護盾</color>"); }
                        else if (bIsHeal) { unitB.Heal(dB.effectValue); Log($"<color=#00FF00>└ 恢復 {dB.effectValue} 點生命</color>"); }

                        if (!bIsEvd) idxB++;
                        if (bIsMelee && !aIsMelee && !aIsBlk && !aIsEvd && dist > 2.5f) currentPosB -= currentDir * 1f;
                    }
                    StartCoroutine(FOVShakeRoutine(0.2f, clashFovShake));
                }
                else // 平手
                {
                    Log($"<color=#AAAAAA>[平手] 武器相交，火花四濺！({valA} vs {valB})</color>");
                    idxA++; idxB++;
                    StartCoroutine(FOVShakeRoutine(0.2f, clashFovShake * 0.5f));
                    CameraDirector.Instance?.TriggerImpulse(0.5f);
                }
            }
            else if (dA != null) // A 單方行動
            {
                idxA++;
                Log($"<color=#FFA500>[單方行動] 執行！(拚點數值 {valA})</color>");
                if (aIsAtk) { finalDmgB = dA.effectValue; }
                else if (aIsBlk) { unitA.GainShield(dA.effectValue); }
                else if (aIsHeal) { unitA.Heal(dA.effectValue); }
                StartCoroutine(FOVShakeRoutine(0.2f, clashFovShake));
            }
            else if (dB != null) // B 單方行動
            {
                idxB++;
                Log($"<color=#FFA500>[單方行動] 執行！(拚點數值 {valB})</color>");
                if (bIsAtk) { finalDmgA = dB.effectValue; }
                else if (bIsBlk) { unitB.GainShield(dB.effectValue); }
                else if (bIsHeal) { unitB.Heal(dB.effectValue); }
                StartCoroutine(FOVShakeRoutine(0.2f, clashFovShake));
            }

            // ===== 結算實質傷害 =====
            if (finalDmgA > 0)
            {
                int actualDmgA = unitA.TakeDamage(finalDmgA);
                Log($"<color=#FF0000>└ 對 {unitA.unitName} 造成 {actualDmgA} 點實質傷害！</color>");
                CameraDirector.Instance?.TriggerImpulse(1.2f);
            }

            if (finalDmgB > 0)
            {
                int actualDmgB = unitB.TakeDamage(finalDmgB);
                Log($"<color=#FF0000>└ 對 {unitB.unitName} 造成 {actualDmgB} 點實質傷害！</color>");
                CameraDirector.Instance?.TriggerImpulse(1.2f);
            }

            yield return StartCoroutine(HitStopRoutine(normalHitStopTime, 0.1f));
            yield return new WaitForSeconds(0.4f);
        }

        StartCoroutine(MoveUnitRoutine(unitA.transform, startPosA, 0.3f));
        yield return StartCoroutine(MoveUnitRoutine(unitB.transform, startPosB, 0.3f));
        yield return new WaitForSeconds(0.1f);
    }

    private IEnumerator FOVShakeRoutine(float duration, float magnitude)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            _fovShakeOffset = Random.Range(-magnitude, magnitude);
            magnitude = Mathf.Lerp(magnitude, 0, elapsed / duration);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        _fovShakeOffset = 0f;
    }

    private IEnumerator HitStopRoutine(float duration, float timeScale) { Time.timeScale = timeScale; yield return new WaitForSecondsRealtime(duration); Time.timeScale = 1f; }

    private IEnumerator MoveUnitRoutine(Transform target, Vector3 destination, float duration)
    {
        Vector3 start = target.position; destination.y = start.y;
        float elapsed = 0f;
        while (elapsed < duration) { target.position = Vector3.Lerp(start, destination, elapsed / duration); elapsed += Time.deltaTime; yield return null; }
        target.position = destination;
    }

    private bool CheckBattleEnd()
    {
        if (Player.CurrentHP <= 0) { StartCoroutine(EndBattleSequence(false)); return true; }

        List<BattleUnit> deadEnemies = Enemies.Where(e => e.CurrentHP <= 0).ToList();
        bool keyTargetDefeated = false;

        foreach (var dead in deadEnemies)
        {
            if (dead.isKeyTarget)
            {
                keyTargetDefeated = true;
                Log($"<color=#FFFF00>[系統] 關鍵目標 {dead.unitName} 被擊破！敵方陣腳大亂！</color>");
            }
            dead.gameObject.SetActive(false);
            Enemies.Remove(dead);
        }

        if (keyTargetDefeated || Enemies.Count == 0)
        {
            StartCoroutine(EndBattleSequence(true));
            return true;
        }
        return false;
    }

    private bool _lastBattleResult = false;

    public void ConfirmBattleEnd()
    {
        if (CurrentState != BattleState.BattleEnd) return;
        _ui.DisableEntireBattleUI();
        if (_cinemachineBrain != null) _cinemachineBrain.enabled = true;
        if (Player != null) Player.transform.rotation = Quaternion.identity;
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