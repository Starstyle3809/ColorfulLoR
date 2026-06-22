using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Explore, Battle }
    public GameState CurrentState { get; private set; }
    public event System.Action<GameState> OnGameStateChanged;

    [Header("場景中的核心元件")]
    public BattleUnit playerUnit;
    public BattleUIController battleUI;
    public MapExploration mapExploration;

    [Header("戰鬥場景根物件")]
    public GameObject battleCanvas;

    [Header("探索場景根物件")]
    public GameObject exploreCanvas;

    private EnemyMarker _currentEnemy;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        EnterExploreMode();
    }

    public void EnterExploreMode()
    {
        CurrentState = GameState.Explore;
        OnGameStateChanged?.Invoke(CurrentState);
        mapExploration?.SetMovementEnabled(true);
        CameraDirector.Instance?.SwitchToExplore();


        if (battleCanvas) battleCanvas.SetActive(false);
        if (exploreCanvas) exploreCanvas.SetActive(true);

        battleUI?.DisableEntireBattleUI();
    }

    public void StartBattle(EnemyMarker enemy, bool isAmbush)
    {
        _currentEnemy = enemy;
        CurrentState = GameState.Battle;
        OnGameStateChanged?.Invoke(CurrentState);
        mapExploration?.SetMovementEnabled(false);
        CameraDirector.Instance?.SwitchToBattleOverview();

        if (battleCanvas) battleCanvas.SetActive(true);
        if (exploreCanvas) exploreCanvas.SetActive(false);

        List<BattleUnit> enemies = new List<BattleUnit>();
        if (enemy != null)
        {
            if (enemy.battleUnit != null) enemies.Add(enemy.battleUnit);
            if (enemy.linkedEnemies != null)
            {
                foreach (var linked in enemy.linkedEnemies)
                {
                    if (linked != null && linked.battleUnit != null) enemies.Add(linked.battleUnit);
                }
            }
        }

        BattleManager.Instance?.StartBattle(playerUnit, enemies, isAmbush);
    }

    public void OnBattleFinished(bool playerWins)
    {
        if (!playerWins)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
            return;
        }

        // ★ 戰鬥勝利，正確設定 isDefeated 標記，並關閉碰撞體避免重複觸發，且這將阻止它戰後逃跑
        if (_currentEnemy)
        {
            _currentEnemy.isDefeated = true;
            var colliders = _currentEnemy.GetComponentsInChildren<Collider>();
            foreach (var c in colliders) c.enabled = false;

            if (_currentEnemy)
            {
                // 處理主要遭遇的敵人
                ProcessEnemyPostBattle(_currentEnemy);

                // 處理被牽扯進戰鬥的連動敵人
                if (_currentEnemy.linkedEnemies != null)
                {
                    foreach (var linked in _currentEnemy.linkedEnemies)
                    {
                        if (linked != null)
                        {
                            ProcessEnemyPostBattle(linked);
                        }
                    }
                }
            }

            EnterExploreMode();
        }
    }

        private void ProcessEnemyPostBattle(EnemyMarker marker)
    {
        if (marker.battleUnit != null)
        {
            if (marker.battleUnit.CurrentHP <= 0)
            {
                // 真的是被打死的：標記擊敗、關閉碰撞體
                marker.isDefeated = true;
                var colliders = marker.GetComponentsInChildren<Collider>();
                foreach (var c in colliders) c.enabled = false;
            }
            else
            {
                // 沒死 (因隊長死亡而提早結束戰鬥)：強制逃跑
                marker.ForceFlee();
            }
        }
    }
}