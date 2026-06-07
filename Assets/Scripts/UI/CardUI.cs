using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class CardUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [HideInInspector] public CardData cardData;
    [HideInInspector] public SpeedDiceSlot sourceSlot;

    private Canvas _canvas;
    private CanvasGroup _canvasGroup;
    private Vector3 _originalScale;

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
        // ★ 修正：右鍵只單純呼叫詳細大圖檢視，不傳入改變位置的參數
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            FindAnyObjectByType<BattleUIController>().ShowCardInspect(cardData);
        }
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

        foreach (var result in results)
        {
            SpeedDiceSlot enemySlot = result.gameObject.GetComponent<SpeedDiceSlot>();
            if (enemySlot != null && !enemySlot.IsPlayerSlot)
            {
                bool canClash = false;

                if (enemySlot.TargetSlot == sourceSlot) canClash = true;
                else if (sourceSlot.Speed > enemySlot.Speed) canClash = true;

                if (canClash)
                {
                    sourceSlot.SetAction(cardData, enemySlot, null);
                    enemySlot.TargetSlot = sourceSlot;
                    Debug.Log($"[系統] 攔截成功！進入拼點狀態。");
                }
                else
                {
                    sourceSlot.SetAction(cardData, null, enemySlot.Owner);
                    Debug.Log($"[系統] 速度不足，將進行單方面攻擊。");
                }

                FindAnyObjectByType<BattleUIController>().CloseCardSelection();
                Destroy(gameObject);
                return;
            }
        }

        FindAnyObjectByType<BattleUIController>().CloseCardSelection();
        Destroy(gameObject);
    }
}