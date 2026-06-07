using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BattleUIController : MonoBehaviour
{
    [Header("UI 容器")]
    public RectTransform playerHUD; public RectTransform enemyHUD;
    public RectTransform playerSlotsGroup; public RectTransform enemySlotsGroup;

    [Header("動態位置偏移設定")]
    public Vector3 hpBottomOffset = new Vector3(0, -0.8f, 0);
    public float slotsForwardDistance = 1.3f;
    public Vector3 slotsVerticalOffset = new Vector3(0, 0.6f, 0);

    [Header("狀態 UI")]
    public TextMeshProUGUI playerHPText; public Slider playerHPSlider; public TextMeshProUGUI playerStaggerText; public Slider playerStaggerSlider; public TextMeshProUGUI playerBuffText;
    public TextMeshProUGUI enemyHPText; public Slider enemyHPSlider; public TextMeshProUGUI enemyStaggerText; public Slider enemyStaggerSlider; public TextMeshProUGUI enemyBuffText;

    [Header("預製物與面板")]
    public GameObject speedDiceSlotPrefab;
    public RectTransform handContainer; public GameObject cardButtonPrefab;
    public TextMeshProUGUI roundText; public Button startClashButton;
    public TextMeshProUGUI battleLogText; public int maxLogLines = 8;
    public GameObject battleEndPanel; public TextMeshProUGUI battleEndText;

    [Header("卡片檢視面板")]
    public GameObject cardInspectPanel;
    public GameObject bgOverlay;            // ★ BG_Overlay：半透明黑底，點擊關閉用
    public Image inspectArtwork; public TextMeshProUGUI inspectNameText; public TextMeshProUGUI inspectDescText;

    // ★ 懸浮提示：改為固定錨點於玩家/敵人區域，不跟著滑鼠飄
    [Header("懸浮提示 (固定區域)")]
    public RectTransform playerTooltipAnchor;   // 指定到 PlayerSlotsGroup 附近的固定位置
    public RectTransform enemyTooltipAnchor;    // 指定到 EnemySlotsGroup 附近的固定位置

    private List<string> _logLines = new();
    private BattleManager _bm; private Camera _mainCamera;

    // ★ 重構：懸浮提示改用獨立的 playerTooltip / enemyTooltip，避免定位問題
    private GameObject _playerTooltip;
    private GameObject _enemyTooltip;

    private void Start()
    {
        _bm = BattleManager.Instance; _mainCamera = Camera.main;
        if (_bm == null) return;
        _bm.OnBattleLog += AppendLog;
        _bm.OnStateChanged += OnStateChanged;
        _bm.OnRoundStart += OnRoundStart;
        _bm.OnBattleEnded += OnBattleEnded;
        if (startClashButton != null) startClashButton.onClick.AddListener(() => _bm.OnStartClashButtonClicked());
        CloseCardSelection();

        // ★ BG_Overlay：確保預設關閉，並綁定點擊事件關閉 CardInspect
        if (bgOverlay)
        {
            bgOverlay.SetActive(false);
            Button overlayBtn = bgOverlay.GetComponent<Button>();
            if (overlayBtn == null) overlayBtn = bgOverlay.AddComponent<Button>();
            overlayBtn.transition = Selectable.Transition.None;
            overlayBtn.onClick.AddListener(HideCardInspect);
        }

        // ★ 結算面板：點擊立刻執行完整的結束流程，不等 WaitUntil
        if (battleEndPanel)
        {
            battleEndPanel.SetActive(false);
            Button endBtn = battleEndPanel.GetComponent<Button>();
            if (endBtn == null) endBtn = battleEndPanel.AddComponent<Button>();
            endBtn.transition = Selectable.Transition.None;
            endBtn.onClick.AddListener(() => {
                if (_bm.CurrentState == BattleManager.BattleState.BattleEnd)
                    _bm.ConfirmBattleEnd();
            });
        }

        // ★ 預先建立兩個懸浮提示物件（預設隱藏）
        _playerTooltip = CreateTooltipObject("PlayerTooltip");
        _enemyTooltip = CreateTooltipObject("EnemyTooltip");
    }

    // ── 建立懸浮提示 GO ───────────────────────────────────
    private GameObject CreateTooltipObject(string name)
    {
        // 掛在 BattleCanvas 根（this.transform 已在 BattleCanvas 下）
        var go = new GameObject(name);
        go.transform.SetParent(this.transform, false);

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.08f, 0.92f);

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(go.transform, false);
        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 22;
        tmp.lineSpacing = 8;
        tmp.alignment = TextAlignmentOptions.Left;
        // ★ 給 Text 一個固定寬度，讓 ContentSizeFitter 只在高度上自動
        var textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero; textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(14, 10); textRt.offsetMax = new Vector2(-14, -10);

        // ★ 固定寬度 240，高度由 ContentSizeFitter 自動撐開
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(240, 0);
        rt.pivot = new Vector2(0.5f, 0f);   // pivot 底部中心

        var csf = go.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        go.SetActive(false);
        return go;
    }

    // ── 更新懸浮提示文字 ──────────────────────────────────
    private void SetTooltipContent(GameObject tooltip, CardData card)
    {
        var tmp = tooltip.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp == null) return;

        string info = $"<color=#FFFFFF><b>{card.cardName}</b></color>\n";
        foreach (var d in card.diceList)
        {
            string color = "#AAAAAA";
            if (d.type == DiceType.MeleeAttack || d.type == DiceType.RangedAttack) color = "#FF4444";
            else if (d.type == DiceType.Block) color = "#44AAFF";
            else if (d.type == DiceType.Evade) color = "#44FF44";
            else if (d.type == DiceType.Heal) color = "#00FF00";
            info += $"<color={color}>[{d.GetTypeName()}] {d.minVal}~{d.maxVal}</color>\n";
        }
        tmp.text = info.TrimEnd();
    }

    private void Update()
    {
        if (_bm == null || _mainCamera == null || _bm.CurrentState == BattleManager.BattleState.BattleEnd) return;

        if (_bm.Player != null && playerHUD != null)
        {
            Vector3 s = _mainCamera.WorldToScreenPoint(_bm.Player.transform.position + hpBottomOffset); s.z = 0; playerHUD.position = s;
            if (playerSlotsGroup != null) { s = _mainCamera.WorldToScreenPoint(_bm.Player.transform.position + (_bm.Player.transform.forward * slotsForwardDistance) + slotsVerticalOffset); s.z = 0; playerSlotsGroup.position = s; }
            RefreshUnitUIValues(_bm.Player, true);
        }

        if (_bm.Enemies.Count > 0 && _bm.Enemies[0] != null && enemyHUD != null)
        {
            Vector3 s = _mainCamera.WorldToScreenPoint(_bm.Enemies[0].transform.position + hpBottomOffset); s.z = 0; enemyHUD.position = s;
            if (enemySlotsGroup != null) { s = _mainCamera.WorldToScreenPoint(_bm.Enemies[0].transform.position + (_bm.Enemies[0].transform.forward * slotsForwardDistance) + slotsVerticalOffset); s.z = 0; enemySlotsGroup.position = s; }
            RefreshUnitUIValues(_bm.Enemies[0], false);
        }

        // ★ 懸浮提示每幀跟著行動槽組的螢幕位置更新（避免 ContentSizeFitter 首幀為 0 的問題）
        UpdateTooltipPosition(_playerTooltip, playerSlotsGroup, true);
        UpdateTooltipPosition(_enemyTooltip, enemySlotsGroup, false);
    }

    // ★ 懸浮提示定位：緊貼在行動槽群組的正上方
    private void UpdateTooltipPosition(GameObject tooltip, RectTransform slotsGroup, bool isPlayer)
    {
        if (tooltip == null || !tooltip.activeSelf || slotsGroup == null) return;

        var rt = tooltip.GetComponent<RectTransform>();

        // 用 anchor 固定在螢幕角落（玩家左上、敵人右上），不跟著 3D 世界位置飄
        if (playerTooltipAnchor != null && isPlayer)
        {
            rt.position = playerTooltipAnchor.position;
        }
        else if (enemyTooltipAnchor != null && !isPlayer)
        {
            rt.position = enemyTooltipAnchor.position;
        }
        else
        {
            // Fallback：跟著行動槽群組位置，往上偏移
            Vector3 pos = slotsGroup.position;
            pos.y += 80f;   // screen-space 往上 80px
            rt.position = pos;
        }
    }

    private void RefreshUnitUIValues(BattleUnit unit, bool isPlayer)
    {
        if (isPlayer)
        {
            if (playerHPText) playerHPText.text = $"HP: {unit.CurrentHP}/{unit.maxHP}"; if (playerHPSlider) playerHPSlider.value = (float)unit.CurrentHP / unit.maxHP;
            if (playerStaggerText) playerStaggerText.text = $"混亂: {unit.CurrentStagger}/{unit.maxStagger}"; if (playerStaggerSlider) playerStaggerSlider.value = (float)unit.CurrentStagger / unit.maxStagger;
            if (playerBuffText) playerBuffText.text = unit.activeBuffs.Count > 0 ? string.Join("\n", unit.activeBuffs) : "Buff";
        }
        else
        {
            if (enemyHPText) enemyHPText.text = $"HP: {unit.CurrentHP}/{unit.maxHP}"; if (enemyHPSlider) enemyHPSlider.value = (float)unit.CurrentHP / unit.maxHP;
            if (enemyStaggerText) enemyStaggerText.text = $"混亂: {unit.CurrentStagger}/{unit.maxStagger}"; if (enemyStaggerSlider) enemyStaggerSlider.value = (float)unit.CurrentStagger / unit.maxStagger;
            if (enemyBuffText) enemyBuffText.text = unit.activeBuffs.Count > 0 ? string.Join("\n", unit.activeBuffs) : "Buff";
        }
    }

    public void ShowFloatingText(BattleUnit unit, string message, Color color)
    {
        RectTransform parentHUD = (unit == _bm.Player) ? playerHUD : enemyHUD;
        if (parentHUD == null || !parentHUD.gameObject.activeInHierarchy) return;

        GameObject popupObj = new GameObject("FloatingTextPopup");
        popupObj.transform.SetParent(parentHUD, false);
        TextMeshProUGUI tmp = popupObj.AddComponent<TextMeshProUGUI>();
        string hexColor = ColorUtility.ToHtmlStringRGB(color);
        tmp.text = $"<color=#{hexColor}><b>{message}</b></color>";
        tmp.fontSize = 80; tmp.alignment = TextAlignmentOptions.Center;

        RectTransform rt = tmp.GetComponent<RectTransform>(); rt.anchoredPosition = new Vector2(0, 50f);
        StartCoroutine(AnimatePopup(tmp));
    }

    private IEnumerator AnimatePopup(TextMeshProUGUI tmp)
    {
        float elapsed = 0f; float duration = 1.5f;
        RectTransform rt = tmp.GetComponent<RectTransform>();
        Vector2 startPos = rt.anchoredPosition; Vector2 endPos = startPos + new Vector2(0, 150f);
        Color c = tmp.color;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (rt != null) rt.anchoredPosition = Vector2.Lerp(startPos, endPos, elapsed / duration);
            if (tmp != null) { c.a = Mathf.Lerp(1f, 0f, elapsed / duration); tmp.color = c; }
            yield return null;
        }
        if (tmp != null) Destroy(tmp.gameObject);
    }

    public void DisableEntireBattleUI()
    {
        if (battleEndPanel) battleEndPanel.SetActive(false);
        HideCardInspect(); ClearAllSlots(); CloseCardSelection(); HideHoverInfo();
        if (playerHUD) playerHUD.gameObject.SetActive(false); if (enemyHUD) enemyHUD.gameObject.SetActive(false);
        if (roundText) roundText.gameObject.SetActive(false); if (startClashButton) startClashButton.gameObject.SetActive(false);
        if (battleLogText) battleLogText.gameObject.SetActive(false);
    }

    public void HideAllCombatUI()
    {
        if (playerHUD) playerHUD.gameObject.SetActive(false); if (enemyHUD) enemyHUD.gameObject.SetActive(false);
        if (playerSlotsGroup) playerSlotsGroup.gameObject.SetActive(false); if (enemySlotsGroup) enemySlotsGroup.gameObject.SetActive(false);
        if (startClashButton) startClashButton.gameObject.SetActive(false); if (roundText) roundText.gameObject.SetActive(false);
        if (handContainer) handContainer.gameObject.SetActive(false);
        ClearAllSlots(); HideHoverInfo();
    }

    public void ShowCombatUI()
    {
        if (playerHUD) playerHUD.gameObject.SetActive(true); if (enemyHUD) enemyHUD.gameObject.SetActive(true);
        if (playerSlotsGroup) playerSlotsGroup.gameObject.SetActive(true); if (enemySlotsGroup) enemySlotsGroup.gameObject.SetActive(true);
        if (startClashButton) startClashButton.gameObject.SetActive(true); if (roundText) roundText.gameObject.SetActive(true);
        if (battleLogText) battleLogText.gameObject.SetActive(true);
    }

    private void OnStateChanged(BattleManager.BattleState state)
    {
        if (startClashButton) startClashButton.interactable = (state == BattleManager.BattleState.AssignPhase);
        if (state == BattleManager.BattleState.ClashResolution) { if (playerSlotsGroup) playerSlotsGroup.gameObject.SetActive(false); if (enemySlotsGroup) enemySlotsGroup.gameObject.SetActive(false); }
        else if (state == BattleManager.BattleState.AssignPhase || state == BattleManager.BattleState.Setup) { if (playerSlotsGroup) playerSlotsGroup.gameObject.SetActive(true); if (enemySlotsGroup) enemySlotsGroup.gameObject.SetActive(true); }

        if (state == BattleManager.BattleState.BattleEnd) HideAllCombatUI();
    }

    private void OnRoundStart() { if (roundText) roundText.text = $"第 {_bm.currentRound} 幕"; CloseCardSelection(); }

    // ★ 戰鬥結束：顯示結算面板與勝負文字
    private void OnBattleEnded(bool playerWins)
    {
        if (battleEndPanel) battleEndPanel.SetActive(true);
        if (battleEndText) battleEndText.text = playerWins ? "勝利！" : "失敗...";
    }

    public void OpenCardSelection(SpeedDiceSlot slot) { if (handContainer == null || cardButtonPrefab == null) return; handContainer.gameObject.SetActive(true); foreach (Transform child in handContainer) Destroy(child.gameObject); foreach (var card in _bm.Player.Hand) { var btn = Instantiate(cardButtonPrefab, handContainer); var cardUI = btn.GetComponent<CardUI>(); if (cardUI != null) cardUI.Setup(card, slot); } }
    public void CloseCardSelection() { if (handContainer) handContainer.gameObject.SetActive(false); }
    public void ClearAllSlots() { if (playerSlotsGroup) foreach (Transform child in playerSlotsGroup) Destroy(child.gameObject); if (enemySlotsGroup) foreach (Transform child in enemySlotsGroup) Destroy(child.gameObject); }
    public SpeedDiceSlot CreateSlot(BattleUnit unit, int speed, bool isPlayer) { RectTransform parentGroup = isPlayer ? playerSlotsGroup : enemySlotsGroup; GameObject slotObj = Instantiate(speedDiceSlotPrefab, parentGroup, false); SpeedDiceSlot slot = slotObj.GetComponent<SpeedDiceSlot>(); if (slot != null) slot.Setup(unit, speed, isPlayer); return slot; }

    // ── 右鍵詳細大圖（固定置中）────────────────────────────
    public void ShowCardInspect(CardData card)
    {
        if (cardInspectPanel == null) return;

        // ★ 先啟用 BG_Overlay（黑底擋住其他點擊，並提供關閉入口）
        if (bgOverlay) bgOverlay.SetActive(true);

        var layout = cardInspectPanel.GetComponent<LayoutElement>();
        if (layout != null) layout.ignoreLayout = true;

        RectTransform rt = cardInspectPanel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        cardInspectPanel.SetActive(true);
        cardInspectPanel.transform.SetAsLastSibling();

        if (inspectNameText) inspectNameText.text = card.cardName;
        if (inspectArtwork != null) { if (card.artwork != null) { inspectArtwork.sprite = card.artwork; inspectArtwork.gameObject.SetActive(true); } else { inspectArtwork.gameObject.SetActive(false); } }
        string details = (card.description ?? "") + "\n\n";
        foreach (var dice in card.diceList) details += $"<color=#FFD700>[{dice.GetTypeName()}]</color> {dice.minVal} ~ {dice.maxVal}\n";
        if (inspectDescText) inspectDescText.text = details;
    }

    public void HideCardInspect()
    {
        if (cardInspectPanel) cardInspectPanel.SetActive(false);
        if (bgOverlay) bgOverlay.SetActive(false);  // ★ 同時關閉黑底
    }

    // ── 懸浮提示（固定顯示在玩家/敵人槽位區上方）──────────
    public void ShowHoverInfo(CardData card, RectTransform slotRect, bool isPlayerSlot)
    {
        GameObject tooltip = isPlayerSlot ? _playerTooltip : _enemyTooltip;
        if (tooltip == null) return;

        SetTooltipContent(tooltip, card);
        tooltip.SetActive(true);
        tooltip.transform.SetAsLastSibling();
    }

    public void HideHoverInfo(bool isPlayerSlot)
    {
        if (isPlayerSlot && _playerTooltip != null) _playerTooltip.SetActive(false);
        else if (!isPlayerSlot && _enemyTooltip != null) _enemyTooltip.SetActive(false);
    }

    // ★ 保留無參數版本供其他地方呼叫（同時隱藏兩個）
    public void HideHoverInfo()
    {
        if (_playerTooltip != null) _playerTooltip.SetActive(false);
        if (_enemyTooltip != null) _enemyTooltip.SetActive(false);
    }

    private void AppendLog(string line) { _logLines.Add(line); if (_logLines.Count > maxLogLines) _logLines.RemoveAt(0); if (battleLogText) battleLogText.text = string.Join("\n", _logLines); }
}