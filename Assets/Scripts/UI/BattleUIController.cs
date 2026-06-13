using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BattleUIController : MonoBehaviour
{
    [Header("UI 容器")]
    public RectTransform playerHUD;
    public RectTransform enemyHUD;
    public RectTransform playerSlotsGroup;
    public RectTransform enemySlotsGroup;

    [Header("動態位置偏移設定")]
    public Vector3 hpBottomOffset = new Vector3(0, -0.8f, 0);
    public float slotsForwardDistance = 1.3f;
    public Vector3 slotsVerticalOffset = new Vector3(0, 0.6f, 0);

    [Header("狀態 UI (綁定 EnemyHUD 模板)")]
    public TextMeshProUGUI playerHPText; public Slider playerHPSlider; public TextMeshProUGUI playerStaggerText; public Slider playerStaggerSlider; public TextMeshProUGUI playerBuffText;
    public TextMeshProUGUI enemyHPText; public Slider enemyHPSlider; public TextMeshProUGUI enemyStaggerText; public Slider enemyStaggerSlider; public TextMeshProUGUI enemyBuffText;

    [Header("預製物與面板")]
    public GameObject speedDiceSlotPrefab;
    public RectTransform handContainer;
    public GameObject cardButtonPrefab;
    public TextMeshProUGUI roundText;
    public Button startClashButton;
    public TextMeshProUGUI battleLogText;
    public int maxLogLines = 8;
    public GameObject battleEndPanel;
    public TextMeshProUGUI battleEndText;

    [Header("卡片檢視面板 (右鍵詳細)")]
    public GameObject cardInspectPanel;
    public GameObject bgOverlay;
    public Image inspectArtwork;
    public TextMeshProUGUI inspectNameText;
    public TextMeshProUGUI inspectDescText;

    [Header("懸停提示面板")]
    public GameObject playerTooltipPanel;
    public GameObject enemyTooltipPanel;

    private List<string> _logLines = new List<string>();
    private BattleManager _bm;
    private Camera _mainCamera;

    private Dictionary<BattleUnit, RectTransform> _enemyHUDs = new Dictionary<BattleUnit, RectTransform>();
    private Dictionary<BattleUnit, RectTransform> _enemySlotGroups = new Dictionary<BattleUnit, RectTransform>();

    private void Start()
    {
        _bm = BattleManager.Instance;
        _mainCamera = Camera.main;

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

        if (playerTooltipPanel) playerTooltipPanel.SetActive(false);
        if (enemyTooltipPanel) enemyTooltipPanel.SetActive(false);
    }

    private void InitEnemyUIs()
    {
        foreach (var kvp in _enemyHUDs) if (kvp.Value != null && kvp.Value != enemyHUD) Destroy(kvp.Value.gameObject);
        foreach (var kvp in _enemySlotGroups) if (kvp.Value != null && kvp.Value != enemySlotsGroup) Destroy(kvp.Value.gameObject);

        _enemyHUDs.Clear();
        _enemySlotGroups.Clear();

        if (_bm.Enemies == null || _bm.Enemies.Count == 0) return;

        if (_bm.Enemies[0] != null)
        {
            _enemyHUDs[_bm.Enemies[0]] = enemyHUD;
            _enemySlotGroups[_bm.Enemies[0]] = enemySlotsGroup;
            if (enemyHUD) enemyHUD.gameObject.SetActive(true);
            if (enemySlotsGroup) enemySlotsGroup.gameObject.SetActive(true);
        }

        for (int i = 1; i < _bm.Enemies.Count; i++)
        {
            if (_bm.Enemies[i] == null) continue;

            if (enemyHUD != null)
            {
                RectTransform newHUD = Instantiate(enemyHUD, enemyHUD.parent);
                newHUD.gameObject.SetActive(true);
                _enemyHUDs[_bm.Enemies[i]] = newHUD;
            }

            if (enemySlotsGroup != null)
            {
                RectTransform newSlots = Instantiate(enemySlotsGroup, enemySlotsGroup.parent);
                newSlots.gameObject.SetActive(true);
                _enemySlotGroups[_bm.Enemies[i]] = newSlots;
            }
        }
    }

    private void Update()
    {
        if (_bm == null || _mainCamera == null || _bm.CurrentState == BattleManager.BattleState.BattleEnd) return;

        if (_bm.Player != null && playerHUD != null)
        {
            Vector3 s = _mainCamera.WorldToScreenPoint(_bm.Player.transform.position + hpBottomOffset); s.z = 0; playerHUD.position = s;
            if (playerSlotsGroup != null) { s = _mainCamera.WorldToScreenPoint(_bm.Player.transform.position + (_bm.Player.transform.forward * slotsForwardDistance) + slotsVerticalOffset); s.z = 0; playerSlotsGroup.position = s; }
            RefreshPlayerHUD(_bm.Player);
        }

        for (int i = 0; i < _bm.Enemies.Count; i++)
        {
            var enemy = _bm.Enemies[i];
            if (enemy == null) continue;

            if (enemy.CurrentHP <= 0)
            {
                if (_enemyHUDs.ContainsKey(enemy) && _enemyHUDs[enemy] != null) _enemyHUDs[enemy].gameObject.SetActive(false);
                if (_enemySlotGroups.ContainsKey(enemy) && _enemySlotGroups[enemy] != null) _enemySlotGroups[enemy].gameObject.SetActive(false);
                continue;
            }

            if (_enemyHUDs.ContainsKey(enemy) && _enemyHUDs[enemy] != null)
            {
                _enemyHUDs[enemy].gameObject.SetActive(true);
                Vector3 s = _mainCamera.WorldToScreenPoint(enemy.transform.position + hpBottomOffset); s.z = 0; _enemyHUDs[enemy].position = s;
                RefreshEnemyHUD(enemy, _enemyHUDs[enemy]);
            }
            if (_enemySlotGroups.ContainsKey(enemy) && _enemySlotGroups[enemy] != null)
            {
                _enemySlotGroups[enemy].gameObject.SetActive(true);
                Vector3 s = _mainCamera.WorldToScreenPoint(enemy.transform.position + (enemy.transform.forward * slotsForwardDistance) + slotsVerticalOffset); s.z = 0; _enemySlotGroups[enemy].position = s;
            }
        }
    }

    private void RefreshPlayerHUD(BattleUnit unit)
    {
        if (playerHPText) playerHPText.text = $"HP: {unit.CurrentHP}/{unit.maxHP}";
        if (playerHPSlider) playerHPSlider.value = (float)unit.CurrentHP / unit.maxHP;
        if (playerStaggerText) playerStaggerText.text = $"Stagger: {unit.CurrentStagger}/{unit.maxStagger}";
        if (playerStaggerSlider) playerStaggerSlider.value = (float)unit.CurrentStagger / unit.maxStagger;
        if (playerBuffText) playerBuffText.text = unit.activeBuffs.Count > 0 ? string.Join("\n", unit.activeBuffs) : "Buff";
    }

    private TextMeshProUGUI GetEnemyText(RectTransform hud, TextMeshProUGUI template)
    {
        if (template == null) return null;
        if (hud == enemyHUD) return template;
        Transform t = FindChildRecursive(hud, template.gameObject.name);
        return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
    }

    private Slider GetEnemySlider(RectTransform hud, Slider template)
    {
        if (template == null) return null;
        if (hud == enemyHUD) return template;
        Transform t = FindChildRecursive(hud, template.gameObject.name);
        return t != null ? t.GetComponent<Slider>() : null;
    }

    private void RefreshEnemyHUD(BattleUnit unit, RectTransform hud)
    {
        TextMeshProUGUI hpText = GetEnemyText(hud, enemyHPText);
        Slider hpSlider = GetEnemySlider(hud, enemyHPSlider);
        TextMeshProUGUI stagText = GetEnemyText(hud, enemyStaggerText);
        Slider stagSlider = GetEnemySlider(hud, enemyStaggerSlider);
        TextMeshProUGUI buffText = GetEnemyText(hud, enemyBuffText);

        if (hpText) hpText.text = $"HP: {unit.CurrentHP}/{unit.maxHP}";
        if (hpSlider) hpSlider.value = (float)unit.CurrentHP / unit.maxHP;
        if (stagText) stagText.text = $"Stagger: {unit.CurrentStagger}/{unit.maxStagger}";
        if (stagSlider) stagSlider.value = (float)unit.CurrentStagger / unit.maxStagger;
        if (buffText) buffText.text = unit.activeBuffs.Count > 0 ? string.Join("\n", unit.activeBuffs) : "Buff";
    }

    public void ShowFloatingText(BattleUnit unit, string message, Color color)
    {
        RectTransform parentHUD = null;
        if (unit == _bm.Player) parentHUD = playerHUD;
        else if (_enemyHUDs.ContainsKey(unit)) parentHUD = _enemyHUDs[unit];

        if (parentHUD == null || !parentHUD.gameObject.activeInHierarchy) return;

        GameObject popupObj = new GameObject("FloatingTextPopup");
        popupObj.transform.SetParent(parentHUD, false);
        TextMeshProUGUI tmp = popupObj.AddComponent<TextMeshProUGUI>();
        string hexColor = ColorUtility.ToHtmlStringRGB(color);
        tmp.text = $"<color=#{hexColor}><b>{message}</b></color>";
        tmp.fontSize = 80;
        tmp.alignment = TextAlignmentOptions.Center;

        RectTransform rt = tmp.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(0, 50f);
        StartCoroutine(AnimatePopup(tmp));
    }

    private IEnumerator AnimatePopup(TextMeshProUGUI tmp)
    {
        float elapsed = 0f; float duration = 1.5f;
        RectTransform rt = tmp.GetComponent<RectTransform>();
        Vector2 startPos = rt.anchoredPosition;
        Vector2 endPos = startPos + new Vector2(0, 150f);
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
        HideCardInspect();
        ClearAllSlots();
        CloseCardSelection();
        HideHoverInfo();

        if (playerHUD) playerHUD.gameObject.SetActive(false);
        foreach (var hud in _enemyHUDs.Values) if (hud) hud.gameObject.SetActive(false);

        if (roundText) roundText.gameObject.SetActive(false);
        if (startClashButton) startClashButton.gameObject.SetActive(false);
        if (battleLogText) battleLogText.gameObject.SetActive(false);
    }

    public void HideAllCombatUI()
    {
        if (playerHUD) playerHUD.gameObject.SetActive(false);
        foreach (var hud in _enemyHUDs.Values) if (hud) hud.gameObject.SetActive(false);

        if (playerSlotsGroup) playerSlotsGroup.gameObject.SetActive(false);
        foreach (var grp in _enemySlotGroups.Values) if (grp) grp.gameObject.SetActive(false);

        if (startClashButton) startClashButton.gameObject.SetActive(false);
        if (roundText) roundText.gameObject.SetActive(false);
        if (handContainer) handContainer.gameObject.SetActive(false);

        ClearAllSlots();
        HideHoverInfo();
    }

    public void ShowCombatUI()
    {
        if (playerHUD) playerHUD.gameObject.SetActive(true);
        foreach (var hud in _enemyHUDs.Values) if (hud) hud.gameObject.SetActive(true);

        if (playerSlotsGroup) playerSlotsGroup.gameObject.SetActive(true);
        foreach (var grp in _enemySlotGroups.Values) if (grp) grp.gameObject.SetActive(true);

        if (startClashButton) startClashButton.gameObject.SetActive(true);
        if (roundText) roundText.gameObject.SetActive(true);
        if (battleLogText) battleLogText.gameObject.SetActive(true);
    }

    private void OnStateChanged(BattleManager.BattleState state)
    {
        if (state == BattleManager.BattleState.Setup)
        {
            InitEnemyUIs();
        }

        if (startClashButton) startClashButton.interactable = (state == BattleManager.BattleState.AssignPhase);

        if (state == BattleManager.BattleState.ClashResolution)
        {
            if (playerSlotsGroup) playerSlotsGroup.gameObject.SetActive(false);
            foreach (var grp in _enemySlotGroups.Values) if (grp) grp.gameObject.SetActive(false);
        }
        else if (state == BattleManager.BattleState.AssignPhase || state == BattleManager.BattleState.Setup)
        {
            if (playerSlotsGroup) playerSlotsGroup.gameObject.SetActive(true);
            foreach (var grp in _enemySlotGroups.Values) if (grp) grp.gameObject.SetActive(true);
        }

        if (state == BattleManager.BattleState.BattleEnd) HideAllCombatUI();
    }

    private void OnRoundStart()
    {
        if (roundText) roundText.text = $"Round {_bm.currentRound}";
        CloseCardSelection();
    }

    private void OnBattleEnded(bool playerWins)
    {
        if (battleEndPanel) battleEndPanel.SetActive(true);
        if (battleEndText) battleEndText.text = playerWins ? "Victory!" : "Defeat...";
    }

    public void OpenCardSelection(SpeedDiceSlot slot)
    {
        if (handContainer == null || cardButtonPrefab == null) return;
        handContainer.gameObject.SetActive(true);
        foreach (Transform child in handContainer) Destroy(child.gameObject);
        foreach (var card in _bm.Player.Hand)
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
        foreach (var grp in _enemySlotGroups.Values) if (grp) foreach (Transform child in grp) Destroy(child.gameObject);
    }

    public SpeedDiceSlot CreateSlot(BattleUnit unit, int speed, bool isPlayer)
    {
        RectTransform parentGroup = playerSlotsGroup;
        if (!isPlayer)
        {
            if (_enemySlotGroups.ContainsKey(unit)) parentGroup = _enemySlotGroups[unit];
            else parentGroup = enemySlotsGroup;
        }

        GameObject slotObj = Instantiate(speedDiceSlotPrefab, parentGroup, false);
        SpeedDiceSlot slot = slotObj.GetComponent<SpeedDiceSlot>();
        if (slot != null) slot.Setup(unit, speed, isPlayer);
        return slot;
    }

    // ────────────────────────────────────────────────────────
    // 顯示文字產生器 (純文字風格，無表情符號)
    // ────────────────────────────────────────────────────────
    private string GenerateLibraryStyleDiceText(CardData card)
    {
        string details = "";

        // 描述區塊
        if (!string.IsNullOrEmpty(card.description))
        {
            details += $"<size=90%><color=#DDDDDD>{card.description}</color></size>\n";
        }

        // 加上分隔線讓描述與骰子區分開來
        if (card.diceList.Count > 0)
        {
            details += "<color=#555555>─────────────────</color>\n";
        }

        // 骰子數值區塊
        foreach (var d in card.diceList)
        {
            string colorHex = "#FFFFFF";
            string typeTag = "";

            switch (d.type)
            {
                case DiceType.MeleeAttack: colorHex = "#E84141"; typeTag = "[近戰]"; break;
                case DiceType.RangedAttack: colorHex = "#E84141"; typeTag = "[遠程]"; break;
                case DiceType.Block: colorHex = "#417BE8"; typeTag = "[格擋]"; break;
                case DiceType.Evade: colorHex = "#41E86E"; typeTag = "[閃避]"; break;
                case DiceType.Heal: colorHex = "#E8D341"; typeTag = "[回復]"; break;
            }

            details += $"<color={colorHex}><b>{typeTag}</b> <space=1em>{d.minVal} - {d.maxVal}</color>\n";
        }
        return details.TrimEnd();
    }

    public void ShowCardInspect(CardData card)
    {
        if (cardInspectPanel == null) return;

        if (bgOverlay) bgOverlay.SetActive(true);
        cardInspectPanel.SetActive(true);

        if (inspectNameText) inspectNameText.text = $"<size=120%><b>{card.cardName}</b></size>";

        if (inspectArtwork != null)
        {
            if (card.artwork != null)
            {
                inspectArtwork.sprite = card.artwork;
                inspectArtwork.gameObject.SetActive(true);
            }
            else
            {
                inspectArtwork.gameObject.SetActive(false);
            }
        }

        if (inspectDescText)
            inspectDescText.text = GenerateLibraryStyleDiceText(card);
    }

    public void HideCardInspect()
    {
        if (cardInspectPanel) cardInspectPanel.SetActive(false);
        if (bgOverlay) bgOverlay.SetActive(false);
    }

    public void ShowHoverInfo(CardData card, RectTransform slotRect, bool isPlayerSlot)
    {
        GameObject targetPanel = isPlayerSlot ? playerTooltipPanel : enemyTooltipPanel;
        if (targetPanel == null) return;

        UpdateTooltipData(targetPanel, card);
        targetPanel.SetActive(true);
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName) return child;
        }
        foreach (Transform child in parent)
        {
            Transform found = FindChildRecursive(child, childName);
            if (found != null) return found;
        }
        return null;
    }

    private void UpdateTooltipData(GameObject panel, CardData card)
    {
        // 1. 抓取左半邊的卡圖 (Artwork)
        Image artImg = null;
        var artTransform = FindChildRecursive(panel.transform, "Artwork");
        if (artTransform != null) artImg = artTransform.GetComponent<Image>();

        if (artImg == null)
        {
            Image[] allImages = panel.GetComponentsInChildren<Image>(true);
            foreach (var img in allImages)
            {
                if (img.gameObject != panel) { artImg = img; break; }
            }
        }

        if (artImg != null)
        {
            if (card.artwork != null)
            {
                artImg.sprite = card.artwork;
                artImg.gameObject.SetActive(true);
            }
            else
            {
                artImg.gameObject.SetActive(false);
            }
        }

        // 2. ★ 智慧抓取文字元件 (解決名稱不符導致全擠在一起的問題)
        TextMeshProUGUI[] allTexts = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
        TextMeshProUGUI nameTmp = null;
        TextMeshProUGUI contentTmp = null;

        // 2-a. 先嘗試模糊比對名稱 (無視大小寫與後綴)
        foreach (var t in allTexts)
        {
            string lowerName = t.name.ToLower();
            if (lowerName.Contains("name")) nameTmp = t;
            else if (lowerName.Contains("content") || lowerName.Contains("desc") || lowerName.Contains("info")) contentTmp = t;
        }

        // 2-b. 如果名稱比對失敗，依照 Unity 內的層級順序直接分配
        if (nameTmp == null && allTexts.Length > 0) nameTmp = allTexts[0];
        if (contentTmp == null && allTexts.Length > 1)
        {
            for (int i = 1; i < allTexts.Length; i++)
            {
                // 確保不跟卡名抓到同一個
                if (allTexts[i] != nameTmp) { contentTmp = allTexts[i]; break; }
            }
        }

        // 3. 填入資料
        if (nameTmp != null && contentTmp != null)
        {
            // 有兩個獨立的文字框，完美分離 (左圖右文成功！)
            nameTmp.text = $"<size=120%><color=#FFFFFF><b>{card.cardName}</b></color></size>";
            contentTmp.text = GenerateLibraryStyleDiceText(card);
        }
        else if (nameTmp != null)
        {
            // 極端防呆：如果你的 UI 真的只有放一個文字框，才把它們合併顯示
            string info = $"<size=120%><color=#FFFFFF><b>{card.cardName}</b></color></size>\n";
            info += GenerateLibraryStyleDiceText(card);
            nameTmp.text = info;
        }
    }

    public void HideHoverInfo(bool isPlayerSlot)
    {
        if (isPlayerSlot && playerTooltipPanel != null) playerTooltipPanel.SetActive(false);
        else if (!isPlayerSlot && enemyTooltipPanel != null) enemyTooltipPanel.SetActive(false);
    }

    public void HideHoverInfo()
    {
        if (playerTooltipPanel != null) playerTooltipPanel.SetActive(false);
        if (enemyTooltipPanel != null) enemyTooltipPanel.SetActive(false);
    }

    private void AppendLog(string line)
    {
        _logLines.Add(line);
        if (_logLines.Count > maxLogLines) _logLines.RemoveAt(0);
        if (battleLogText) battleLogText.text = string.Join("\n", _logLines);
    }
}