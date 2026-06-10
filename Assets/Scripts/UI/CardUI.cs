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
        if (GetComponentInChildren<TMPro.TextMeshProUGUI>())
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
        if (eventData.dragging) return;
        transform.localScale = _originalScale * hoverScaleMultiplier;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (eventData.dragging) return;
        transform.localScale = _originalScale;
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

        // 判斷是否放到敵人的行動槽上
        if (targetSlot != null && !targetSlot.IsPlayerSlot)
        {
            bool canClash = false;

            // 速度大於對方，或是該槽原本就已經鎖定我們，則允許拼點
            if (targetSlot.TargetSlot == sourceSlot) canClash = true;
            else if (sourceSlot.Speed > targetSlot.Speed) canClash = true;

            if (canClash)
            {
                // ★ 核心邏輯：如果這個敵人槽已經被「其他我方行動槽」瞄準，剝奪前一個的拼點權！
                if (targetSlot.TargetSlot != null && targetSlot.TargetSlot != sourceSlot)
                {
                    targetSlot.TargetSlot.TargetSlot = null;              // 解除前一個槽的拼點狀態
                    targetSlot.TargetSlot.TargetUnit = targetSlot.Owner;  // 強制讓前一個槽變成單方面攻擊
                }

                sourceSlot.SetAction(cardData, targetSlot, null);
                targetSlot.TargetSlot = sourceSlot; // 敵人改瞄準最後放上去的這顆骰子
                Debug.Log($"[系統] 攔截成功！最後一顆進入拼點狀態。");
            }
            else
            {
                // 速度不夠，只能單方面攻擊
                sourceSlot.SetAction(cardData, null, targetSlot.Owner);
                Debug.Log($"[系統] 速度不足，將進行單方面攻擊。");
            }

            uiController.CloseCardSelection();
            Destroy(gameObject);
            return;
        }

        uiController.CloseCardSelection();
        Destroy(gameObject);
    }
}
