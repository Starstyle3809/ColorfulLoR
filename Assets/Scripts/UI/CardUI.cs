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

        // ★ 嚴格判定：只找滑鼠底下的 SpeedDiceSlot，不再尋找 EnemyTargetArea
        foreach (var result in results)
        {
            if (targetSlot == null) targetSlot = result.gameObject.GetComponentInParent<SpeedDiceSlot>();
        }

        var uiController = FindAnyObjectByType<BattleUIController>();

        // 只允許放到敵人的「行動槽」上 -> 進行拼點 (或單方面攻擊該槽位擁有者)
        if (targetSlot != null && !targetSlot.IsPlayerSlot)
        {
            sourceSlot.SetAction(cardData, targetSlot, null);
            uiController.CloseCardSelection();
            Destroy(gameObject);
            return;
        }

        // 如果沒放到正確的敵方行動槽上，就取消裝填並關閉介面
        uiController.CloseCardSelection();
        Destroy(gameObject);
    }
}