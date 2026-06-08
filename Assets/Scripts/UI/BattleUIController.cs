using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ★ 新增一個輕量的類別，用來掛在敵人血條或面板上，作為「接受攻擊」的感應區
public class EnemyTargetArea : MonoBehaviour
{
    public BattleUnit unit;
}

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
    public GameObject bgOverlay;
    public Image inspectArtwork; public TextMeshProUGUI inspectNameText; public TextMeshProUGUI inspectDescText;

    [Header("懸浮提示 (固定區域)")]
    public RectTransform playerTooltipAnchor;
    public RectTransform enemyTooltipAnchor;

    private List<string> _logLines = new();
    private BattleManager _bm; private Camera _mainCamera;

    private GameObject _playerTooltip;
    private GameObject _enemyTooltip;

    private class EnemyUIData
    {
        public BattleUnit unit;
        public RectTransform hud;
        public RectTransform slotsGroup;
        public TextMeshProUGUI hpText;
        public Slider hpSlider;
        public TextMeshProUGUI staggerText;
        public Slider staggerSlider;
        public TextMeshProUGUI buffText;
    }
    private List<EnemyUIData> _enemyUIs = new List<EnemyUIData>();

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

        if (bgOverlay)
        {
            bgOverlay.SetActive(false);
            Button overlayBtn = bgOverlay.GetComponent<Button>();
            if (overlayBtn == null) overlayBtn = bgOverlay.AddComponent<Button>();
            overlayBtn.transition = Selectable.Transition.None;
            overlayBtn.onClick.AddListener(HideCardInspect);
        }

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

        _playerTooltip = CreateTooltipObject("PlayerTooltip");
        _enemyTooltip = CreateTooltipObject("EnemyTooltip");
    }

    private void InitEnemyUIs()
    {
        foreach (var eUI in _enemyUIs)
        {
            if (eUI.hud != null && eUI.hud != enemyHUD) Destroy(eUI.hud.gameObject);
            if (eUI.slotsGroup != null && eUI.slotsGroup != enemySlotsGroup) Destroy(eUI.slotsGroup.gameObject);
        }
        _enemyUIs.Clear();

        for (int i = 0; i < _bm.Enemies.Count; i++)
        {
            BattleUnit enemy = _bm.Enemies[i];
            EnemyUIData data = new EnemyUIData();
            data.unit = enemy;

            if (i == 0)
            {
                data.hud = enemyHUD; data.slotsGroup = enemySlotsGroup;
                data.hpText = enemyHPText; data.hpSlider = enemyHPSlider;
                data.staggerText = enemyStaggerText; data.staggerSlider = enemyStaggerSlider;
                data.buffText = enemyBuffText;
            }
            else
            {
                data.hud = Instantiate(enemyHUD, enemyHUD.parent);
                data.slotsGroup = Instantiate(enemySlotsGroup, enemySlotsGroup.parent);
                data.hpText = GetEquivalentComponent(enemyHUD, data.hud, enemyHPText);
                data.hpSlider = GetEquivalentComponent(enemyHUD, data.hud, enemyHPSlider);
                data.staggerText = GetEquivalentComponent(enemyHUD, data.hud, enemyStaggerText);
                data.staggerSlider = GetEquivalentComponent(enemyHUD, data.hud, enemyStaggerSlider);
                data.buffText = GetEquivalentComponent(enemyHUD, data.hud, enemyBuffText);
            }

            // ★ 動態幫敵人的 HUD 加上 TargetArea，這樣玩家把卡片丟到血條上就能攻擊他
            EnemyTargetArea targetArea = data.hud.gameObject.GetComponent<EnemyTargetArea>();
            if (targetArea == null) targetArea = data.hud.gameObject.AddComponent<EnemyTargetArea>();
            targetArea.unit = enemy;

            data.hud.gameObject.SetActive(true); data.slotsGroup.gameObject.SetActive(true);
            _enemyUIs.Add(data);
        }
    }

    private T GetEquivalentComponent<T>(Transform originalRoot, Transform clonedRoot, T originalComponent) where T : Component
    {
        if (originalComponent == null) return null;
        string path = ""; Transform current = originalComponent.transform;
        while (current != null && current != originalRoot)
        {
            path = (path == "") ? current.name : current.name + "/" + path;
            current = current.parent;
        }
        Transform clonedObj = clonedRoot.Find(path);
        return clonedObj != null ? clonedObj.GetComponent<T>() : null;
    }

    private GameObject CreateTooltipObject(string name)
    {
        var go = new GameObject(name); go.transform.SetParent(this.transform, false);
        var bg = go.AddComponent<Image>(); bg.color = new Color(0.08f, 0.08f, 0.08f, 0.92f);
        var textObj = new GameObject("Text"); textObj.transform.SetParent(go.transform, false);
        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 22; tmp.lineSpacing = 8; tmp.alignment = TextAlignmentOptions.Left;
        var textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero; textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(14, 10); textRt.offsetMax = new Vector2(-14, -10);
        var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(240, 0);
        var csf = go.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        go.SetActive(false); return go;
    }

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
            RefreshPlayerUI(_bm.Player);
        }

        foreach (var eUI in _enemyUIs)
        {
            if (eUI.unit != null && eUI.unit.gameObject.activeInHierarchy && eUI.unit.CurrentHP > 0)
            {
                Vector3 s = _mainCamera.WorldToScreenPoint(eUI.unit.transform.position + hpBottomOffset); s.z = 0; eUI.hud.position = s;
                s = _mainCamera.WorldToScreenPoint(eUI.unit.transform.position + (eUI.unit.transform.forward * slotsForwardDistance) + slotsVerticalOffset); s.z = 0; eUI.slotsGroup.position = s;

                if (eUI.hpText) eUI.hpText.text = $"HP: {eUI.unit.CurrentHP}/{eUI.unit.maxHP}";
                if (eUI.hpSlider) eUI.hpSlider.value = (float)eUI.unit.CurrentHP / eUI.unit.maxHP;
                if (eUI.staggerText) eUI.staggerText.text = $"混亂: {eUI.unit.CurrentStagger}/{eUI.unit.maxStagger}";
                if (eUI.staggerSlider) eUI.staggerSlider.value = (float)eUI.unit.CurrentStagger / eUI.unit.maxStagger;
                if (eUI.buffText) eUI.buffText.text = eUI.unit.activeBuffs.Count > 0 ? string.Join("\n", eUI.unit.activeBuffs) : "Buff";
            }
            else
            {
                if (eUI.hud) eUI.hud.gameObject.SetActive(false);
                if (eUI.slotsGroup) eUI.slotsGroup.gameObject.SetActive(false);
            }
        }

        UpdateTooltipPosition(_playerTooltip, playerSlotsGroup, true);
        RectTransform activeEnemyGroup = null;
        foreach (var eUI in _enemyUIs) { if (eUI.slotsGroup != null && eUI.slotsGroup.gameObject.activeInHierarchy) { activeEnemyGroup = eUI.slotsGroup; break; } }
        UpdateTooltipPosition(_enemyTooltip, activeEnemyGroup, false);
    }

    private void UpdateTooltipPosition(GameObject tooltip, RectTransform slotsGroup, bool isPlayer)
    {
        if (tooltip == null || !tooltip.activeSelf) return;
        RectTransform rt = tooltip.GetComponent<RectTransform>();

        if (isPlayer && playerTooltipAnchor != null)
        {
            if (rt.parent != playerTooltipAnchor) rt.SetParent(playerTooltipAnchor, false);
            rt.pivot = new Vector2(0f, 1f); rt.anchoredPosition = Vector2.zero;
        }
        else if (!isPlayer && enemyTooltipAnchor != null)
        {
            if (rt.parent != enemyTooltipAnchor) rt.SetParent(enemyTooltipAnchor, false);
            rt.pivot = new Vector2(1f, 1f); rt.anchoredPosition = Vector2.zero;
        }
        else if (slotsGroup != null)
        {
            Vector3 pos = slotsGroup.position; pos.y += 80f; rt.position = pos;
        }
    }

    private void RefreshPlayerUI(BattleUnit unit)
    {
        if (playerHPText) playerHPText.text = $"HP: {unit.CurrentHP}/{unit.maxHP}"; if (playerHPSlider) playerHPSlider.value = (float)unit.CurrentHP / unit.maxHP;
        if (playerStaggerText) playerStaggerText.text = $"混亂: {unit.CurrentStagger}/{unit.maxStagger}"; if (playerStaggerSlider) playerStaggerSlider.value = (float)unit.CurrentStagger / unit.maxStagger;
        if (playerBuffText) playerBuffText.text = unit.activeBuffs.Count > 0 ? string.Join("\n", unit.activeBuffs) : "Buff";
    }

    public void ShowFloatingText(BattleUnit unit, string message, Color color)
    {
        RectTransform parentHUD = playerHUD;
        if (unit != _bm.Player)
        {
            var eUI = _enemyUIs.Find(e => e.unit == unit);
            if (eUI != null) parentHUD = eUI.hud;
        }
        if (parentHUD == null || !parentHUD.gameObject.activeInHierarchy) return;

        GameObject popupObj = new GameObject("FloatingTextPopup"); popupObj.transform.SetParent(parentHUD, false);
        TextMeshProUGUI tmp = popupObj.AddComponent<TextMeshProUGUI>();
        string hexColor = ColorUtility.ToHtmlStringRGB(color);
        tmp.text = $"<color=#{hexColor}><b>{message}</b></color>"; tmp.fontSize = 80; tmp.alignment = TextAlignmentOptions.Center;
        RectTransform rt = tmp.GetComponent<RectTransform>(); rt.anchoredPosition = new Vector2(0, 50f);
        StartCoroutine(AnimatePopup(tmp));
    }

    private IEnumerator AnimatePopup(TextMeshProUGUI tmp)
    {
        float elapsed = 0f; float duration = 1.5f; RectTransform rt = tmp.GetComponent<RectTransform>();
        Vector2 startPos = rt.anchoredPosition; Vector2 endPos = startPos + new Vector2(0, 150f); Color c = tmp.color;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; if (rt != null) rt.anchoredPosition = Vector2.Lerp(startPos, endPos, elapsed / duration);
            if (tmp != null) { c.a = Mathf.Lerp(1f, 0f, elapsed / duration); tmp.color = c; }
            yield return null;
        }
        if (tmp != null) Destroy(tmp.gameObject);
    }

    public void DisableEntireBattleUI()
    {
        if (battleEndPanel) battleEndPanel.SetActive(false);
        HideCardInspect(); ClearAllSlots(); CloseCardSelection(); HideHoverInfo();
        if (playerHUD) playerHUD.gameObject.SetActive(false);
        foreach (var eUI in _enemyUIs) { if (eUI.hud) eUI.hud.gameObject.SetActive(false); }
        if (enemyHUD) enemyHUD.gameObject.SetActive(false);
        if (roundText) roundText.gameObject.SetActive(false); if (startClashButton) startClashButton.gameObject.SetActive(false);
        if (battleLogText) battleLogText.gameObject.SetActive(false);
    }

    public void HideAllCombatUI()
    {
        if (playerHUD) playerHUD.gameObject.SetActive(false);
        if (playerSlotsGroup) playerSlotsGroup.gameObject.SetActive(false);
        foreach (var eUI in _enemyUIs)
        {
            if (eUI.hud) eUI.hud.gameObject.SetActive(false);
            if (eUI.slotsGroup) eUI.slotsGroup.gameObject.SetActive(false);
        }
        if (startClashButton) startClashButton.gameObject.SetActive(false); if (roundText) roundText.gameObject.SetActive(false);
        if (handContainer) handContainer.gameObject.SetActive(false);
        ClearAllSlots(); HideHoverInfo();
    }

    public void ShowCombatUI()
    {
        if (playerHUD) playerHUD.gameObject.SetActive(true);
        if (playerSlotsGroup) playerSlotsGroup.gameObject.SetActive(true);
        foreach (var eUI in _enemyUIs)
        {
            if (eUI.hud) eUI.hud.gameObject.SetActive(true);
            if (eUI.slotsGroup) eUI.slotsGroup.gameObject.SetActive(true);
        }
        if (startClashButton) startClashButton.gameObject.SetActive(true); if (roundText) roundText.gameObject.SetActive(true);
        if (battleLogText) battleLogText.gameObject.SetActive(true);
    }

    private void OnStateChanged(BattleManager.BattleState state)
    {
        if (state == BattleManager.BattleState.Setup) InitEnemyUIs();

        if (startClashButton) startClashButton.interactable = (state == BattleManager.BattleState.AssignPhase);

        if (state == BattleManager.BattleState.ClashResolution)
        {
            if (playerSlotsGroup) playerSlotsGroup.gameObject.SetActive(false);
            foreach (var eUI in _enemyUIs) if (eUI.slotsGroup) eUI.slotsGroup.gameObject.SetActive(false);
        }
        else if (state == BattleManager.BattleState.AssignPhase || state == BattleManager.BattleState.Setup)
        {
            if (playerSlotsGroup) playerSlotsGroup.gameObject.SetActive(true);
            foreach (var eUI in _enemyUIs) if (eUI.slotsGroup) eUI.slotsGroup.gameObject.SetActive(true);
        }

        if (state == BattleManager.BattleState.BattleEnd) HideAllCombatUI();
    }

    private void OnRoundStart() { if (roundText) roundText.text = $"第 {_bm.currentRound} 幕"; CloseCardSelection(); }

    private void OnBattleEnded(bool playerWins)
    {
        if (battleEndPanel) battleEndPanel.SetActive(true);
        if (battleEndText) battleEndText.text = playerWins ? "勝利！" : "失敗...";
    }

    // ★ 關鍵修復：每次開啟手牌介面時，先過濾掉已經裝填在行動槽的卡片！
    public void OpenCardSelection(SpeedDiceSlot slot)
    {
        if (handContainer == null || cardButtonPrefab == null) return;
        handContainer.gameObject.SetActive(true);
        foreach (Transform child in handContainer) Destroy(child.gameObject);

        // 複製一份手牌清單
        List<CardData> availableCards = new List<CardData>(_bm.Player.Hand);

        // 檢查玩家每個行動槽，把已經裝填的卡從清單裡扣除 (只會扣一次，確保相同卡片不受影響)
        foreach (var s in _bm.GetPlayerSlots())
        {
            if (s != slot && s.AssignedCard != null)
            {
                availableCards.Remove(s.AssignedCard);
            }
        }

        foreach (var card in availableCards)
        {
            var btn = Instantiate(cardButtonPrefab, handContainer);
            var cardUI = btn.GetComponent<CardUI>();
            if (cardUI != null) cardUI.Setup(card, slot);
        }
    }

    public void CloseCardSelection() { if (handContainer) handContainer.gameObject.SetActive(false); }

    public void ClearAllSlots()
    {
        if (playerSlotsGroup) foreach (Transform child in playerSlotsGroup) Destroy(child.gameObject);
        foreach (var eUI in _enemyUIs) { if (eUI.slotsGroup) foreach (Transform child in eUI.slotsGroup) Destroy(child.gameObject); }
        if (enemySlotsGroup) foreach (Transform child in enemySlotsGroup) Destroy(child.gameObject);
    }

    public SpeedDiceSlot CreateSlot(BattleUnit unit, int speed, bool isPlayer)
    {
        RectTransform parentGroup = playerSlotsGroup;
        if (!isPlayer)
        {
            var eUI = _enemyUIs.Find(e => e.unit == unit);
            if (eUI != null) parentGroup = eUI.slotsGroup;
            else parentGroup = enemySlotsGroup;
        }

        GameObject slotObj = Instantiate(speedDiceSlotPrefab, parentGroup, false);
        SpeedDiceSlot slot = slotObj.GetComponent<SpeedDiceSlot>();
        if (slot != null) slot.Setup(unit, speed, isPlayer);
        return slot;
    }

    // ★ 取消代碼強制排版，完全由 Unity 介面設計控制
    public void ShowCardInspect(CardData card)
    {
        if (cardInspectPanel == null) return;

        if (bgOverlay)
        {
            bgOverlay.SetActive(true);
            bgOverlay.transform.SetAsLastSibling();
        }

        // 這裡不再使用程式強制設定錨點，你的 Unity LayoutGroup 將會完全生效！
        cardInspectPanel.SetActive(true);
        cardInspectPanel.transform.SetAsLastSibling();

        if (inspectNameText) inspectNameText.text = card.cardName;
        if (inspectArtwork != null)
        {
            inspectArtwork.sprite = card.artwork;
            inspectArtwork.gameObject.SetActive(card.artwork != null);
        }

        string details = (card.description ?? "") + "\n\n";
        foreach (var dice in card.diceList) details += $"<color=#FFD700>[{dice.GetTypeName()}]</color> {dice.minVal} ~ {dice.maxVal}\n";
        if (inspectDescText) inspectDescText.text = details;
    }

    public void HideCardInspect()
    {
        if (cardInspectPanel) cardInspectPanel.SetActive(false);
        if (bgOverlay) bgOverlay.SetActive(false);
    }

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

    public void HideHoverInfo()
    {
        if (_playerTooltip != null) _playerTooltip.SetActive(false);
        if (_enemyTooltip != null) _enemyTooltip.SetActive(false);
    }

    private void AppendLog(string line) { _logLines.Add(line); if (_logLines.Count > maxLogLines) _logLines.RemoveAt(0); if (battleLogText) battleLogText.text = string.Join("\n", _logLines); }
}