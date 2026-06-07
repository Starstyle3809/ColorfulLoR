using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class SpeedDiceSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public TextMeshProUGUI speedText;
    public Image cardThumbnail;

    public BattleUnit Owner { get; private set; }
    public int Speed { get; private set; }
    public bool IsPlayerSlot { get; private set; }
    public bool IsResolved { get; set; }

    [HideInInspector] public CardData AssignedCard;
    [HideInInspector] public SpeedDiceSlot TargetSlot;
    [HideInInspector] public BattleUnit TargetUnit;

    private BattleUIController _ui;

    public void Setup(BattleUnit owner, int speed, bool isPlayer)
    {
        Owner = owner; Speed = speed; IsPlayerSlot = isPlayer;
        AssignedCard = null; TargetSlot = null; TargetUnit = null; IsResolved = false;
        _ui = FindAnyObjectByType<BattleUIController>();

        if (speedText) speedText.text = owner.staggerTurnsLeft > 0 ? "X" : speed.ToString();
        if (cardThumbnail) cardThumbnail.gameObject.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (AssignedCard != null && _ui != null)
            _ui.ShowHoverInfo(AssignedCard, GetComponent<RectTransform>(), IsPlayerSlot);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_ui != null) _ui.HideHoverInfo(IsPlayerSlot);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Owner.staggerTurnsLeft > 0) return;

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (IsPlayerSlot && AssignedCard != null && BattleManager.Instance.CurrentState == BattleManager.BattleState.AssignPhase)
            {
                AssignedCard = null; TargetSlot = null; TargetUnit = null;
                if (cardThumbnail) cardThumbnail.gameObject.SetActive(false);
                if (_ui != null) _ui.HideHoverInfo(IsPlayerSlot);
            }
        }
        else if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (IsPlayerSlot && BattleManager.Instance.CurrentState == BattleManager.BattleState.AssignPhase)
                if (_ui != null) _ui.OpenCardSelection(this);
        }
    }

    public void SetAction(CardData card, SpeedDiceSlot clashTarget, BattleUnit oneSidedTarget)
    {
        AssignedCard = card; TargetSlot = clashTarget; TargetUnit = oneSidedTarget;
        if (cardThumbnail != null && card.artwork != null)
        {
            cardThumbnail.sprite = card.artwork;
            cardThumbnail.color = new Color(1f, 1f, 1f, 0.4f);
            cardThumbnail.gameObject.SetActive(true);
        }
    }
}