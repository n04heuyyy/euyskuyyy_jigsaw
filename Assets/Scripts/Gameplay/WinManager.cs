using System.Collections.Generic;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WinManager : MonoBehaviour
{
    [Header("Win Display Panel 1 (Standard - Dari ATAS)")]
    [SerializeField] private GameObject standardWinPanel;
    [SerializeField] private CanvasGroup standardWinCanvasGroup; // Tambahkan CanvasGroup di Inspector!
    [SerializeField] private TextMeshProUGUI standardTimeResultText;

    [Header("Win Display Panel 2 (New Record - Dari BAWAH)")]
    [SerializeField] private GameObject fastestRecordWinPanel;
    [SerializeField] private CanvasGroup fastestRecordCanvasGroup; // Tambahkan CanvasGroup di Inspector!
    [SerializeField] private TMP_InputField playerNameInputField;
    [SerializeField] private Button submitRecordButton;

    [Header("Animation Settings")]
    [SerializeField] private float slideOffsetDistance = 500f; // Jarak sliding aman Canvas Space
    [SerializeField] private float animDuration = 0.4f;        // Kecepatan sliding

    private float savedFinalTime = 0f;
    private bool isNewRecordAchieved = false;

    private string activePackFolderPath;
    private string currentGridSetting;
    private string currentSpeedSetting;

    private Coroutine activeAnimCoroutine;
    private Vector3 p1CenterLocalPos;
    private Vector3 p2CenterLocalPos;

    void Awake()
    {
        // Paksa aktifkan sementara di milidetik pertama agar koordinat tengah aslinya terekam akurat
        bool p1OriginallyActive = standardWinPanel != null && standardWinPanel.activeSelf;
        bool p2OriginallyActive = fastestRecordWinPanel != null && fastestRecordWinPanel.activeSelf;

        if (standardWinPanel != null) { standardWinPanel.SetActive(true); p1CenterLocalPos = standardWinPanel.transform.localPosition; }
        if (fastestRecordWinPanel != null) { fastestRecordWinPanel.SetActive(true); p2CenterLocalPos = fastestRecordWinPanel.transform.localPosition; }

        // Kembalikan ke status aslinya
        if (standardWinPanel != null) standardWinPanel.SetActive(p1OriginallyActive);
        if (fastestRecordWinPanel != null) fastestRecordWinPanel.SetActive(p2OriginallyActive);
    }

    void Start()
    {
        // 1. Ambil data konfigurasi di awal
        currentGridSetting = PlayerPrefs.GetString("ChosenGridSizeSetting", "4x4");
        currentSpeedSetting = PlayerPrefs.GetString("ChosenSpeedSetting", "Normy");

        string chosenPackID = PlayerPrefs.GetString("SelectedObjectPackID", "");
        bool isCustom = PlayerPrefs.GetInt("SelectedObjectPackIsCustom", 0) == 1;

        if (isCustom)
            activePackFolderPath = Path.Combine(Application.persistentDataPath, "Object Packs", "Pack_" + chosenPackID);
        else
            activePackFolderPath = Path.Combine(Application.streamingAssetsPath, "Default Packs", chosenPackID);

        // Pengaman awal game: Sembunyikan total di luar layar
        if (standardWinPanel != null) standardWinPanel.SetActive(false);
        if (fastestRecordWinPanel != null) fastestRecordWinPanel.SetActive(false);

        if (standardWinCanvasGroup != null) InitializeCanvasGroup(standardWinCanvasGroup);
        if (fastestRecordCanvasGroup != null) InitializeCanvasGroup(fastestRecordCanvasGroup);

        if (submitRecordButton != null)
        {
            submitRecordButton.onClick.RemoveAllListeners();
            submitRecordButton.onClick.AddListener(SubmitNewFastestRecord);
        }

        if (playerNameInputField != null)
        {
            playerNameInputField.onFocusSelectAll = false;
        }
    }

    private void InitializeCanvasGroup(CanvasGroup group)
    {
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    // Fungsi pusat pemicu selebrasi menang yang dipanggil oleh GameUIManager / GridManager
    public void HandleGameplayWinSystem(float finalTime)
    {
        savedFinalTime = finalTime;
        float previousBestTime = float.MaxValue;
        string bestTimeFilePath = Path.Combine(activePackFolderPath, "best_times.json");

        if (File.Exists(bestTimeFilePath))
        {
            try
            {
                string jsonText = File.ReadAllText(bestTimeFilePath);
                BestTimeCollection collection = JsonUtility.FromJson<BestTimeCollection>(jsonText);
                BestTimeRecord record = collection.records.Find(r => r.gridSize == currentGridSetting && r.speedMode == currentSpeedSetting);
                if (record != null) previousBestTime = record.timeInSeconds;
            }
            catch { }
        }

        isNewRecordAchieved = (savedFinalTime < previousBestTime);

        // Langsung paksa suntik nilai waktu teks di frame yang sama
        FormatTimeTextDisplay(savedFinalTime, standardTimeResultText);

        if (activeAnimCoroutine != null) StopCoroutine(activeAnimCoroutine);
        activeAnimCoroutine = StartCoroutine(ExecuteWinPanelsSequenceRoutine());
    }

    private IEnumerator ExecuteWinPanelsSequenceRoutine()
    {
        // ==================================================================
        // LANGKAH 1: Nyalakan Panel Standard & Slide IN dari ATAS
        // ==================================================================
        if (standardWinPanel != null && standardWinCanvasGroup != null)
        {
            standardWinPanel.SetActive(true);
            Vector3 startPos = p1CenterLocalPos + new Vector3(0f, slideOffsetDistance, 0f);
            yield return StartCoroutine(SlideAndFadeLocalRoutine(standardWinPanel, standardWinCanvasGroup, startPos, p1CenterLocalPos, 0f, 1f));
        }

        // Tunggu 3 detik penuh jeda selebrasi pembacaan waktu
        yield return new WaitForSeconds(3.0f);

        // ==================================================================
        // LANGKAH 2: Chaining Animasi Kelanjutan
        // ==================================================================
        if (isNewRecordAchieved)
        {
            // Jika New Record, luncurkan Panel 2 menyusul meluncur dari BAWAH
            if (fastestRecordWinPanel != null && fastestRecordCanvasGroup != null)
            {
                fastestRecordWinPanel.SetActive(true);
                Vector3 startPos = p2CenterLocalPos + new Vector3(0f, -slideOffsetDistance, 0f);
                yield return StartCoroutine(SlideAndFadeLocalRoutine(fastestRecordWinPanel, fastestRecordCanvasGroup, startPos, p2CenterLocalPos, 0f, 1f));
                
                if (playerNameInputField != null)
                {
                    playerNameInputField.text = "";
                    playerNameInputField.ActivateInputField();
                }
            }
        }
        else
        {
            // Jika TIDAK New Record, usir Panel 1 meluncur kembali ke ATAS keluar layar
            if (standardWinCanvasGroup != null && standardWinPanel != null)
            {
                Vector3 endPos = p1CenterLocalPos + new Vector3(0f, slideOffsetDistance, 0f);
                yield return StartCoroutine(SlideAndFadeLocalRoutine(standardWinPanel, standardWinCanvasGroup, p1CenterLocalPos, endPos, 1f, 0f));
                standardWinPanel.SetActive(false);
            }
        }
    }

    public void SubmitNewFastestRecord()
    {
        if (playerNameInputField == null) return;

        string nameInput = playerNameInputField.text.Trim();

        // KUNCI PROTEKSI NAMA KOSONG: Jika dikosongkan, otomatis ubah menjadi "Anonymous"
        if (string.IsNullOrEmpty(nameInput))
        {
            nameInput = "Anonymous";
        }
        else if (nameInput.Length > 10)
        {
            nameInput = nameInput.Substring(0, 12); // Tetap batasi maksimal 12 huruf
        }
        string bestTimeFilePath = Path.Combine(activePackFolderPath, "best_times.json");
        BestTimeCollection collection = new BestTimeCollection();

        if (File.Exists(bestTimeFilePath))
        {
            try
            {
                string jsonText = File.ReadAllText(bestTimeFilePath);
                collection = JsonUtility.FromJson<BestTimeCollection>(jsonText);
            }
            catch { }
        }

        // Hapus rekor lama untuk kombinasi setup ini agar tidak duplikat
        collection.records.RemoveAll(r => r.gridSize == currentGridSetting && r.speedMode == currentSpeedSetting);

        // Tambahkan rekor baru
        BestTimeRecord newRecord = new BestTimeRecord
        {
            gridSize = currentGridSetting,
            speedMode = currentSpeedSetting,
            timeInSeconds = savedFinalTime,
            playerName = nameInput
        };
        collection.records.Add(newRecord);

        try
        {
            string updatedJson = JsonUtility.ToJson(collection, true);
            File.WriteAllText(bestTimeFilePath, updatedJson);
        }
        catch { }

        // KUNCI PENUTUP: Hilangkan kedua panel sekaligus ke arah asalnya masing-masing secara bersamaan
        StartCoroutine(DismissPanelsRoutine());
    }

    private IEnumerator DismissPanelsRoutine()
    {
        Coroutine p1 = null;
        Coroutine p2 = null;

        if (standardWinPanel != null && standardWinCanvasGroup != null) 
        {
            Vector3 endPos = p1CenterLocalPos + new Vector3(0f, slideOffsetDistance, 0f);
            p1 = StartCoroutine(SlideAndFadeLocalRoutine(standardWinPanel, standardWinCanvasGroup, p1CenterLocalPos, endPos, 1f, 0f));
        }
        
        if (fastestRecordWinPanel != null && fastestRecordCanvasGroup != null) 
        {
            Vector3 endPos = p2CenterLocalPos + new Vector3(0f, -slideOffsetDistance, 0f);
            p2 = StartCoroutine(SlideAndFadeLocalRoutine(fastestRecordWinPanel, fastestRecordCanvasGroup, p2CenterLocalPos, endPos, 1f, 0f));
        }

        if (p1 != null) yield return p1;
        if (p2 != null) yield return p2;

        if (standardWinPanel != null) standardWinPanel.SetActive(false);
        if (fastestRecordWinPanel != null) fastestRecordWinPanel.SetActive(false);

        isNewRecordAchieved = false;
    }

    // Sub-Coroutine Core Matematika Lerp untuk Transisi Alpha CanvasGroup
    private IEnumerator SlideAndFadeLocalRoutine(GameObject panel, CanvasGroup group, Vector3 start, Vector3 target, float startAlpha, float endAlpha)
    {
        float elapsed = 0f;
        panel.transform.localPosition = start;
        group.alpha = startAlpha;

        if (endAlpha > 0.5f)
        {
            group.interactable = true;
            group.blocksRaycasts = true;
        }
        else
        {
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animDuration);
            panel.transform.localPosition = Vector3.Lerp(start, target, t);
            group.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            yield return null;
        }

        panel.transform.localPosition = target;
        group.alpha = endAlpha;
    }

    private void FormatTimeTextDisplay(float time, TextMeshProUGUI targetText)
    {
        if (targetText == null) return;
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        targetText.text = string.Format("Your final time: {0:00}:{1:00}", minutes, seconds);
        
        // Paksa TextMeshPro melakukan pembangunan ulang mesh huruf seketika tanpa delay frame
        targetText.ForceMeshUpdate(true);
    }
}