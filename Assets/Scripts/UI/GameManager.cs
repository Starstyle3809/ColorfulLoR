using UnityEngine;
using System.Collections.Generic;

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
    public GameObject battleCanvas;

    [Header("探索場景根物件")]
    public GameObject exploreCanvas;

    private List<EnemyMarker> _currentEnemies = new List<EnemyMarker>();

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
        mapExploration?.SetMovementEnabled(true);
        CameraDirector.Instance?.SwitchToExplore();

        if (battleCanvas) battleCanvas.SetActive(false);
        if (exploreCanvas) exploreCanvas.SetActive(true);

        battleUI?.DisableEntireBattleUI();
    }

    public void StartBattle(EnemyMarker triggeredEnemy, bool isAmbush)
    {
        _currentEnemies.Clear();
        _currentEnemies.Add(triggeredEnemy);
        if (triggeredEnemy.linkedEnemies != null)
        {
            _currentEnemies.AddRange(triggeredEnemy.linkedEnemies);
        }

        CurrentState = GameState.Battle;
        mapExploration?.SetMovementEnabled(false);
        CameraDirector.Instance?.SwitchToBattleOverview();

        if (battleCanvas) battleCanvas.SetActive(true);
        if (exploreCanvas) exploreCanvas.SetActive(false);

        List<BattleUnit> enemies = new List<BattleUnit>();
        foreach (var marker in _currentEnemies)
        {
            if (marker != null && marker.battleUnit != null)
                enemies.Add(marker.battleUnit);
        }

        BattleManager.Instance?.StartBattle(playerUnit, enemies, isAmbush);
    }

    public void OnBattleFinished(bool playerWins)
    {
        if (!playerWins)
        {
            Debug.Log("[GameManager] 玩家敗北，重置場景");
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
            return;
        }

        // ★ 關鍵修復：先立即切換回探索模式，交還攝影機控制權給 Cinemachine
        EnterExploreMode();

        // ★ 切換完畢後，才讓活著的敵人開始在探索地圖上逃跑
        foreach (var marker in _currentEnemies)
        {
            if (marker != null)
            {
                if (marker.battleUnit.CurrentHP <= 0)
                    marker.gameObject.SetActive(false);
                else
                    StartCoroutine(EnemyEscapeRoutine(marker));
            }
        }
    }

    private System.Collections.IEnumerator EnemyEscapeRoutine(EnemyMarker enemy)
    {
        if (enemy == null || playerUnit == null) yield break;

        var agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;
        var collider = enemy.GetComponent<Collider>();
        if (collider != null) collider.enabled = false;

        Vector3 escapeDir = (enemy.transform.position - playerUnit.transform.position).normalized;
        escapeDir.y = 0;

        if (escapeDir == Vector3.zero)
            escapeDir = new Vector3(UnityEngine.Random.Range(-1f, 1f), 0, UnityEngine.Random.Range(-1f, 1f)).normalized;

        float speed = enemy.escapeSpeed > 0 ? enemy.escapeSpeed : 8f;
        float elapsed = 0f;

        while (elapsed < 3f && enemy != null)
        {
            enemy.transform.position += escapeDir * speed * Time.deltaTime;
            enemy.transform.rotation = Quaternion.Slerp(enemy.transform.rotation, Quaternion.LookRotation(escapeDir), Time.deltaTime * 15f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (enemy != null) Destroy(enemy.gameObject);
    }
}