using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 訂閱 BattleManager / BattleUnit 的事件，更新所有戰鬥 UI。
/// ★ 升級：移除生命週期衝突，開放 HideUI 供 GameManager 強制隱藏。
/// </summary>
public class BattleUIController : MonoBehaviour
{
    [Header("HUD 動態跟隨容器 (將血條和Buff放在裡面 - 預設在人物下方)")]
    public RectTransform playerHUDContainer;
    public RectTransform enemyHUDContainer;
    [Tooltip("下方 UI 在 3D 世界中相對於人物中心的偏移量")]
    public Vector3 hudWorldOffset = new Vector3(0f, -1.2f, 0f);

    [Header("上方狀態提示跟隨容器 (陷入混亂字樣 - 預設在人物上方)")]
    public RectTransform staggerIndicatorPlayer;
    public RectTransform staggerIndicatorEnemy;
    [Tooltip("上方 UI 在 3D 世界中相對於人物中心的偏移量")]
    public Vector3 aboveHeadWorldOffset = new Vector3(0f, 1.5f, 0f);

    [Header("玩家 UI (掛在 PlayerHUDContainer 底下)")]
    public Slider playerHPSlider;
    public Slider playerStaggerSlider;
    public TextMeshProUGUI playerHPText;
    public TextMeshProUGUI playerStaggerText;
    public TextMeshProUGUI playerBuffText;

    [Header("敵人 UI (掛在 EnemyHUDContainer 底下)")]
    public Slider enemyHPSlider;
    public Slider enemyStaggerSlider;
    public TextMeshProUGUI enemyHPText;
    public TextMeshProUGUI enemyStaggerText;
    public TextMeshProUGUI enemyBuffText;

    [Header("戰鬥日誌與全域 UI")]
    public TextMeshProUGUI battleLogText;
    public int maxLogLines = 8;
    public Transform handContainer;
    public GameObject cardButtonPrefab;
    public GameObject battleEndPanel;
    public TextMeshProUGUI battleEndText;

    private List<string> _logLines = new();
    private BattleManager _bm;
    private Camera _mainCam;

    private void Start()
    {
        _mainCam = Camera.main;
        _bm = BattleManager.Instance;
        if (_bm == null) { Debug.LogError("[UI] 找不到 BattleManager！"); return; }

        _bm.OnBattleLog += AppendLog;
        _bm.OnStateChanged += OnStateChanged;
        _bm.OnBattleEnded += OnBattleEnded;
        _bm.OnUnitStaggered += OnUnitStaggered;
    }

    // ★ 供 GameManager 呼叫：在探索模式下強制隱藏所有戰鬥 UI
    public void HideUI()
    {
        if (battleEndPanel) battleEndPanel.SetActive(false);
        if (staggerIndicatorPlayer) staggerIndicatorPlayer.gameObject.SetActive(false);
        if (staggerIndicatorEnemy) staggerIndicatorEnemy.gameObject.SetActive(false);
        if (playerHUDContainer) playerHUDContainer.gameObject.SetActive(false);
        if (enemyHUDContainer) enemyHUDContainer.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (_mainCam == null || _bm == null) return;

        // 更新腳下 HUD 條座標
        if (playerHUDContainer != null && playerHUDContainer.gameObject.activeSelf && _bm.Player != null)
        {
            Vector3 screenPos = _mainCam.WorldToScreenPoint(_bm.Player.transform.position + hudWorldOffset);
            playerHUDContainer.position = screenPos;
        }
        if (enemyHUDContainer != null && enemyHUDContainer.gameObject.activeSelf && _bm.Enemy != null)
        {
            Vector3 screenPos = _mainCam.WorldToScreenPoint(_bm.Enemy.transform.position + hudWorldOffset);
            enemyHUDContainer.position = screenPos;
        }

        // 更新頭頂 Stagger 提示座標
        if (staggerIndicatorPlayer != null && staggerIndicatorPlayer.gameObject.activeSelf && _bm.Player != null)
        {
            Vector3 screenPos = _mainCam.WorldToScreenPoint(_bm.Player.transform.position + aboveHeadWorldOffset);
            staggerIndicatorPlayer.position = screenPos;
        }
        if (staggerIndicatorEnemy != null && staggerIndicatorEnemy.gameObject.activeSelf && _bm.Enemy != null)
        {
            Vector3 screenPos = _mainCam.WorldToScreenPoint(_bm.Enemy.transform.position + aboveHeadWorldOffset);
            staggerIndicatorEnemy.position = screenPos;
        }
    }

    public void BindUnits(BattleUnit player, BattleUnit enemy)
    {
        // ★ 戰鬥正式開始，打開血條容器
        if (playerHUDContainer) playerHUDContainer.gameObject.SetActive(true);
        if (enemyHUDContainer) enemyHUDContainer.gameObject.SetActive(true);

        // 綁定玩家事件
        player.OnHPChanged += (cur, max) => UpdateHP(playerHPSlider, playerHPText, cur, max);
        player.OnStaggerChanged += (cur, max) => UpdateStagger(playerStaggerSlider, playerStaggerText, cur, max);
        player.OnBuffsChanged += (buffs) => UpdateBuffs(playerBuffText, buffs);
        player.OnStaggered += () => { if (staggerIndicatorPlayer) staggerIndicatorPlayer.gameObject.SetActive(true); };

        // 綁定敵人事件
        enemy.OnHPChanged += (cur, max) => UpdateHP(enemyHPSlider, enemyHPText, cur, max);
        enemy.OnStaggerChanged += (cur, max) => UpdateStagger(enemyStaggerSlider, enemyStaggerText, cur, max);
        enemy.OnBuffsChanged += (buffs) => UpdateBuffs(enemyBuffText, buffs);
        enemy.OnStaggered += () => { if (staggerIndicatorEnemy) staggerIndicatorEnemy.gameObject.SetActive(true); };

        // 初始化數值 (此時已經完成資料 Initialize()，可以正確讀到 100/100 等數值)
        UpdateHP(playerHPSlider, playerHPText, player.CurrentHP, player.maxHP);
        UpdateHP(enemyHPSlider, enemyHPText, enemy.CurrentHP, enemy.maxHP);
        UpdateStagger(playerStaggerSlider, playerStaggerText, player.CurrentStagger, player.maxStagger);
        UpdateStagger(enemyStaggerSlider, enemyStaggerText, enemy.CurrentStagger, enemy.maxStagger);

        // 清空初始 Buff
        UpdateBuffs(playerBuffText, new List<string>());
        UpdateBuffs(enemyBuffText, new List<string>());
    }

    private void OnStateChanged(BattleManager.BattleState state)
    {
        if (state == BattleManager.BattleState.PlayerTurn)
            RefreshHandUI();

        if (state == BattleManager.BattleState.Setup)
        {
            if (staggerIndicatorPlayer) staggerIndicatorPlayer.gameObject.SetActive(false);
            if (staggerIndicatorEnemy) staggerIndicatorEnemy.gameObject.SetActive(false);
        }
    }

    private void RefreshHandUI()
    {
        if (handContainer == null || cardButtonPrefab == null) return;

        foreach (Transform child in handContainer) Destroy(child.gameObject);

        foreach (var card in BattleManager.Instance.Player.Hand)
        {
            var btn = Instantiate(cardButtonPrefab, handContainer);
            var label = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (label) label.text = card.cardName;

            var capturedCard = card;
            btn.GetComponent<Button>().onClick.AddListener(() =>
            {
                BattleManager.Instance.PlayerPlayCard(capturedCard);
                RefreshHandUI();
            });
        }
    }

    private void OnUnitStaggered(BattleUnit unit)
    {
        bool isPlayer = unit == BattleManager.Instance.Player;
        AppendLog($"⚠ {unit.unitName} 進入混亂！下回合受雙倍傷害！");
        if (isPlayer && staggerIndicatorPlayer) staggerIndicatorPlayer.gameObject.SetActive(true);
        if (!isPlayer && staggerIndicatorEnemy) staggerIndicatorEnemy.gameObject.SetActive(true);
    }

    private void OnBattleEnded(bool playerWins)
    {
        if (battleEndPanel) battleEndPanel.SetActive(true);
        if (battleEndText) battleEndText.text = playerWins ? "勝利！" : "敗北";
    }

    private void UpdateHP(Slider slider, TextMeshProUGUI label, int cur, int max)
    {
        if (slider) { slider.maxValue = max; slider.value = cur; }
        if (label) label.text = $"{cur} / {max}";
    }

    private void UpdateStagger(Slider slider, TextMeshProUGUI label, int cur, int max)
    {
        if (slider) { slider.maxValue = max; slider.value = cur; }
        if (label) label.text = $"{cur} / {max}";
    }

    private void UpdateBuffs(TextMeshProUGUI label, List<string> buffs)
    {
        if (label == null) return;

        if (buffs.Count == 0)
        {
            label.text = "";
        }
        else
        {
            label.text = string.Join("  ", buffs);
        }
    }

    private void AppendLog(string line)
    {
        _logLines.Add(line);
        if (_logLines.Count > maxLogLines) _logLines.RemoveAt(0);
        if (battleLogText) battleLogText.text = string.Join("\n", _logLines);
    }

    private void OnDestroy()
    {
        if (_bm == null) return;
        _bm.OnBattleLog -= AppendLog;
        _bm.OnStateChanged -= OnStateChanged;
        _bm.OnBattleEnded -= OnBattleEnded;
        _bm.OnUnitStaggered -= OnUnitStaggered;
    }
}