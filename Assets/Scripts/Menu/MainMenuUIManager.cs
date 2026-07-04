using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenuUIManager : MonoBehaviour
{
    [Header("Grid Size Panel Configuration")]
    [SerializeField] private TextMeshProUGUI gridSizeText;
    [SerializeField] private Button gridLeftButton;
    [SerializeField] private Button gridRightButton;
    private string[] gridOptions = { "2x2", "3x3", "4x4", "6x6" };
    private int currentGridIndex = 0;

    [Header("Speed Panel Configuration")]
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private Button speedLeftButton;
    [SerializeField] private Button speedRightButton;
    private string[] speedOptions = { "Slowy", "Normy", "Speedy", "GAS GAS GAS" };
    private float[] speedValues = { 1.5f, 3.0f, 4.5f, 6.0f };
    private int currentSpeedIndex = 0;

    [Header("Dynamic Background System")]
    [SerializeField] private Image menuBackgroundImage; 
    [SerializeField] private Button changeBGButton;
    [SerializeField] private List<Sprite> backgroundSprites = new List<Sprite>(); 
    private int currentBGIndex = 0;

    [Header("Core Sidebar Action Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button quitButton;

    [Header("Panels References")]
    [SerializeField] private MenuOptions optionsPanelScript; 
    [SerializeField] private ObjectPreviewManager previewManager; 
    [SerializeField] private FadeManager fadeManager;

    [Header("Transition Audio Setup")]
    [SerializeField] private AudioSource menuAudioSource; 
    [SerializeField] private AudioClip sceneFadeOutSFX;   

    void Start()
    {
        // Cek apakah pemain sudah pernah memilih background sebelumnya
        if (PlayerPrefs.HasKey("ChosenBackgroundIndex"))
        {
            currentBGIndex = PlayerPrefs.GetInt("ChosenBackgroundIndex", 0);
        }
        else currentBGIndex = 0; // Default awal 

        // Muat memori pilihan indeks terakhir
        currentGridIndex = PlayerPrefs.GetInt("SavedGridSizeIndex", 2); // Default ke 4x4
        UpdateGridSizeValue(0); 
        UpdateBackgroundDisplay();
        currentSpeedIndex = PlayerPrefs.GetInt("SavedSpeedModeIndex", 1); // Default ke Normy
        UpdateSpeedModeValue(0);

        // Pasang button listener
        gridLeftButton.onClick.RemoveAllListeners();
        gridLeftButton.onClick.AddListener(() => UpdateGridSizeValue(-1));

        gridRightButton.onClick.RemoveAllListeners();
        gridRightButton.onClick.AddListener(() => UpdateGridSizeValue(1));
        
        speedLeftButton.onClick.RemoveAllListeners();
        speedLeftButton.onClick.AddListener(() => UpdateSpeedModeValue(-1));

        speedRightButton.onClick.RemoveAllListeners();
        speedRightButton.onClick.AddListener(() => UpdateSpeedModeValue(1));

        changeBGButton.onClick.RemoveAllListeners();
        changeBGButton.onClick.AddListener(NextBackground);
        
        optionsButton.onClick.RemoveAllListeners();
        optionsButton.onClick.AddListener(OpenOptionsPanel);
        
        quitButton.onClick.RemoveAllListeners();
        quitButton.onClick.AddListener(QuitGame);
        
        startButton.onClick.RemoveAllListeners();
        startButton.onClick.AddListener(StartGameplay);

        if (fadeManager != null) StartCoroutine(fadeManager.FadeInRoutine());
    }

    // Hubungkan fungsi ini ke tombol panah kiri (-1) dan kanan (+1) di Inspector
    public void UpdateGridSizeValue(int direction)
    {
        currentGridIndex = (currentGridIndex + direction) % gridOptions.Length;
        if (currentGridIndex < 0) currentGridIndex += gridOptions.Length; 

        // Simpan pilihan secara permanen
        PlayerPrefs.SetInt("SavedGridSizeIndex", currentGridIndex);
        PlayerPrefs.SetString("ChosenGridSizeSetting", gridOptions[currentGridIndex]);
        PlayerPrefs.Save();

        // Tampilkan teks visual di antara panah (Misal: "6x6")
        if (gridSizeText != null) gridSizeText.text = gridOptions[currentGridIndex];

        // Segarkan tampilan record time secara real-time
        ObjectPreviewManager prevManager = Object.FindFirstObjectByType<ObjectPreviewManager>();
        if (prevManager != null) prevManager.UpdateBestTimeDisplay();
    }

    public void UpdateSpeedModeValue(int direction)
    {
        currentSpeedIndex = (currentSpeedIndex + direction) % speedOptions.Length;
        if (currentSpeedIndex < 0) currentSpeedIndex += speedOptions.Length;

        // Simpan nilai float fisiknya secara langsung saat tombol diganti
        PlayerPrefs.SetInt("SavedSpeedModeIndex", currentSpeedIndex);
        PlayerPrefs.SetString("ChosenSpeedSetting", speedOptions[currentSpeedIndex]);
        PlayerPrefs.SetFloat("ChosenSpeedValue", speedValues[currentSpeedIndex]); 
        PlayerPrefs.Save();

        // Tampilkan teks visual di antara panah
        if (speedText != null) speedText.text = speedOptions[currentSpeedIndex];

        // Panggil fungsi bawaan dari ObjectPreviewManager asli milikmu
        if (previewManager != null)
        {
            previewManager.UpdateAllObjectsSpeed(speedValues[currentSpeedIndex]);
            previewManager.UpdateBestTimeDisplay();
        }
    }

    public void SetSidebarInteractable(bool state, bool excludeSpeed = false)
    {
        if (startButton != null) startButton.interactable = state;
        if (optionsButton != null) optionsButton.interactable = state;
        if (quitButton != null) quitButton.interactable = state;
        if (changeBGButton != null) changeBGButton.interactable = state;

        // Blokir panel Grid Size
        if (gridLeftButton != null) gridLeftButton.interactable = state;
        if (gridRightButton != null) gridRightButton.interactable = state;

        // Blokir panel Speed Mode (bisa dikecualikan jika excludeSpeed bernilai true)
        if (speedLeftButton != null) speedLeftButton.interactable = excludeSpeed ? true : state;
        if (speedRightButton != null) speedRightButton.interactable = excludeSpeed ? true : state;
    }

    void NextBackground()
    {
        if (backgroundSprites.Count == 0) return;

        // Naikkan indeks, jika melampaui jumlah total, reset kembali ke 0 (Looping Format)
        currentBGIndex = (currentBGIndex + 1) % backgroundSprites.Count;
        UpdateBackgroundDisplay();
    }

    void UpdateBackgroundDisplay()
    {
        if (backgroundSprites.Count > 0 && menuBackgroundImage != null)
        {
            menuBackgroundImage.sprite = backgroundSprites[currentBGIndex];
        }
    }

    void OpenOptionsPanel()
    {
        if (optionsPanelScript != null)
        {
            SetSidebarInteractable(false, false);
            optionsPanelScript.OpenOptionsPanel();
        }
    }

    void QuitGame()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
            // Dalam mode web ini akan memunculkan pesan lol XD
        #endif
    }

    void StartGameplay()
    {
        // Potong string berdasarkan huruf 'x' untuk mendapatkan panjang sisi murni
        string[] parts = gridOptions[currentGridIndex].Split('x');
        int sideSize = 4;
        if (parts.Length > 0)
        {
            int.TryParse(parts[0], out sideSize); // Menghasilkan angka 2, 3, 4, atau 6
        }

        PlayerPrefs.SetInt("ChosenGridSize", sideSize);
        PlayerPrefs.SetInt("ChosenBackgroundIndex", currentBGIndex);

        // Kita dongkrak nilai kecepatan gerak khusus di gameplay agar lebih cepat dan seimbang dengan preview menu
        // Index 0 (Slowy) = 3.5f, Index 1 (Normy) = 6.5f, Index 2 (Speedy) = 9.5f, Index 3 (GAS GAS GAS) = 13.0f
        float gameplaySpeedValue = 6.5f; 
        if (currentSpeedIndex == 0) gameplaySpeedValue = 3.5f;
        else if (currentSpeedIndex == 1) gameplaySpeedValue = 6.5f;
        else if (currentSpeedIndex == 2) gameplaySpeedValue = 9.5f;
        else if (currentSpeedIndex == 3) gameplaySpeedValue = 13.0f;

        PlayerPrefs.SetFloat("ChosenSpeedValue", gameplaySpeedValue);
        
        // Kirim ID Unik Paket aktif saat ini ke gameplay
        CustomPackManager packManager = Object.FindFirstObjectByType<CustomPackManager>();
        if (packManager != null)
        {
            ObjectPackConfig activePack = packManager.GetActivePackConfig();
            if (activePack != null)
            {
                PlayerPrefs.SetString("SelectedObjectPackID", activePack.packID);
                PlayerPrefs.SetInt("SelectedObjectPackIsCustom", activePack.isCustom ? 1 : 0);
            }
        }
        PlayerPrefs.Save();

        if (packManager != null && GameplayDataBridge.Instance != null)
        {
            ObjectPackConfig selected = packManager.GetActivePackConfig();
            if (selected != null)
            {
                GameplayDataBridge.Instance.isPlayingCustomPack = selected.isCustom;
                CustomPack convertedPack = new CustomPack { packID = selected.packID, packName = selected.packName };
                foreach (var item in selected.items) convertedPack.items.Add(item);
                GameplayDataBridge.Instance.activeCustomPack = convertedPack;
            }
        }

        StartCoroutine(StartRoutine());
    }

    IEnumerator StartRoutine()
    {
        Time.timeScale = 1f;

        // Putar audio transisi secara utuh mengikuti volume SFX menu utama
        if (menuAudioSource != null && sceneFadeOutSFX != null)
        {
            float savedSFXVol = PlayerPrefs.GetFloat("SFXVolume", 0.75f);
            menuAudioSource.PlayOneShot(sceneFadeOutSFX, savedSFXVol);
        }

        // Berikan jeda penahan 0.4 detik agar klip transisi sempat bersuara nyaring 
        // sebelum layar menggelap dan berpindah scene
        yield return new WaitForSeconds(0.4f);

        if (fadeManager != null)
        {
            yield return StartCoroutine(fadeManager.FadeOutRoutine());
        }
        else
        {
            yield return new WaitForSeconds(0.6f);
        }

        SceneManager.LoadScene("Euyskuyyy Gameplay"); 
    }

    public void OnCustomPanelOpened()
    {
        if (startButton != null)
        {
            startButton.interactable = false; // Blokir tombol start saat merakit
        }
    }

    // Panggil fungsi ini tepat saat tombol save atau cancel diklik
    public void OnCustomPanelClosed()
    {
        if (startButton != null)
        {
            startButton.interactable = true; // Buka kembali tombol start setelah panel ditutup
        }
    }
}