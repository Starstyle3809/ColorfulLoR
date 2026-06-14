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

    [Header("狀態 UI (純文字綁定)")]
    public TextMeshProUGUI playerHPText;
    public TextMeshProUGUI playerStaggerText;
    public TextMeshProUGUI playerBuffText;

    public TextMeshProUGUI enemyHPText;
    public TextMeshProUGUI enemyStaggerText;
    public TextMeshProUGUI enemyBuffText;

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

    // 儲存動態生成的敵人 UI 參考
    private class EnemyUI
    {
        public RectTransform hudRect;
        public RectTransform slotsGroup;
        public TextMeshProUGUI hpText;
        public TextMeshProUGUI staggerText;
        public TextMeshProUGUI buffText;
    }
    private Dictionary<BattleUnit, EnemyUI> _enemyUIs = new Dictionary<BattleUnit, EnemyUI>();

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

        // ★ 強制將手牌區的排版設為不壓縮，確保卡片尺寸固定
        if (handContainer != null)
        {
            HorizontalLayoutGroup hlg = handContainer.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                hlg.childControlWidth = false;
                hlg.childControlHeight = false;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
            }
        }
    }

    private void InitEnemyUIs()
    {
        // 清理舊的複製體
        foreach (var kvp in _enemyUIs)
        {
            if (kvp.Value.hudRect != null && kvp.Value.hudRect != enemyHUD) Destroy(kvp.Value.hudRect.gameObject);
            if (kvp.Value.slotsGroup != null && kvp.Value.slotsGroup != enemySlotsGroup) Destroy(kvp.Value.slotsGroup.gameObject);
        }
        _enemyUIs.Clear();

        if (_bm.Enemies == null || _bm.Enemies.Count == 0) return;

        // 綁定第一名敵人到原始模板
        EnemyUI firstUI = new EnemyUI();
        firstUI.hudRect = enemyHUD;
        firstUI.slotsGroup = enemySlotsGroup;
        BindEnemyTexts(firstUI, enemyHUD);
        _enemyUIs[_bm.Enemies[0]] = firstUI;

        if (enemyHUD) enemyHUD.gameObject.SetActive(true);
        if (enemySlotsGroup) enemySlotsGroup.gameObject.SetActive(true);

        // 複製給後續敵人
        for (int i = 1; i < _bm.Enemies.Count; i++)
        {
            if (_bm.Enemies[i] == null) continue;

            EnemyUI newUI = new EnemyUI();
            if (enemyHUD != null)
            {
                newUI.hudRect = Instantiate(enemyHUD, enemyHUD.parent);
                newUI.hudRect.gameObject.SetActive(true);
                BindEnemyTexts(newUI, newUI.hudRect);
            }

            if (enemySlotsGroup != null)
            {
                newUI.slotsGroup = Instantiate(enemySlotsGroup, enemySlotsGroup.parent);
                newUI.slotsGroup.gameObject.SetActive(true);
            }

            _enemyUIs[_bm.Enemies[i]] = newUI;
        }
    }

    // ★ 精準綁定敵人內部的文字元件，解決複製體抓不到組件的問題
    private void BindEnemyTexts(EnemyUI uiData, RectTransform hud)
    {
        uiData.hpText = GetEnemyText(hud, enemyHPText);
        uiData.staggerText = GetEnemyText(hud, enemyStaggerText);
        uiData.buffText = GetEnemyText(hud, enemyBuffText);
    }

    private TextMeshProUGUI GetEnemyText(RectTransform hud, TextMeshProUGUI template)
    {
        if (template == null) return null;
        if (hud == enemyHUD) return template; // 若是原本的模板就直接回傳
        Transform t = FindChildRecursive(hud, template.name); // 找名字完全一模一樣的子物件
        return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
    }

    private void Update()
    {
        if (_bm == null || _mainCamera == null || _bm.CurrentState == BattleManager.BattleState.BattleEnd) return;

        // 更新玩家
        if (_bm.Player != null && playerHUD != null)
        {
            Vector3 s = _mainCamera.WorldToScreenPoint(_bm.Player.transform.position + hpBottomOffset); s.z = 0; playerHUD.position = s;
            if (playerSlotsGroup != null) { s = _mainCamera.WorldToScreenPoint(_bm.Player.transform.position + (_bm.Player.transform.forward * slotsForwardDistance) + slotsVerticalOffset); s.z = 0; playerSlotsGroup.position = s; }

            if (playerHPText) playerHPText.text = _bm.Player.CurrentHP.ToString();
            if (playerStaggerText) playerStaggerText.text = _bm.Player.CurrentStagger.ToString();
            if (playerBuffText) playerBuffText.text = _bm.Player.activeBuffs.Count > 0 ? string.Join("\n", _bm.Player.activeBuffs) : "";
        }

        // 更新敵人
        for (int i = 0; i < _bm.Enemies.Count; i++)
        {
            var enemy = _bm.Enemies[i];
            if (enemy == null || !_enemyUIs.ContainsKey(enemy)) continue;

            EnemyUI eUI = _enemyUIs[enemy];

            if (enemy.CurrentHP <= 0)
            {
                if (eUI.hudRect != null) eUI.hudRect.gameObject.SetActive(false);
                if (eUI.slotsGroup != null) eUI.slotsGroup.gameObject.SetActive(false);
                continue;
            }

            if (eUI.hudRect != null)
            {
                eUI.hudRect.gameObject.SetActive(true);
                Vector3 s = _mainCamera.WorldToScreenPoint(enemy.transform.position + hpBottomOffset); s.z = 0; eUI.hudRect.position = s;

                // 更新敵人純數字血量
                if (eUI.hpText) eUI.hpText.text = enemy.CurrentHP.ToString();
                if (eUI.staggerText) eUI.staggerText.text = enemy.CurrentStagger.ToString();
                if (eUI.buffText) eUI.buffText.text = enemy.activeBuffs.Count > 0 ? string.Join("\n", enemy.activeBuffs) : "";
            }

            if (eUI.slotsGroup != null)
            {
                eUI.slotsGroup.gameObject.SetActive(true);
                Vector3 s = _mainCamera.WorldToScreenPoint(enemy.transform.position + (enemy.transform.forward * slotsForwardDistance) + slotsVerticalOffset); s.z = 0; eUI.slotsGroup.position = s;
            }
        }
    }

    public void ShowFloatingText(BattleUnit unit, string message, Color color)
    {
        RectTransform parentHUD = null;
        if (unit == _bm.Player) parentHUD = playerHUD;
        else if (_enemyUIs.ContainsKey(unit)) parentHUD = _enemyUIs[unit].hudRect;

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
        foreach (var eUI in _enemyUIs.Values) if (eUI.hudRect) eUI.hudRect.gameObject.SetActive(false);

        if (roundText) roundText.gameObject.SetActive(false);
        if (startClashButton) startClashButton.gameObject.SetActive(false);
        if (battleLogText) battleLogText.gameObject.SetActive(false);
    }

    public void HideAllCombatUI()
    {
        if (playerHUD) playerHUD.gameObject.SetActive(false);
        foreach (var eUI in _enemyUIs.Values) if (eUI.hudRect) eUI.hudRect.gameObject.SetActive(false);

        if (playerSlotsGroup) playerSlotsGroup.gameObject.SetActive(false);
        foreach (var eUI in _enemyUIs.Values) if (eUI.slotsGroup) eUI.slotsGroup.gameObject.SetActive(false);

        if (startClashButton) startClashButton.gameObject.SetActive(false);
        if (roundText) roundText.gameObject.SetActive(false);
        if (handContainer) handContainer.gameObject.SetActive(false);

        ClearAllSlots();
        HideHoverInfo();
    }

    public void ShowCombatUI()
    {
        if (playerHUD) playerHUD.gameObject.SetActive(true);
        foreach (var eUI in _enemyUIs.Values) if (eUI.hudRect) eUI.hudRect.gameObject.SetActive(true);

        if (playerSlotsGroup) playerSlotsGroup.gameObject.SetActive(true);
        foreach (var eUI in _enemyUIs.Values) if (eUI.slotsGroup) eUI.slotsGroup.gameObject.SetActive(true);

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
            foreach (var eUI in _enemyUIs.Values) if (eUI.slotsGroup) eUI.slotsGroup.gameObject.SetActive(false);
        }
        else if (state == BattleManager.BattleState.AssignPhase || state == BattleManager.BattleState.Setup)
        {
            if (playerSlotsGroup) playerSlotsGroup.gameObject.SetActive(true);
            foreach (var eUI in _enemyUIs.Values) if (eUI.slotsGroup) eUI.slotsGroup.gameObject.SetActive(true);
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

        // 過濾已在場上裝填的牌
        List<CardData> slottedCards = new List<CardData>();
        if (playerSlotsGroup != null)
        {
            foreach (Transform child in playerSlotsGroup)
            {
                var s = child.GetComponent<SpeedDiceSlot>();
                if (s != null)
                {
                    System.Reflection.FieldInfo field = typeof(SpeedDiceSlot).GetField("cardData", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null)
                    {
                        CardData data = field.GetValue(s) as CardData;
                        if (data != null) slottedCards.Add(data);
                    }
                }
            }
        }

        foreach (var card in _bm.Player.Hand)
        {
            // 若該卡牌已被裝填，則跳過，不會生成對應按鈕
            if (slottedCards.Contains(card)) continue;

            var btn = Instantiate(cardButtonPrefab, handContainer);
            var cardUI = btn.GetComponent<CardUI>();
            if (cardUI != null) cardUI.Setup(card, slot);
        }
    }

    public void CloseCardSelection() { if (handContainer) handContainer.gameObject.SetActive(false); }

    public void ClearAllSlots()
    {
        if (playerSlotsGroup) foreach (Transform child in playerSlotsGroup) Destroy(child.gameObject);
        foreach (var eUI in _enemyUIs.Values) if (eUI.slotsGroup) foreach (Transform child in eUI.slotsGroup) Destroy(child.gameObject);
    }

    public SpeedDiceSlot CreateSlot(BattleUnit unit, int speed, bool isPlayer)
    {
        RectTransform parentGroup = playerSlotsGroup;
        if (!isPlayer)
        {
            if (_enemyUIs.ContainsKey(unit)) parentGroup = _enemyUIs[unit].slotsGroup;
            else parentGroup = enemySlotsGroup;
        }

        GameObject slotObj = Instantiate(speedDiceSlotPrefab, parentGroup, false);
        SpeedDiceSlot slot = slotObj.GetComponent<SpeedDiceSlot>();
        if (slot != null) slot.Setup(unit, speed, isPlayer);
        return slot;
    }

    private string GenerateLibraryStyleDiceText(CardData card)
    {
        string details = "";
        if (!string.IsNullOrEmpty(card.description)) details += $"<size=90%><color=#DDDDDD>{card.description}</color></size>\n";
        if (card.diceList.Count > 0) details += "<color=#555555>─────────────────</color>\n";

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

        if (bgOverlay)
        {
            bgOverlay.SetActive(true);
            bgOverlay.transform.SetAsLastSibling();
        }

        cardInspectPanel.SetActive(true);
        cardInspectPanel.transform.SetAsLastSibling();

        if (inspectNameText) inspectNameText.text = $"<size=120%><b>{card.cardName}</b></size>";

        if (inspectArtwork != null)
        {
            inspectArtwork.sprite = card.artwork;
            inspectArtwork.gameObject.SetActive(card.artwork != null);
        }

        if (inspectDescText) inspectDescText.text = GenerateLibraryStyleDiceText(card);
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
        targetPanel.transform.SetAsLastSibling();
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent) if (child.name == childName) return child;
        foreach (Transform child in parent)
        {
            Transform found = FindChildRecursive(child, childName);
            if (found != null) return found;
        }
        return null;
    }

    private void UpdateTooltipData(GameObject panel, CardData card)
    {
        Image artImg = null;
        var artTransform = FindChildRecursive(panel.transform, "Artwork");
        if (artTransform != null) artImg = artTransform.GetComponent<Image>();

        if (artImg == null)
        {
            foreach (var img in panel.GetComponentsInChildren<Image>(true))
                if (img.gameObject != panel) { artImg = img; break; }
        }

        if (artImg != null)
        {
            artImg.sprite = card.artwork;
            artImg.gameObject.SetActive(card.artwork != null);
        }

        TextMeshProUGUI[] allTexts = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
        TextMeshProUGUI nameTmp = null;
        TextMeshProUGUI contentTmp = null;

        foreach (var t in allTexts)
        {
            string lowerName = t.name.ToLower();
            if (lowerName.Contains("name")) nameTmp = t;
            else if (lowerName.Contains("content") || lowerName.Contains("desc") || lowerName.Contains("info")) contentTmp = t;
        }

        if (nameTmp == null && allTexts.Length > 0) nameTmp = allTexts[0];
        if (contentTmp == null && allTexts.Length > 1)
            for (int i = 1; i < allTexts.Length; i++)
                if (allTexts[i] != nameTmp) { contentTmp = allTexts[i]; break; }

        if (nameTmp != null && contentTmp != null)
        {
            nameTmp.text = $"<size=120%><color=#FFFFFF><b>{card.cardName}</b></color></size>";
            contentTmp.text = GenerateLibraryStyleDiceText(card);
        }
        else if (nameTmp != null)
        {
            nameTmp.text = $"<size=120%><color=#FFFFFF><b>{card.cardName}</b></color></size>\n" + GenerateLibraryStyleDiceText(card);
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