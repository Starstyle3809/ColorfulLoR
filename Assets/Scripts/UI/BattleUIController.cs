using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Text.RegularExpressions;

public class BattleUIController : MonoBehaviour
{
    [Header("UI 容器")]
    public RectTransform playerHUD;
    public RectTransform enemyHUD;
    public RectTransform playerSlotsGroup;
    public RectTransform enemySlotsGroup;

    [Header("動態位置偏移設定 (絕對世界座標)")]
    public Vector3 playerHpOffset = new Vector3(0, -1.5f, 0);
    public Vector3 enemyHpOffset = new Vector3(0, -1.5f, 0);
    public Vector3 playerSlotsOffset = new Vector3(-2.5f, 3.5f, 0f);
    public Vector3 enemySlotsOffset = new Vector3(2.5f, 3.5f, 0f);

    [Header("狀態 UI")]
    public TextMeshProUGUI playerHPText;
    public CustomHPBar playerHPBar;
    public RectTransform playerBuffContainer;

    public TextMeshProUGUI enemyHPText;
    public CustomHPBar enemyHPBar;
    public RectTransform enemyBuffContainer;

    // ★ 已經把死板的護盾變數全部刪除，讓介面回歸乾淨！

    [System.Serializable]
    public struct BuffSpriteMapping
    {
        public string buffNameKeyword;
        public Sprite icon;
    }

    [Header("Buff 圖示系統")]
    public GameObject buffIconPrefab;
    [Tooltip("在這裡設定 Buff 關鍵字對應的圖片")]
    public List<BuffSpriteMapping> buffSpriteDatabase;

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

    private class EnemyUI
    {
        public RectTransform hudRect;
        public RectTransform slotsGroup;
        public TextMeshProUGUI hpText;
        public CustomHPBar hpBar;
        public RectTransform buffContainer;
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
    }

    private string GetPathToChild(Transform child, Transform root)
    {
        if (child == root) return "";
        string path = child.name;
        Transform curr = child.parent;
        while (curr != null && curr != root)
        {
            path = curr.name + "/" + path;
            curr = curr.parent;
        }
        return path;
    }

    private void InitEnemyUIs()
    {
        foreach (var kvp in _enemyUIs)
        {
            if (kvp.Value.hudRect != null && kvp.Value.hudRect != enemyHUD) Destroy(kvp.Value.hudRect.gameObject);
            if (kvp.Value.slotsGroup != null && kvp.Value.slotsGroup != enemySlotsGroup) Destroy(kvp.Value.slotsGroup.gameObject);
        }
        _enemyUIs.Clear();

        if (_bm.Enemies == null || _bm.Enemies.Count == 0) return;

        EnemyUI firstUI = new EnemyUI();
        firstUI.hudRect = enemyHUD;
        firstUI.slotsGroup = enemySlotsGroup;
        firstUI.hpText = enemyHPText;
        firstUI.hpBar = enemyHPBar;
        firstUI.buffContainer = enemyBuffContainer;
        _enemyUIs[_bm.Enemies[0]] = firstUI;

        if (enemyHUD) enemyHUD.gameObject.SetActive(true);
        if (enemySlotsGroup) enemySlotsGroup.gameObject.SetActive(true);

        for (int i = 1; i < _bm.Enemies.Count; i++)
        {
            if (_bm.Enemies[i] == null) continue;

            EnemyUI newUI = new EnemyUI();
            if (enemyHUD != null)
            {
                newUI.hudRect = Instantiate(enemyHUD, enemyHUD.parent);
                newUI.hudRect.gameObject.SetActive(true);

                if (enemyHPText != null) { Transform t = newUI.hudRect.Find(GetPathToChild(enemyHPText.transform, enemyHUD)); if (t) newUI.hpText = t.GetComponent<TextMeshProUGUI>(); }
                if (enemyHPBar != null) { Transform t = newUI.hudRect.Find(GetPathToChild(enemyHPBar.transform, enemyHUD)); if (t) newUI.hpBar = t.GetComponent<CustomHPBar>(); }
                if (enemyBuffContainer != null) { Transform t = newUI.hudRect.Find(GetPathToChild(enemyBuffContainer.transform, enemyHUD)); if (t) newUI.buffContainer = t.GetComponent<RectTransform>(); }
            }

            if (enemySlotsGroup != null)
            {
                newUI.slotsGroup = Instantiate(enemySlotsGroup, enemySlotsGroup.parent);
                newUI.slotsGroup.gameObject.SetActive(true);
            }

            _enemyUIs[_bm.Enemies[i]] = newUI;
        }
    }

    private void Update()
    {
        if (_bm == null || _mainCamera == null || _bm.CurrentState == BattleManager.BattleState.BattleEnd) return;

        if (_bm.Player != null && playerHUD != null)
        {
            Vector3 sHp = _mainCamera.WorldToScreenPoint(_bm.Player.transform.position + playerHpOffset);
            sHp.z = 0;
            playerHUD.position = sHp;
            if (playerSlotsGroup != null)
            {
                Vector3 slotPos = _mainCamera.WorldToScreenPoint(_bm.Player.transform.position + playerSlotsOffset);
                slotPos.z = 0;
                playerSlotsGroup.position = slotPos;
            }

            if (playerHPText) playerHPText.text = _bm.Player.CurrentHP.ToString();
            if (playerHPBar) playerHPBar.UpdateHP(_bm.Player.CurrentHP);

            // ==============================================
            // ★ 將護盾混入 Buff 清單一起顯示給玩家
            // ==============================================
            if (playerBuffContainer)
            {
                List<string> displayBuffs = new List<string>(_bm.Player.activeBuffs);
                // 如果有護盾，就塞進名單的最前面！
                if (_bm.Player.currentShield > 0) displayBuffs.Insert(0, $"護盾 {_bm.Player.currentShield}");

                SyncBuffIcons(playerBuffContainer, displayBuffs);
            }
        }

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
                Vector3 sHp = _mainCamera.WorldToScreenPoint(enemy.transform.position + enemyHpOffset);
                sHp.z = 0;
                eUI.hudRect.position = sHp;

                if (eUI.hpText) eUI.hpText.text = enemy.CurrentHP.ToString();
                if (eUI.hpBar) eUI.hpBar.UpdateHP(enemy.CurrentHP);

                // ==============================================
                // ★ 將護盾混入 Buff 清單一起顯示給敵人
                // ==============================================
                if (eUI.buffContainer)
                {
                    List<string> displayBuffs = new List<string>(enemy.activeBuffs);
                    if (enemy.currentShield > 0) displayBuffs.Insert(0, $"護盾 {enemy.currentShield}");

                    SyncBuffIcons(eUI.buffContainer, displayBuffs);
                }
            }

            if (eUI.slotsGroup != null)
            {
                
                Vector3 slotPos = _mainCamera.WorldToScreenPoint(enemy.transform.position + enemySlotsOffset);
                slotPos.z = 0;
                eUI.slotsGroup.position = slotPos;
            }
        }
    }

    private void SyncBuffIcons(RectTransform container, List<string> buffs)
    {
        if (container == null || buffIconPrefab == null || buffs == null) return;

        while (container.childCount < buffs.Count)
        {
            Instantiate(buffIconPrefab, container);
        }

        for (int i = 0; i < container.childCount; i++)
        {
            Transform child = container.GetChild(i);

            if (i < buffs.Count)
            {
                child.gameObject.SetActive(true);
                Image img = child.GetComponent<Image>();
                TextMeshProUGUI txt = child.GetComponentInChildren<TextMeshProUGUI>();

                string currentBuff = buffs[i];

                if (img != null)
                {
                    img.sprite = null;
                    foreach (var mapping in buffSpriteDatabase)
                    {
                        if (currentBuff.Contains(mapping.buffNameKeyword))
                        {
                            img.sprite = mapping.icon;
                            break;
                        }
                    }
                }

                if (txt != null)
                {
                    string num = Regex.Match(currentBuff, @"\d+").Value;
                    txt.text = num;
                    txt.gameObject.SetActive(!string.IsNullOrEmpty(num));
                }
            }
            else
            {
                child.gameObject.SetActive(false);
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
        if (state == BattleManager.BattleState.Setup) InitEnemyUIs();
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
        if (roundText) roundText.text = $"回合 {_bm.currentRound}";
        CloseCardSelection();
    }

    private void OnBattleEnded(bool playerWins)
    {
        if (battleEndPanel) battleEndPanel.SetActive(true);
        if (battleEndText) battleEndText.text = playerWins ? "勝利!" : "戰敗...";
    }

    public void OpenCardSelection(SpeedDiceSlot slot)
    {
        if (handContainer == null || cardButtonPrefab == null) return;
        handContainer.gameObject.SetActive(true);
        foreach (Transform child in handContainer) Destroy(child.gameObject);

        List<CardData> availableCards = new List<CardData>(_bm.Player.Hand);

        if (playerSlotsGroup != null)
        {
            foreach (Transform child in playerSlotsGroup)
            {
                var s = child.GetComponent<SpeedDiceSlot>();
                if (s != null && s.AssignedCard != null && s != slot)
                {
                    availableCards.Remove(s.AssignedCard);
                }
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
        if (inspectArtwork != null) { inspectArtwork.sprite = card.artwork; inspectArtwork.gameObject.SetActive(card.artwork != null); }
        if (inspectDescText) inspectDescText.text = GenerateLibraryStyleDiceText(card);
    }

    public void HideCardInspect()
    {
        if (cardInspectPanel) cardInspectPanel.SetActive(false);
        if (bgOverlay) bgOverlay.SetActive(false);
    }

    public void ShowHoverInfo(SpeedDiceSlot hoveredSlot)
    {
        if (hoveredSlot == null) return;

        // 1. 顯示被懸停的槽位「本身」裝填的卡牌
        if (hoveredSlot.AssignedCard != null)
        {
            GameObject targetPanel = hoveredSlot.IsPlayerSlot ? playerTooltipPanel : enemyTooltipPanel;
            if (targetPanel != null)
            {
                UpdateTooltipData(targetPanel, hoveredSlot.AssignedCard);
                targetPanel.SetActive(true);
                targetPanel.transform.SetAsLastSibling();
            }
        }

        // 2. 神奇魔法：尋找是否有其他槽位「正瞄準著」這個槽位，如果有，一併顯示對方的卡牌！
        SpeedDiceSlot targetingSlot = FindSlotTargeting(hoveredSlot);
        if (targetingSlot != null && targetingSlot.AssignedCard != null)
        {
            // 對方該用哪個面板顯示 (如果我是玩家，瞄準我的就是敵人，所以用敵人的面板)
            GameObject oppPanel = targetingSlot.IsPlayerSlot ? playerTooltipPanel : enemyTooltipPanel;
            if (oppPanel != null)
            {
                UpdateTooltipData(oppPanel, targetingSlot.AssignedCard);
                oppPanel.SetActive(true);
                oppPanel.transform.SetAsLastSibling();
            }
        }
    }

    private SpeedDiceSlot FindSlotTargeting(SpeedDiceSlot target)
    {
        if (playerSlotsGroup != null)
        {
            foreach (Transform child in playerSlotsGroup)
            {
                var s = child.GetComponent<SpeedDiceSlot>();
                if (s != null && s.TargetSlot == target) return s;
            }
        }
        foreach (var eUI in _enemyUIs.Values)
        {
            if (eUI.slotsGroup != null)
            {
                foreach (Transform child in eUI.slotsGroup)
                {
                    var s = child.GetComponent<SpeedDiceSlot>();
                    if (s != null && s.TargetSlot == target) return s;
                }
            }
        }
        return null;
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
        if (artImg == null) { foreach (var img in panel.GetComponentsInChildren<Image>(true)) if (img.gameObject != panel) { artImg = img; break; } }

        if (artImg != null) { artImg.sprite = card.artwork; artImg.gameObject.SetActive(card.artwork != null); }

        TextMeshProUGUI[] allTexts = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
        TextMeshProUGUI nameTmp = null; TextMeshProUGUI contentTmp = null;

        foreach (var t in allTexts)
        {
            string lowerName = t.name.ToLower();
            if (lowerName.Contains("name")) nameTmp = t;
            else if (lowerName.Contains("content") || lowerName.Contains("desc") || lowerName.Contains("info")) contentTmp = t;
        }

        if (nameTmp == null && allTexts.Length > 0) nameTmp = allTexts[0];
        if (contentTmp == null && allTexts.Length > 1)
            for (int i = 1; i < allTexts.Length; i++) if (allTexts[i] != nameTmp) { contentTmp = allTexts[i]; break; }

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

    public void HideHoverInfo(bool isPlayerSlot) { if (isPlayerSlot && playerTooltipPanel != null) playerTooltipPanel.SetActive(false); else if (!isPlayerSlot && enemyTooltipPanel != null) enemyTooltipPanel.SetActive(false); }
    public void HideHoverInfo() { if (playerTooltipPanel != null) playerTooltipPanel.SetActive(false); if (enemyTooltipPanel != null) enemyTooltipPanel.SetActive(false); }

    private void AppendLog(string line) { _logLines.Add(line); if (_logLines.Count > maxLogLines) _logLines.RemoveAt(0); if (battleLogText) battleLogText.text = string.Join("\n", _logLines); }
}