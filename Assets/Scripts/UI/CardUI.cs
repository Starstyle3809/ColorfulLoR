using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class CardUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [HideInInspector] public CardData cardData;
    [HideInInspector] public SpeedDiceSlot sourceSlot;

    private Canvas _canvas;
    private CanvasGroup _canvasGroup;
    private Vector3 _originalScale;
    private float hoverScaleMultiplier = 1.15f;

    private void Awake()
    {
        _originalScale = transform.localScale;
        _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();

        _canvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void Setup(CardData card, SpeedDiceSlot source)
    {
        cardData = card;
        sourceSlot = source;

        // 如果你的 UI 內有 Name 這個文字框，更新卡名
        var nameText = transform.Find("Name")?.GetComponent<TMPro.TextMeshProUGUI>();
        if (nameText != null) nameText.text = card.cardName;
        else if (GetComponentInChildren<TMPro.TextMeshProUGUI>())
            GetComponentInChildren<TMPro.TextMeshProUGUI>().text = card.cardName;
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
        transform.SetAsLastSibling();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!eventData.dragging) transform.localScale = _originalScale;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_canvasGroup != null) _canvasGroup.blocksRaycasts = false;
        if (_canvas != null) transform.SetParent(_canvas.transform);
        transform.localScale = _originalScale * 0.7f;
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

        // 只允許放到敵人的「行動槽」上 -> 進行拼點 (或單方面攻擊該槽位擁有者)
        if (targetSlot != null && !targetSlot.IsPlayerSlot)
        {
            bool canClash = false;

            // 速度大於對方，或是該槽原本就已經鎖定我們，則允許拼點
            if (targetSlot.TargetSlot == sourceSlot) canClash = true;
            else if (sourceSlot.Speed > targetSlot.Speed) canClash = true;

            if (canClash)
            {
                // ★ 完美攔截邏輯：掃描所有我方行動槽，將所有之前瞄準此敵人槽的我方行動槽強制降級
                if (uiController.playerSlotsGroup != null)
                {
                    var allPlayerSlots = uiController.playerSlotsGroup.GetComponentsInChildren<SpeedDiceSlot>();
                    foreach (var pSlot in allPlayerSlots)
                    {
                        // 若這不是現在拖的這顆，但它正在瞄準目標敵人槽
                        if (pSlot != sourceSlot && pSlot.TargetSlot == targetSlot)
                        {
                            pSlot.TargetSlot = null;             // 解除它的拼點權
                            pSlot.TargetUnit = targetSlot.Owner; // 改為單向攻擊敵人單位
                        }
                    }
                }

                sourceSlot.SetAction(cardData, targetSlot, null);
                targetSlot.TargetSlot = sourceSlot; // 敵人改瞄準最後放上去的這顆骰子
            }
            else
            {
                // 速度不夠，只能單方面攻擊該敵人
                sourceSlot.SetAction(cardData, null, targetSlot.Owner);
            }

            // ★ 裝填完成後立即隱藏這張卡牌
            gameObject.SetActive(false);
            uiController.CloseCardSelection();
            return;
        }

        uiController.CloseCardSelection();
        Destroy(gameObject); // 沒放到正確位置就銷毀這張拖曳的 UI 牌
    }
}