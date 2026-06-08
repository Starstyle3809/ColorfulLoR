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
    public float staggerFovShake = 20f;
    public float preClashWaitTime = 0.8f;
    public float normalHitStopTime = 0.05f;
    public float staggerHitStopDuration = 1.0f;
    public float staggerTimeScale = 0.02f;

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

    private void LateUpdate()
    {
        if (CurrentState == BattleState.BattleEnd || !_isCameraTracking) return;

        if (_camFocusA != null && _camFocusB != null)
        {
            Vector3 midPoint = (_camFocusA.position + _camFocusB.position) / 2f;
            float dist = Vector3.Distance(_camFocusA.position, _camFocusB.position);

            float targetZ = _isRangedClash ? (-8f - (dist * 0.35f)) : -10f;
            float targetFOV = _isRangedClash ? Mathf.Clamp(45f + (dist * 1.5f), 45f, 75f) : _originalFOV;

            Camera.main.transform.position = Vector3.Lerp(Camera.main.transform.position, new Vector3(midPoint.x, 2.5f, targetZ), Time.deltaTime * 6f);
            Camera.main.fieldOfView = Mathf.Lerp(Camera.main.fieldOfView, targetFOV, Time.deltaTime * 6f) + _fovShakeOffset;
        }
    }

    // ★ 新增：供 UI 讀取，判斷哪些卡片已經被裝填了
    public List<SpeedDiceSlot> GetPlayerSlots()
    {
        return _allActiveSlots.Where(s => s.IsPlayerSlot).ToList();
    }

    public void StartBattle(BattleUnit player, List<BattleUnit> enemies, bool isAmbush)
    {
        if (_cinemachineBrain != null) _cinemachineBrain.enabled = false;
        if (_ui != null) _ui.ShowCombatUI();

        if (Camera.main != null)
        {
            Camera.main.transform.position = new Vector3(0, 2.5f, -12f);
            Camera.main.transform.rotation = Quaternion.Euler(5f, 0, 0);
            Camera.main.fieldOfView = 60f;
            _originalCamPos = Camera.main.transform.position;
            _originalFOV = 60f;
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
        Player.ResetStaggerAtRoundStart(); foreach (var e in Enemies) e.ResetStaggerAtRoundStart();
        Player.DrawCards(1); foreach (var e in Enemies) e.DrawCards(1);

        if (_ui != null)
        {
            _ui.ClearAllSlots(); _allActiveSlots.Clear();

            int totalEnemySlots = 0;
            foreach (var e in Enemies) totalEnemySlots += e.speedDiceCount;
            int playerSlotCount = Mathf.Max(Player.speedDiceCount, totalEnemySlots);

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
                    if (enemy.Hand.Count > 0 && enemy.staggerTurnsLeft <= 0)
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
        if (playerSlots.Any(s => s.AssignedCard == null && s.Owner.staggerTurnsLeft <= 0)) { Log("<color=#FF4444>[警告] 您還有行動槽未配置書頁！</color>"); return; }
        StartCoroutine(ExecuteAllSlotsRoutine());
    }

    private IEnumerator ExecuteAllSlotsRoutine()
    {
        ChangeState(BattleState.ClashResolution);
        var sortedSlots = _allActiveSlots.Where(s => s.AssignedCard != null).OrderByDescending(s => s.Speed).ToList();

        foreach (var slot in sortedSlots)
        {
            if (slot.IsResolved || slot.Owner.CurrentHP <= 0 || slot.Owner.staggerTurnsLeft > 0) continue;
            SpeedDiceSlot targetSlot = slot.TargetSlot; BattleUnit targetUnit = slot.TargetUnit;

            if (targetSlot != null && !targetSlot.IsResolved && targetSlot.Owner.CurrentHP > 0 && targetSlot.Owner.staggerTurnsLeft <= 0)
            {
                yield return StartCoroutine(ResolveClash(slot.Owner, slot.AssignedCard, targetSlot.Owner, targetSlot.AssignedCard));
                slot.Owner.Discard(slot.AssignedCard); slot.Owner.Hand.Remove(slot.AssignedCard); slot.IsResolved = true;
                if (targetSlot.AssignedCard != null) { targetSlot.Owner.Discard(targetSlot.AssignedCard); targetSlot.Owner.Hand.Remove(targetSlot.AssignedCard); }
                targetSlot.IsResolved = true;
            }
            else
            {
                if (targetUnit == null && Enemies.Count > 0) targetUnit = slot.Owner == Player ? Enemies[0] : Player;
                if (targetUnit != null && targetUnit.CurrentHP > 0) yield return StartCoroutine(ResolveClash(slot.Owner, slot.AssignedCard, targetUnit, null));
                slot.Owner.Discard(slot.AssignedCard); slot.Owner.Hand.Remove(slot.AssignedCard); slot.IsResolved = true;
            }
        }

        _camFocusA = null; _camFocusB = null; _isRangedClash = false; _isCameraTracking = false;
        yield return StartCoroutine(ResetCameraRoutine());

        if (!CheckBattleEnd()) { currentRound++; StartRound(); }
    }

    private IEnumerator ResolveClash(BattleUnit unitA, CardData cardA, BattleUnit unitB, CardData cardB)
    {
        Log($"<b>[交鋒] {unitA.unitName} vs {unitB.unitName}</b>");
        _camFocusA = unitA.transform; _camFocusB = unitB.transform;
        _isCameraTracking = true;

        Vector3 startPosA = unitA.transform.position; Vector3 startPosB = unitB.transform.position;
        Vector3 currentPosA = startPosA; Vector3 currentPosB = startPosB;
        int idxA = 0, idxB = 0; List<DiceData> diceA = cardA.diceList; List<DiceData> diceB = cardB != null ? cardB.diceList : new List<DiceData>();

        bool aWasStaggered = unitA.IsStaggered;
        bool bWasStaggered = unitB.IsStaggered;

        while (idxA < diceA.Count || idxB < diceB.Count)
        {
            if (unitA.CurrentHP <= 0 || unitB.CurrentHP <= 0) break;
            if (unitA.IsStaggered && idxA < diceA.Count) { idxA = diceA.Count; Log($"<color=#AAAAAA>[中斷] {unitA.unitName} 處於混亂，後續行動強制取消！</color>"); }
            if (unitB.IsStaggered && idxB < diceB.Count) { idxB = diceB.Count; Log($"<color=#AAAAAA>[中斷] {unitB.unitName} 處於混亂，後續行動強制取消！</color>"); }

            DiceData dA = idxA < diceA.Count ? diceA[idxA] : null; DiceData dB = idxB < diceB.Count ? diceB[idxB] : null;

            bool aIsAtk = dA != null && (dA.type == DiceType.MeleeAttack || dA.type == DiceType.RangedAttack);
            bool bIsAtk = dB != null && (dB.type == DiceType.MeleeAttack || dB.type == DiceType.RangedAttack);
            bool aIsBlk = dA != null && dA.type == DiceType.Block; bool bIsBlk = dB != null && dB.type == DiceType.Block;
            bool aIsEvd = dA != null && dA.type == DiceType.Evade; bool bIsEvd = dB != null && dB.type == DiceType.Evade;
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

            bool aEffectivelyAlone = dA != null && aIsMelee && (dB == null || unitB.IsStaggered) && dist > 2f;
            bool bEffectivelyAlone = dB != null && bIsMelee && (dA == null || unitA.IsStaggered) && dist > 2f;
            if (aEffectivelyAlone && dashTargetA == currentPosA) { dashTargetA = currentPosB - currentDir * 1.5f; currentPosA = dashTargetA; }
            if (bEffectivelyAlone && dashTargetB == currentPosB) { dashTargetB = currentPosA + currentDir * 1.5f; currentPosB = dashTargetB; }

            if (dashTargetA != currentPosA) StartCoroutine(MoveUnitRoutine(unitA.transform, dashTargetA, 0.15f));
            if (dashTargetB != currentPosB) StartCoroutine(MoveUnitRoutine(unitB.transform, dashTargetB, 0.15f));
            if (dashTargetA != currentPosA || dashTargetB != currentPosB) yield return new WaitForSeconds(0.2f);

            if (dA != null && dB != null)
            {
                Log($"<color=#00FFFF>【拼點準備】 {unitA.unitName}({dA.GetTypeName()}) vs {unitB.unitName}({dB.GetTypeName()})</color>");
                yield return new WaitForSeconds(preClashWaitTime);
            }
            else if (dA != null)
            {
                Log($"<color=#FFA500>【單方準備】 {unitA.unitName}({dA.GetTypeName()}) 發動攻擊！</color>");
                yield return new WaitForSeconds(preClashWaitTime * 0.8f);
            }
            else if (dB != null)
            {
                Log($"<color=#FFA500>【單方準備】 {unitB.unitName}({dB.GetTypeName()}) 發動攻擊！</color>");
                yield return new WaitForSeconds(preClashWaitTime * 0.8f);
            }

            if ((aIsBlk || aIsEvd) && (bIsBlk || bIsEvd)) { Log($"<color=#AAAAAA>[對峙] 雙方皆為防守姿態，互相架開。</color>"); idxA++; idxB++; continue; }
            if (dA != null && dA.type == DiceType.Heal) { int h = dA.Roll(); unitA.Heal(h); Log($"<color=#00FF00>[回復] {unitA.unitName} 恢復了 {h} 點生命 (骰出 {h})。</color>"); idxA++; continue; }
            if (dB != null && dB.type == DiceType.Heal) { int h = dB.Roll(); unitB.Heal(h); Log($"<color=#00FF00>[回復] {unitB.unitName} 恢復了 {h} 點生命 (骰出 {h})。</color>"); idxB++; continue; }

            int valA = dA != null ? dA.Roll() : 0; int valB = dB != null ? dB.Roll() : 0;
            int baseDmgA = 0, stagA = 0, healStagA = 0; int baseDmgB = 0, stagB = 0, healStagB = 0;

            if (dA != null && dB != null)
            {
                if (valA > valB)
                {
                    idxB++;
                    if (aIsMelee && bIsRanged)
                    {
                        baseDmgB = 0; stagB = 0;
                        if (dist > 2.5f) currentPosA += currentDir * 2.5f;
                        Log($"<color=#00FFFF>[彈開] {unitA.unitName} 劈開了遠程攻擊，強行逼近！(骰出 {valA} vs {valB})</color>");
                    }
                    else
                    {
                        if (aIsAtk && bIsBlk) { baseDmgB = Mathf.Max(0, valA - valB); stagB = Mathf.Max(0, valA - valB); Log($"<color=#44AAFF>[格擋] {unitB.unitName} 盾牌吸收衝擊！(骰出 {valA} vs {valB})</color>"); }
                        else if (aIsAtk && bIsEvd) { baseDmgB = valA; stagB = valA; Log($"<color=#FFA500>[擊破] {unitA.unitName} 擊破閃避！(骰出 {valA} vs {valB})</color>"); }
                        else if (aIsBlk && bIsAtk) { stagB = Mathf.Max(0, valA - valB); Log($"<color=#44AAFF>[反震] {unitA.unitName} 完美格擋並震傷對方！(骰出 {valA} vs {valB})</color>"); }
                        else if (aIsEvd && bIsAtk) { healStagA = valA; Log($"<color=#00FF00>[閃避] {unitA.unitName} 躲避成功恢復混亂！(骰出 {valA} vs {valB})</color>"); }
                        else { baseDmgB = valA; stagB = valA; Log($"<color=#FFA500>[拼點勝利] {unitA.unitName} 壓制對手！(骰出 {valA} vs {valB})</color>"); }

                        if (!aIsEvd) idxA++;
                        if (aIsMelee && !bIsMelee && !bIsBlk && !bIsEvd && dist > 2.5f) currentPosA += currentDir * 1f;
                    }
                    StartCoroutine(FOVShakeRoutine(0.2f, clashFovShake));
                }
                else if (valB > valA)
                {
                    idxA++;
                    if (bIsMelee && aIsRanged)
                    {
                        baseDmgA = 0; stagA = 0;
                        if (dist > 2.5f) currentPosB -= currentDir * 2.5f;
                        Log($"<color=#00FFFF>[彈開] {unitB.unitName} 劈開了遠程攻擊，強行逼近！(骰出 {valB} vs {valA})</color>");
                    }
                    else
                    {
                        if (bIsAtk && aIsBlk) { baseDmgA = Mathf.Max(0, valB - valA); stagA = Mathf.Max(0, valB - valA); Log($"<color=#44AAFF>[格擋] {unitA.unitName} 盾牌吸收衝擊！(骰出 {valA} vs {valB})</color>"); }
                        else if (bIsAtk && aIsEvd) { baseDmgA = valB; stagA = valB; Log($"<color=#FFA500>[擊破] {unitB.unitName} 擊破閃避！(骰出 {valA} vs {valB})</color>"); }
                        else if (bIsBlk && aIsAtk) { stagA = Mathf.Max(0, valB - valA); Log($"<color=#44AAFF>[反震] {unitB.unitName} 完美格擋並震傷對方！(骰出 {valA} vs {valB})</color>"); }
                        else if (bIsEvd && aIsAtk) { healStagB = valB; Log($"<color=#00FF00>[閃避] {unitB.unitName} 躲避成功恢復混亂！(骰出 {valA} vs {valB})</color>"); }
                        else { baseDmgA = valB; stagA = valB; Log($"<color=#FFA500>[拼點勝利] {unitB.unitName} 壓制對手！(骰出 {valA} vs {valB})</color>"); }

                        if (!bIsEvd) idxB++;
                        if (bIsMelee && !aIsMelee && !aIsBlk && !aIsEvd && dist > 2.5f) currentPosB -= currentDir * 1f;
                    }
                    StartCoroutine(FOVShakeRoutine(0.2f, clashFovShake));
                }
                else
                {
                    Log($"<color=#AAAAAA>[拼點] 平手！({valA} vs {valB}) 武器相交，火花四濺。</color>");
                    idxA++; idxB++;
                    StartCoroutine(FOVShakeRoutine(0.2f, clashFovShake * 0.5f));
                }
            }
            else if (dA != null)
            {
                idxA++;
                StartCoroutine(FOVShakeRoutine(0.2f, clashFovShake));
                if (aIsAtk) { baseDmgB = valA; stagB = valA; Log($"<color=#FFA500>[命中] {unitA.unitName} 單方攻擊命中！(骰出 {valA})</color>"); }
            }
            else if (dB != null)
            {
                idxB++;
                StartCoroutine(FOVShakeRoutine(0.2f, clashFovShake));
                if (bIsAtk) { baseDmgA = valB; stagA = valB; Log($"<color=#FFA500>[命中] {unitB.unitName} 單方攻擊命中！(骰出 {valB})</color>"); }
            }

            if (baseDmgA > 0 || stagA > 0)
            {
                int actualDmgA = unitA.TakeDamage(baseDmgA); if (baseDmgA == 0 && stagA > 0) unitA.TakeStaggerDamage(stagA);
                Log($"<color=#FF0000>└ 造成 {actualDmgA} 點生命與 {stagA} 點混亂傷害！</color>");
            }
            if (healStagA > 0) unitA.HealStagger(healStagA);

            if (baseDmgB > 0 || stagB > 0)
            {
                int actualDmgB = unitB.TakeDamage(baseDmgB); if (baseDmgB == 0 && stagB > 0) unitB.TakeStaggerDamage(stagB);
                Log($"<color=#FF0000>└ 造成 {actualDmgB} 點生命與 {stagB} 點混亂傷害！</color>");
            }
            if (healStagB > 0) unitB.HealStagger(healStagB);

            if (!aWasStaggered && unitA.IsStaggered)
            {
                aWasStaggered = true; Log($"<color=#FFFF00><b>【擊破】{unitA.unitName} 防線崩潰，陷入混亂！</b></color>");
                _ui.ShowFloatingText(unitA, "混亂!", Color.yellow);
                StartCoroutine(FOVShakeRoutine(0.4f, staggerFovShake));
                yield return StartCoroutine(HitStopRoutine(staggerHitStopDuration, staggerTimeScale));
            }
            else if (!bWasStaggered && unitB.IsStaggered)
            {
                bWasStaggered = true; Log($"<color=#FFFF00><b>【擊破】{unitB.unitName} 防線崩潰，陷入混亂！</b></color>");
                _ui.ShowFloatingText(unitB, "混亂!", Color.yellow);
                StartCoroutine(FOVShakeRoutine(0.4f, staggerFovShake));
                yield return StartCoroutine(HitStopRoutine(staggerHitStopDuration, staggerTimeScale));
            }
            else
            {
                yield return StartCoroutine(HitStopRoutine(normalHitStopTime, 0.1f));
            }

            yield return new WaitForSeconds(0.4f);
        }

        StartCoroutine(MoveUnitRoutine(unitA.transform, startPosA, 0.3f));
        yield return StartCoroutine(MoveUnitRoutine(unitB.transform, startPosB, 0.3f));
        yield return new WaitForSeconds(0.1f);
    }

    private IEnumerator ResetCameraRoutine()
    {
        while (Vector3.Distance(Camera.main.transform.position, _originalCamPos) > 0.1f)
        {
            Camera.main.transform.position = Vector3.Lerp(Camera.main.transform.position, _originalCamPos, Time.deltaTime * 5f);
            Camera.main.fieldOfView = Mathf.Lerp(Camera.main.fieldOfView, _originalFOV, Time.deltaTime * 5f);
            yield return null;
        }
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
        foreach (var dead in deadEnemies) { dead.gameObject.SetActive(false); Enemies.Remove(dead); }

        if ((endBattleOnFirstKill && deadEnemies.Count > 0) || Enemies.Count == 0)
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