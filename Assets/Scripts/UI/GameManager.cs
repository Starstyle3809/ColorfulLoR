using System.Collections.Generic;
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

    private List<EnemyMarker> _currentEnemies = new List<EnemyMarker>();

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
        battleUI?.DisableEntireBattleUI();
    }

    // ── 觸發戰鬥（由 MapExploration 呼叫）────────────────

    // ★ 新增一個 List 記錄當下遭遇的所有地圖敵人標記
    private System.Collections.Generic.List<EnemyMarker> _currentEncounter = new System.Collections.Generic.List<EnemyMarker>();

    // ── 觸發戰鬥（由 MapExploration 呼叫）────────────────
    public void StartBattle(EnemyMarker triggeredEnemy, bool isAmbush)
    {
        _currentEnemies.Clear();
        _currentEnemies.Add(triggeredEnemy);
        _currentEnemies.AddRange(triggeredEnemy.linkedEnemies); // 把同夥也加進來

        CurrentState = GameState.Battle;
        mapExploration?.SetMovementEnabled(false);
        CameraDirector.Instance?.SwitchToBattleOverview();

        if (battleCanvas) battleCanvas.SetActive(true);
        if (exploreCanvas) exploreCanvas.SetActive(false);

        // 把地圖標記轉換為戰鬥單位
        List<BattleUnit> enemies = new List<BattleUnit>();
        foreach (var marker in _currentEnemies)
        {
            if (marker != null && marker.battleUnit != null)
                enemies.Add(marker.battleUnit);
        }

        BattleManager.Instance?.StartBattle(playerUnit, enemies, isAmbush);
    }

    // ── 戰鬥結束回調 ─────────────────────────────────────
    public void OnBattleFinished(bool playerWins)
    {
        if (!playerWins)
        {
            Debug.Log("[GameManager] 玩家敗北，重置場景");
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
            return;
        }

        // ★ 判斷敵人是死亡還是存活(逃跑)
        foreach (var marker in _currentEnemies)
        {
            if (marker != null)
            {
                if (marker.battleUnit.CurrentHP <= 0)
                    marker.gameObject.SetActive(false); // 死亡直接消失
                else
                    marker.Escape(); // 存活的執行逃跑
            }
        }

        Invoke(nameof(EnterExploreMode), 2f);
    }
}
