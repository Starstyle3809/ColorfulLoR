using UnityEngine;

/// <summary>
/// 負責在探索(俯視)與戰鬥(側視)模式間，自動切換角色的 2D 圖片。
/// </summary>
public class CharacterVisualSwitcher : MonoBehaviour
{
    [Header("視覺元件")]
    public SpriteRenderer spriteRenderer;

    [Header("探索模式 (俯視角)")]
    public Sprite exploreSprite;

    [Header("戰鬥模式 (側視角)")]
    public Sprite battleSprite;

    private void Start()
    {
        // ★ 修正：將訂閱移到 Start，確保 GameManager 絕對已經準備完畢
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged += HandleStateChanged;

            // 訂閱完畢後，立刻根據當前狀態初始化一次圖片
            HandleStateChanged(GameManager.Instance.CurrentState);
        }
    }

    private void OnDestroy()
    {
        // ★ 修正：改在物件被銷毀時取消訂閱
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleStateChanged;
        }
    }

    private void HandleStateChanged(GameManager.GameState newState)
    {
        if (spriteRenderer == null) return;

        // 根據不同模式換上對應的 Sprite
        if (newState == GameManager.GameState.Explore && exploreSprite != null)
        {
            spriteRenderer.sprite = exploreSprite;
        }
        else if (newState == GameManager.GameState.Battle && battleSprite != null)
        {
            spriteRenderer.sprite = battleSprite;
        }
    }
}