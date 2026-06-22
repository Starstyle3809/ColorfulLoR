using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

public class CardUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [HideInInspector] public CardData cardData;
    [HideInInspector] public SpeedDiceSlot sourceSlot;

    [Header("數值與圖片顯示")]
    public TextMeshProUGUI valueText;
    public Image artworkImage;

    private Canvas _canvas;
    private Canvas _localCanvas;
    private CanvasGroup _canvasGroup;
    private Vector3 _originalScale;
    private float hoverScaleMultiplier = 1.15f;

    private void Awake()
    {
        _originalScale = transform.localScale;

        // 先取得最外層的 Canvas，供拖曳時轉換 Parent 以脫離 Layout Group
        _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();

        _canvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // 賦予卡牌獨立的 Canvas 來控制顯示層級，避免改變順序破壞排版
        _localCanvas = gameObject.GetComponent<Canvas>();
        if (_localCanvas == null)
        {
            _localCanvas = gameObject.AddComponent<Canvas>();
            gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }
        _localCanvas.overrideSorting = true;
        _localCanvas.sortingOrder = 1;
    }

    public void Setup(CardData card, SpeedDiceSlot source)
    {
        cardData = card;
        sourceSlot = source;
        var nameText = transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
        if (nameText != null) nameText.text = card.cardName;
        else if (GetComponentInChildren<TextMeshProUGUI>())
            GetComponentInChildren<TextMeshProUGUI>().text = card.cardName;
        
        if (artworkImage != null && card != null)
        {
            if (card.artwork != null)
            {
                artworkImage.sprite = card.artwork;
                artworkImage.gameObject.SetActive(true); // 有圖片就顯示
            }
            else
            {
                artworkImage.gameObject.SetActive(false); // 沒設定圖片就隱藏
            }
        }

        // ==========================================
        // ★ 新增：顯示獨立的效果數值與拚點勝率
        // ==========================================
        if (valueText != null && card != null && card.diceList.Count > 0)
        {
            // 取第一顆骰子作為代表
            DiceData mainDice = card.diceList[0];

            if (mainDice.type == DiceType.Evade)
            {
                // 閃避不造成傷害/補血，只顯示躲避與勝率
                valueText.text = $"{mainDice.minVal}~{mainDice.maxVal}";
            }
            else
            {
               
                valueText.text = $"{mainDice.effectValue}";
            }
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            FindAnyObjectByType<BattleUIController>().ShowCardInspect(cardData);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        transform.localScale = _originalScale * hoverScaleMultiplier;
        if (_localCanvas != null) _localCanvas.sortingOrder = 10;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!eventData.dragging)
        {
            transform.localScale = _originalScale;
            if (_localCanvas != null) _localCanvas.sortingOrder = 1;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_canvasGroup != null) _canvasGroup.blocksRaycasts = false;
        if (_canvas != null) transform.SetParent(_canvas.transform);
        transform.localScale = _originalScale * 0.7f;
        if (_localCanvas != null) _localCanvas.sortingOrder = 20;
    }

    public void OnDrag(PointerEventData eventData) => transform.position = eventData.position;

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_canvasGroup != null) _canvasGroup.blocksRaycasts = true;
        transform.localScale = _originalScale;

        PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = eventData.position };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        SpeedDiceSlot targetSlot = null;

        foreach (var result in results)
        {
            if (targetSlot == null) targetSlot = result.gameObject.GetComponentInParent<SpeedDiceSlot>();
        }

        var uiController = FindAnyObjectByType<BattleUIController>();

        // 只允許放到敵人的「行動槽」上 -> 進行拚點 或 單方面攻擊
        if (targetSlot != null && !targetSlot.IsPlayerSlot)
        {
            bool canClash = false;
            if (targetSlot.TargetSlot == sourceSlot) canClash = true;
            else if (sourceSlot.Speed >= targetSlot.Speed) canClash = true;

            var allPlayerSlots = uiController.playerSlotsGroup.GetComponentsInChildren<SpeedDiceSlot>();
            var allEnemySlots = uiController.enemySlotsGroup.GetComponentsInChildren<SpeedDiceSlot>();

            if (canClash)
            {
                foreach (var pSlot in allPlayerSlots)
                {
                    if (pSlot != sourceSlot && pSlot.TargetSlot == targetSlot)
                    {
                        pSlot.SetAction(pSlot.AssignedCard, null, targetSlot.Owner);
                    }
                }

                sourceSlot.SetAction(cardData, targetSlot, null);

                if (targetSlot.AssignedCard != null)
                {
                    targetSlot.SetAction(targetSlot.AssignedCard, sourceSlot, null);
                }
                else
                {
                    targetSlot.TargetSlot = sourceSlot;
                    targetSlot.TargetUnit = null;
                }
            }
            else
            {
                sourceSlot.SetAction(cardData, null, targetSlot.Owner);
            }

            foreach (var pSlot in allPlayerSlots)
            {
                if (pSlot.TargetSlot == null && pSlot.TargetUnit != null)
                {
                    foreach (var eSlot in allEnemySlots)
                    {
                        if (eSlot.TargetSlot == pSlot)
                        {
                            eSlot.SetAction(eSlot.AssignedCard, null, pSlot.Owner);
                        }
                    }
                }
            }

            gameObject.SetActive(false);
            uiController.CloseCardSelection();
            return;
        }

        uiController.CloseCardSelection();
        Destroy(gameObject);
    }
}