using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using TMPro;

/// <summary>
/// 全域暫停與設定管理器 (Limbus Company 風格)
/// 負責 ESC 暫停、頁籤切換、音量控制與畫質設定。
/// </summary>
public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance { get; private set; }

    [Header("核心 UI")]
    public GameObject pausePanel; // 整個半透明黑底+設定視窗的父物件

    [Header("頁籤內容 (右側面板)")]
    public GameObject graphicsPanel;
    public GameObject volumePanel;

    [Header("音效控制")]
    public AudioMixer mainAudioMixer;
    public Slider masterSlider;
    public Slider bgmSlider;
    public Slider sfxSlider;

    [Header("畫質與畫面")]
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;
    public TMP_Dropdown qualityDropdown; // 低、中、高

    private bool _isPaused = false;
    private Resolution[] _resolutions;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // 確保遊戲開始時設定面板是關閉的
        if (pausePanel) pausePanel.SetActive(false);
    }

    private void Start()
    {
        InitializeResolutions();
        LoadSettings();

        // 預設開啟第一個頁籤 (圖形)
        SwitchTab("Graphics");
    }

    private void Update()
    {
        // 偵測 ESC 鍵
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

    // ── 暫停與恢復 ──
    public void TogglePause()
    {
        _isPaused = !_isPaused;
        pausePanel.SetActive(_isPaused);

        // Time.timeScale = 0 會讓所有受時間影響的動作 (動畫、物理、Update) 暫停
        Time.timeScale = _isPaused ? 0f : 1f;
    }

    // ── 頁籤切換 (供左側按鈕呼叫) ──
    public void SwitchTab(string tabName)
    {
        graphicsPanel.SetActive(tabName == "Graphics");
        volumePanel.SetActive(tabName == "Volume");
    }

    // ── 音量控制 ──
    // 注意：AudioMixer 使用對數 (dB)，所以 Slider 的值必須是 0.0001 到 1
    public void SetMasterVolume(float value)
    {
        mainAudioMixer.SetFloat("MasterVolume", Mathf.Log10(value) * 20);
        PlayerPrefs.SetFloat("MasterVol", value);
    }

    public void SetBGMVolume(float value)
    {
        mainAudioMixer.SetFloat("BGMVolume", Mathf.Log10(value) * 20);
        PlayerPrefs.SetFloat("BGMVol", value);
    }

    public void SetSFXVolume(float value)
    {
        mainAudioMixer.SetFloat("SFXVolume", Mathf.Log10(value) * 20);
        PlayerPrefs.SetFloat("SFXVol", value);
    }

    // ── 畫質與解析度控制 ──
    private void InitializeResolutions()
    {
        _resolutions = Screen.resolutions;
        if (resolutionDropdown == null) return;

        resolutionDropdown.ClearOptions();
        List<string> options = new List<string>();
        int currentResIndex = 0;

        for (int i = 0; i < _resolutions.Length; i++)
        {
            string option = _resolutions[i].width + " x " + _resolutions[i].height;
            // 避免加入重複的解析度
            if (!options.Contains(option)) options.Add(option);

            if (_resolutions[i].width == Screen.currentResolution.width &&
                _resolutions[i].height == Screen.currentResolution.height)
            {
                currentResIndex = i;
            }
        }
        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResIndex;
        resolutionDropdown.RefreshShownValue();
    }

    public void SetResolution(int resIndex)
    {
        Resolution res = _resolutions[resIndex];
        Screen.SetResolution(res.width, res.height, Screen.fullScreen);
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
    }

    public void SetQuality(int qualityIndex)
    {
        QualitySettings.SetQualityLevel(qualityIndex);
        PlayerPrefs.SetInt("QualityLevel", qualityIndex);
    }

    // ── 存檔與讀取設定 ──
    private void LoadSettings()
    {
        // 讀取音量 (預設為 0.8)
        if (masterSlider) { masterSlider.value = PlayerPrefs.GetFloat("MasterVol", 0.8f); SetMasterVolume(masterSlider.value); }
        if (bgmSlider) { bgmSlider.value = PlayerPrefs.GetFloat("BGMVol", 0.8f); SetBGMVolume(bgmSlider.value); }
        if (sfxSlider) { sfxSlider.value = PlayerPrefs.GetFloat("SFXVol", 0.8f); SetSFXVolume(sfxSlider.value); }

        // 讀取畫質 (預設為高畫質，Unity 預設最高通常是 index 2)
        if (qualityDropdown)
        {
            qualityDropdown.value = PlayerPrefs.GetInt("QualityLevel", QualitySettings.GetQualityLevel());
            SetQuality(qualityDropdown.value);
        }

        if (fullscreenToggle) fullscreenToggle.isOn = Screen.fullScreen;
    }
}