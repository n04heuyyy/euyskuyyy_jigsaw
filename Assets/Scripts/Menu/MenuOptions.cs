using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class MenuOptions : MonoBehaviour
{
    [Header("UI Panel Animation Setup")]
    [SerializeField] private RectTransform optionsPanelRect; 
    [SerializeField] private Button backButton;             
    [SerializeField] private float hidePositionY = -1080f;    
    [SerializeField] private float targetShowY = 0f;         
    [SerializeField] private float animDuration = 0.4f;

    [Header("Audio Controllers")]
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;
    public AudioSource mainMenuBGM;

    [Header("SFX Preview")]
    public AudioSource previewAudioSource;
    public AudioClip previewSFXClip;

    [Header("Graphics & Performance")]
    public TMP_Dropdown fpsDropdown;

    private Coroutine activeAnimCoroutine;

    void Start()
    {
        // Ambil data volume musik yang tersimpan
        float savedMusic = PlayerPrefs.GetFloat("MusicVolume", 0.75f);
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.value = savedMusic;
            musicVolumeSlider.onValueChanged.RemoveAllListeners();
            musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);
        }
        
        // Begitu main menu dibuka, langsung apply volume
        if (mainMenuBGM != null) mainMenuBGM.volume = savedMusic;

        // Ambil data volume SFX yang tersimpan
        float savedSFX = PlayerPrefs.GetFloat("SFXVolume", 0.75f);
        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = savedSFX;
            sfxVolumeSlider.onValueChanged.RemoveAllListeners();
            sfxVolumeSlider.onValueChanged.AddListener(SaveSFXVolume);
        }
        // Terapkan volume SFX tersimpan ke komponen target AudioSource preview sejak awal game dibuka
        if (previewAudioSource != null) previewAudioSource.volume = savedSFX;

        // Setup Dropdown FPS
        if (fpsDropdown != null)
        {
            fpsDropdown.ClearOptions();
            var options = new System.Collections.Generic.List<string> { "30 FPS", "60 FPS", "Unlimited" };
            fpsDropdown.AddOptions(options);
            fpsDropdown.value = PlayerPrefs.GetInt("FPSSetting", 1);
            fpsDropdown.onValueChanged.RemoveAllListeners();
            fpsDropdown.onValueChanged.AddListener(SetFPSLimit);
            ApplyFPSLimit(fpsDropdown.value);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(CloseOptionsPanel);
        }

        // Inisialisasi posisi awal panel Options agar tersembunyi di luar layar saat game dibuka
        if (optionsPanelRect != null)
        {
            optionsPanelRect.anchoredPosition = new Vector2(optionsPanelRect.anchoredPosition.x, hidePositionY);
        }
    }

    public void SetMusicVolume(float value) // Simpan volume musik
    {
        PlayerPrefs.SetFloat("MusicVolume", value);
        PlayerPrefs.Save();
        // Langsung ubah volume musik menu utama secara realtime
        if (mainMenuBGM != null) mainMenuBGM.volume = value;
    }

    public void SaveSFXVolume(float value) // Simpan volume SFX
    {
        PlayerPrefs.SetFloat("SFXVolume", value);
        PlayerPrefs.Save();

        // KUNCI FIX: Ubah properti volume AudioSource preview secara real-time saat slider digeser!
        if (previewAudioSource != null)
        {
            previewAudioSource.volume = value;
        }
    }

    // Putar preview sfx ketika melepas pointer (menggunakan event trigger pointer up)
    public void PlaySFXPreviewOnPointerUp()
    {
        if (previewAudioSource != null && previewSFXClip != null)
        {
            // Ambil volume yang disimpan saat digeser
            float currentVolume = PlayerPrefs.GetFloat("SFXVolume", 0.75f);
            
            // Setel volume audio source
            previewAudioSource.volume = currentVolume;

            // Putar suara sekali
            previewAudioSource.PlayOneShot(previewSFXClip, currentVolume);
        }
    }

    public void SetFPSLimit(int index)
    {
        PlayerPrefs.SetInt("FPSSetting", index);
        PlayerPrefs.Save();
        ApplyFPSLimit(index);
    }

    void ApplyFPSLimit(int index)
    {
        if (index == 0) Application.targetFrameRate = 30;
        else if (index == 1) Application.targetFrameRate = 60;
        else Application.targetFrameRate = -1; // Tanpa batasan (Unlimited)
    }

    public void OpenOptionsPanel()
    {
        TriggerPanelAnimation(targetShowY);
    }

    public void CloseOptionsPanel()
    {
        MainMenuUIManager menuUI = Object.FindFirstObjectByType<MainMenuUIManager>();
        if (menuUI != null)
        {
            menuUI.SetSidebarInteractable(true, false);
        }

        TriggerPanelAnimation(hidePositionY);
    }

    private void TriggerPanelAnimation(float targetY)
    {
        if (optionsPanelRect == null) return;
        if (activeAnimCoroutine != null) StopCoroutine(activeAnimCoroutine);
        activeAnimCoroutine = StartCoroutine(SlidePanelRoutine(targetY));
    }

    private IEnumerator SlidePanelRoutine(float targetY)
    {
        float elapsed = 0f;
        Vector2 startPos = optionsPanelRect.anchoredPosition;
        Vector2 targetPos = new Vector2(startPos.x, targetY);

        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animDuration);
            optionsPanelRect.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }
        optionsPanelRect.anchoredPosition = targetPos;
    }
}