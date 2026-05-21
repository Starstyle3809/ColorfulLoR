using UnityEngine;

/// <summary>
/// 全域遊戲狀態管理器。
/// 負責協調「探索模式」與「戰鬥模式」的切換，
/// 以及連接 CameraDirector / BattleManager / MapExploration。
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Explore, Battle }
    public GameState CurrentState { get; private set; }

    [Header("場景中的核心元件")]
    public BattleUnit playerUnit;
    public BattleUIController battleUI;
    public MapExploration mapExploration;

    [Header("戰鬥場景根物件")]
    [Tooltip("包含所有戰鬥 UI 的 Canvas 或 GameObject")]
    public GameObject battleCanvas;

    [Header("探索場景根物件")]
    public GameObject exploreCanvas;

    private EnemyMarker _currentEnemy;

    // ─────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        EnterExploreMode();
    }

    // ── 進入探索模式 ──────────────────────────────────────
    public void EnterExploreMode()
    {
        CurrentState = GameState.Explore;
        mapExploration?.SetMovementEnabled(true);
        CameraDirector.Instance?.SwitchToExplore();

        if (battleCanvas) battleCanvas.SetActive(false);
        if (exploreCanvas) exploreCanvas.SetActive(true);

        // ★ 確保探索模式下，腳下的跟隨 UI 也完全隱藏
        battleUI?.HideUI();
    }

    // ── 觸發戰鬥（由 MapExploration 呼叫）────────────────
    public void StartBattle(EnemyMarker enemy, bool isAmbush)
    {
        _currentEnemy = enemy;
        CurrentState = GameState.Battle;

        mapExploration?.SetMovementEnabled(false);
        CameraDirector.Instance?.SwitchToBattleOverview();

        if (battleCanvas) battleCanvas.SetActive(true);
        if (exploreCanvas) exploreCanvas.SetActive(false);

        var enemyUnit = enemy.battleUnit;

        // ★ 修正核心順序：必須「先」讓 BattleManager 初始化雙方數值與抽牌
        BattleManager.Instance?.StartBattle(playerUnit, enemyUnit, isAmbush);

        // ★ 初始化完成後，「再」綁定 UI。這樣 UI 讀取到的就會是滿血 (或被偷襲後的扣血狀態)！
        battleUI?.BindUnits(playerUnit, enemyUnit);
    }

    // ── 戰鬥結束回調 ─────────────────────────────────────
    public void OnBattleFinished(bool playerWins)
    {
        if (!playerWins)
        {
            Debug.Log("[GameManager] 玩家敗北，重置場景");
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
            return;
        }

        // 敵人消滅
        if (_currentEnemy) _currentEnemy.gameObject.SetActive(false);

        // 延遲返回探索
        Invoke(nameof(EnterExploreMode), 2f);
    }
}