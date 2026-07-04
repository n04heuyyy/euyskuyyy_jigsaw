using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using UnityEngine.Networking;

public class ObjectPreviewManager : MonoBehaviour
{
    [Header("Render Texture Setup")]
    public int textureResolution = 1024;
    [SerializeField] private RawImage uiTargetRawImage; 

    [Header("Best Time UI Area")]
    [SerializeField] private TextMeshProUGUI bestTimeTextDisplay;         
    [SerializeField] private TextMeshProUGUI bestTimePlayerNameTextDisplay; 
    [SerializeField] private Button clearBestTimeButton;

    [Header("Clear Leaderboard Confirmation UI (Sliding System)")]
    [SerializeField] private RectTransform clearBestTimeConfirmationPopup;
    [SerializeField] private Button confirmClearYesButton; 
    [SerializeField] private Button confirmClearNoButton; 
    
    [Header("Sliding Animation Parameters")]
    [SerializeField] private float hidePositionY = -1080f; 
    [SerializeField] private float targetShowY = 0f;
    [SerializeField] private float animDuration = 0.4f;

    private List<LocalBoundEnforcer> activeEnforcers = new List<LocalBoundEnforcer>();
    private RenderTexture previewRenderTexture;
    private GameObject currentAnchor; 
    private Dictionary<int, GameObject> customPreviewObjectsMap = new Dictionary<int, GameObject>();
    private Dictionary<RectTransform, Coroutine> activePopupCoroutines = new Dictionary<RectTransform, Coroutine>();

    private const int PREVIEW_LAYER = 31;
    private string rootCustomPacksPath;
    private string rootDefaultPacksPath;

    [Header("Manager Connection")]
    [SerializeField] private CustomPackManager customPackManager;
    
    void Start()
    {
        rootCustomPacksPath = Path.Combine(Application.persistentDataPath, "Object Packs");
        rootDefaultPacksPath = Path.Combine(Application.streamingAssetsPath, "Default Packs");

        if (clearBestTimeButton != null) {
            clearBestTimeButton.onClick.RemoveAllListeners();
            clearBestTimeButton.onClick.AddListener(TriggerClearBestTimeConfirmation);
        }

        if (confirmClearYesButton != null) {
            confirmClearYesButton.onClick.RemoveAllListeners();
            confirmClearYesButton.onClick.AddListener(ConfirmClearBestTimeYes);
        }

        if (confirmClearNoButton != null) {
            confirmClearNoButton.onClick.RemoveAllListeners();
            confirmClearNoButton.onClick.AddListener(ConfirmClearBestTimeNo);
        }

        if (clearBestTimeConfirmationPopup != null)
        {
            clearBestTimeConfirmationPopup.gameObject.SetActive(true);
            clearBestTimeConfirmationPopup.anchoredPosition = new Vector2(clearBestTimeConfirmationPopup.anchoredPosition.x, hidePositionY);
        }

        if (previewRenderTexture == null)
        {
            previewRenderTexture = new RenderTexture(textureResolution, textureResolution, 24, RenderTextureFormat.ARGB32);
            previewRenderTexture.useMipMap = false;
            previewRenderTexture.filterMode = FilterMode.Point;
            previewRenderTexture.Create();
        }
        if (uiTargetRawImage != null) uiTargetRawImage.texture = previewRenderTexture;
    }

    public void LoadObjectPack(int packIndex)
    {
        CustomPackManager packManager = Object.FindFirstObjectByType<CustomPackManager>();
        if (packManager == null || packManager.allLoadedPacks == null || packManager.allLoadedPacks.Count == 0) return;

        int safeIndex = Mathf.Clamp(packIndex, 0, packManager.allLoadedPacks.Count - 1);
        ObjectPackConfig config = packManager.allLoadedPacks[safeIndex];
        if (config == null) return;

        // Baca langsung dari simpanan custom pack di appdata (custom)
        string packFolderPath = "";
        if (config.isCustom) {
            packFolderPath = Path.Combine(rootCustomPacksPath, "Pack_" + config.packID);
        } else {
            // Baca dari folder StreamingAssets (default)
            packFolderPath = Path.Combine(Application.streamingAssetsPath, "Default Packs", config.packID);
        }

        CustomItemData[] fixedSizeArray = new CustomItemData[3] { null, null, null };
        for (int i = 0; i < Mathf.Min(config.items.Count, 3); i++) fixedSizeArray[i] = config.items[i];

        ProcessObjectData(fixedSizeArray, packFolderPath);
        UpdateBestTimeDisplay();
    }
    
    public void ProcessObjectData(CustomItemData[] customItems, string packFolderPath)
    {
        // Hancurkan kandang lama dan bersihkan database map kustom
        if (currentAnchor != null) Destroy(currentAnchor);
        activeEnforcers.Clear();
        customPreviewObjectsMap.Clear(); 

        currentAnchor = new GameObject("Menu Preview Anchor");
        currentAnchor.transform.position = new Vector3(500f, -50f, 0f);

        // Loop menelusuri seluruh 3 slot potensial secara bersamaan
        for (int i = 0; i < customItems.Length; i++)
        {
            CustomItemData itemData = customItems[i];
            
            // Jika slot data kosong atau belum diupload gambarnya, lewati
            if (itemData == null || string.IsNullOrEmpty(itemData.imageFileName)) continue;

            string fullImagePath = Path.Combine(packFolderPath, itemData.imageFileName);

            // Buat struktur objek baru untuk slot aktif ini
            GameObject spawnedTarget = new GameObject($"Preview Object Slot_{i}");
            spawnedTarget.transform.SetParent(currentAnchor.transform);

            spawnedTarget.layer = PREVIEW_LAYER;
            spawnedTarget.transform.localPosition = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.4f, 0.4f), 0f);

            SpriteRenderer sr = spawnedTarget.AddComponent<SpriteRenderer>();
            StartCoroutine(ApplySpriteRuntimeAsync(sr, fullImagePath, spawnedTarget, itemData));

            Rigidbody2D rb = spawnedTarget.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.angularDamping = 0f;

            CircleCollider2D col = spawnedTarget.AddComponent<CircleCollider2D>();
            col.isTrigger = true;

            LocalBoundEnforcer enforcer = spawnedTarget.AddComponent<LocalBoundEnforcer>();
            enforcer.SetupBounds(2.0f, 1.4f);
            
            float startDeviation = itemData.maxRotSpeed - 0.5f;
            enforcer.maxRotationSpeed = (Mathf.Abs(startDeviation) < 0.04f) ? 0f : startDeviation * -360f; 
            enforcer.useCustomPanelRotation = true; 
    
            activeEnforcers.Add(enforcer);
            customPreviewObjectsMap[i] = spawnedTarget;
        }

        // Ambil nilai kecepatan gerak linier dulu
        float currentSpeedValue = PlayerPrefs.GetFloat("ChosenSpeedValue", 3.0f);

        // Ambil index speed terakhir yang tersimpan, lalu konversikan ke float speed gerak linier-nya
        int savedSpeedIndex = PlayerPrefs.GetInt("SavedSpeedModeIndex", 1); // Default ke 1 (Normy)
        if (savedSpeedIndex == 0) currentSpeedValue = 1.5f;      // Slowy
        else if (savedSpeedIndex == 1) currentSpeedValue = 3.0f; // Normy
        else if (savedSpeedIndex == 2) currentSpeedValue = 4.5f; // Speedy
        else if (savedSpeedIndex == 3) currentSpeedValue = 6.0f; // GAS GAS GAS

        // Terapkan langsung ke semua enforcer agar kecepatannya tidak kembali melambat
        UpdateAllObjectsSpeed(currentSpeedValue);

        // Buat preview target camera
        GameObject camObj = new GameObject("PreviewTargetCamera");
        camObj.transform.SetParent(currentAnchor.transform);
        camObj.transform.localPosition = new Vector3(0f, 0f, -10f);
        Camera previewCam = camObj.AddComponent<Camera>();
        previewCam.cullingMask = (1 << PREVIEW_LAYER); 
        if (Camera.main != null) Camera.main.cullingMask &= ~(1 << PREVIEW_LAYER);

        previewCam.targetTexture = previewRenderTexture;
        previewCam.orthographic = true;
        previewCam.orthographicSize = 1.3f;
        previewCam.clearFlags = CameraClearFlags.SolidColor;
        previewCam.backgroundColor = new Color(1f, 1f, 1f, 0.2f);

        if (uiTargetRawImage != null) uiTargetRawImage.texture = previewRenderTexture;
    }

    // Coroutine Asinkron untuk membaca file lokal maupun internal StreamingAssets secara seragam
    private IEnumerator ApplySpriteRuntimeAsync(SpriteRenderer sr, string path, GameObject target, CustomItemData itemData)
    {
        // Jika path hancur atau nama file kosong akibat post-save cleanup, hentikan
        if (string.IsNullOrEmpty(path) || path.EndsWith("/") || path.EndsWith("\\"))
        {
            yield break;
        }

        // Sembunyikan visual objek terlebih dahulu selama proses download berjalan
        target.transform.localScale = Vector3.zero;

        string finalUrl = path;
        if (path.Contains("StreamingAssets"))
        {
            #if UNITY_EDITOR || UNITY_STANDALONE_WIN
            finalUrl = "file://" + path;
            #else
            finalUrl = path;
            #endif
        }
        else if (!path.Contains("://"))
        {
            finalUrl = "file://" + path;
        }

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(finalUrl))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success && sr != null)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                texture.filterMode = FilterMode.Point;
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                sr.sprite = sprite;

                // Hitung normalisasi dimensi ukuran setelah sprite sukses termuat sempurna
                float maxSpriteDimension = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
                float normalizer = 1.0f / maxSpriteDimension;
                
                float targetClampedSize = Mathf.Lerp(1.0f, 2.0f, itemData.sizeScale); 
                float finalScale = normalizer * targetClampedSize;
                
                // Terapkan langsung ukuran akhir secara instan
                target.transform.localScale = new Vector3(finalScale, finalScale, 1f);

                LocalBoundEnforcer enforcer = target.GetComponent<LocalBoundEnforcer>();
                if (enforcer != null) enforcer.ApplyCustomPanelRotation();
            } 
        }
    }

    public void UpdateBestTimeDisplay()
    {
        if (bestTimeTextDisplay == null) return;

        CustomPackManager packManager = Object.FindFirstObjectByType<CustomPackManager>();
        if (packManager == null || packManager.allLoadedPacks.Count == 0) {
            bestTimeTextDisplay.text = "Record: --:--";
            if (clearBestTimeButton != null) clearBestTimeButton.interactable = false;
            return;
        }

        ObjectPackConfig activePack = packManager.GetActivePackConfig();
        if (activePack == null) return;

        // Samakan nama key sesuai ingatan MainMenuUIManager
        string currentGrid = PlayerPrefs.GetString("ChosenGridSizeSetting", "4x4"); 
        string currentSpeed = PlayerPrefs.GetString("ChosenSpeedSetting", "Normy");

        string packFolderPath = activePack.isCustom ? 
            Path.Combine(Application.persistentDataPath, "Object Packs", "Pack_" + activePack.packID) :
            Path.Combine(Application.streamingAssetsPath, "Default Packs", activePack.packID);

        string bestTimeFilePath = Path.Combine(packFolderPath, "best_times.json");
        if (File.Exists(bestTimeFilePath))
        {
            try
            {
                string jsonText = File.ReadAllText(bestTimeFilePath);
                BestTimeCollection collection = JsonUtility.FromJson<BestTimeCollection>(jsonText);
                BestTimeRecord record = collection.records.Find(r => r.gridSize == currentGrid && r.speedMode == currentSpeed);
                
                if (record != null)
                {
                    int minutes = Mathf.FloorToInt(record.timeInSeconds / 60F);
                    int seconds = Mathf.FloorToInt(record.timeInSeconds % 60F);
                    
                    bestTimeTextDisplay.text = $"Record: {minutes:00}:{seconds:00}";
                    if (bestTimePlayerNameTextDisplay != null)
                    {
                        bestTimePlayerNameTextDisplay.text = $"by {record.playerName}";
                    }
                    
                    if (clearBestTimeButton != null) clearBestTimeButton.interactable = true;
                    return;
                }
            }
            catch { }
        }

        bestTimeTextDisplay.text = "Record: --:--";
        if (bestTimePlayerNameTextDisplay != null) bestTimePlayerNameTextDisplay.text = "-";
        if (clearBestTimeButton != null) clearBestTimeButton.interactable = false;
    }

    // Munculkan popup notifikasi clear ketika tombol clear ditekan
    public void TriggerClearBestTimeConfirmation()
    {
        TriggerPopupAnimation(clearBestTimeConfirmationPopup, targetShowY);

        MainMenuUIManager menuUI = Object.FindFirstObjectByType<MainMenuUIManager>();
        if (menuUI != null) menuUI.SetSidebarInteractable(false, false); // Blokir tombol luar
    }

    // Konfirmasi hapus
    public void ConfirmClearBestTimeYes()
    {
        ClearRecordTime();

        // Sembunyikan kembali popup ke bawah layar
        TriggerPopupAnimation(clearBestTimeConfirmationPopup, hidePositionY);

        // Pulihkan interaksi tombol luar
        MainMenuUIManager menuUI = Object.FindFirstObjectByType<MainMenuUIManager>();
        if (menuUI != null) menuUI.SetSidebarInteractable(true, false);
    }

    // Batalkan
    public void ConfirmClearBestTimeNo()
    {
        TriggerPopupAnimation(clearBestTimeConfirmationPopup, hidePositionY);

        MainMenuUIManager menuUI = Object.FindFirstObjectByType<MainMenuUIManager>();
        if (menuUI != null) menuUI.SetSidebarInteractable(true, false);
    }

    // Bersihkan data rekor di JSON
    private void ClearRecordTime()
    {
        CustomPackManager packManager = Object.FindFirstObjectByType<CustomPackManager>();
        if (packManager == null || packManager.allLoadedPacks.Count == 0) return;

        ObjectPackConfig activePack = packManager.GetActivePackConfig();
        if (activePack == null) return;

        string packFolderPath = activePack.isCustom ? 
            Path.Combine(Application.persistentDataPath, "Object Packs", "Pack_" + activePack.packID) :
            Path.Combine(Application.streamingAssetsPath, "Default Packs", activePack.packID);

        string bestTimeFilePath = Path.Combine(packFolderPath, "best_times.json");
        if (File.Exists(bestTimeFilePath))
        {
            string currentGrid = PlayerPrefs.GetString("ChosenGridSizeSetting", "4x4");
            string currentSpeed = PlayerPrefs.GetString("ChosenSpeedSetting", "Normy");

            string jsonText = File.ReadAllText(bestTimeFilePath);
            BestTimeCollection collection = JsonUtility.FromJson<BestTimeCollection>(jsonText);
            collection.records.RemoveAll(r => r.gridSize == currentGrid && r.speedMode == currentSpeed);

            string updatedJson = JsonUtility.ToJson(collection, true);
            File.WriteAllText(bestTimeFilePath, updatedJson);
            
            UpdateBestTimeDisplay();
        }
    }

    // Fungsi animasi sliding
    private void TriggerPopupAnimation(RectTransform panel, float targetY) 
    { 
        if (panel == null) return; 
        if (activePopupCoroutines.ContainsKey(panel) && activePopupCoroutines[panel] != null) StopCoroutine(activePopupCoroutines[panel]); 
        activePopupCoroutines[panel] = StartCoroutine(SlidePopupRoutine(panel, targetY)); 
    }

    IEnumerator SlidePopupRoutine(RectTransform panel, float targetY) 
    { 
        float elapsed = 0f; 
        Vector2 startPos = panel.anchoredPosition; 
        Vector2 targetPos = new Vector2(startPos.x, targetY); 
        while (elapsed < animDuration) 
        { 
            elapsed += Time.deltaTime; 
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animDuration); 
            panel.anchoredPosition = Vector2.Lerp(startPos, targetPos, t); 
            yield return null; 
        } 
        panel.anchoredPosition = targetPos; 
    }

    public void UpdateLiveScaleFromSlider(int slotIndex, float sliderValue)
    {
        // Cari langsung objek di dalam map berdasarkan ID slot yang sedang digeser slidernya
        if (customPreviewObjectsMap.ContainsKey(slotIndex) && customPreviewObjectsMap[slotIndex] != null)
        {
            GameObject targetObj = customPreviewObjectsMap[slotIndex];
            SpriteRenderer sr = targetObj.GetComponent<SpriteRenderer>();
            
            if (sr != null && sr.sprite != null)
            {
                float maxSpriteDimension = Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y);
                float normalizer = 1.0f / maxSpriteDimension;

                // Memetakan nilai slider 0 -> 1.0f (1/6 area) dan 1 -> 2.0f (1/3 area) agar lebih berisi
                float targetClampedSize = Mathf.Lerp(1.0f, 2.0f, sliderValue);
                float finalScale = normalizer * targetClampedSize;
                
                targetObj.transform.localScale = new Vector3(finalScale, finalScale, 1f);
            }
        }
    }

    public void UpdateLiveRotationFromSlider(int slotIndex, float sliderValue)
    {
        if (customPreviewObjectsMap.ContainsKey(slotIndex) && customPreviewObjectsMap[slotIndex] != null)
        {
            GameObject targetObj = customPreviewObjectsMap[slotIndex];
            LocalBoundEnforcer enforcer = targetObj.GetComponent<LocalBoundEnforcer>();
            Rigidbody2D rb = targetObj.GetComponent<Rigidbody2D>();
            
            if (enforcer != null && rb != null)
            {
                // Hitung deviasi nilai slider dari titik tengah (0.5f)
                float deviation = sliderValue - 0.5f;

                // Toleransi deadzone (jika di kisaran 0.5f, objek akan diam total)
                if (Mathf.Abs(deviation) < 0.04f)
                {
                    enforcer.maxRotationSpeed = 0f;
                }
                else
                {
                    enforcer.maxRotationSpeed = deviation * -360f; 
                }
                
                rb.WakeUp();
                enforcer.ApplyCustomPanelRotation();
            }
        }
    }

    public void UpdateAllObjectsSpeed(float newSpeed)
    {
        // Terapkan speed ke semua objek yang aktif di paket saat ini
        foreach (LocalBoundEnforcer enforcer in activeEnforcers)
        {
            if (enforcer != null)
            {
                enforcer.UpdateMovementSpeed(newSpeed);
            }
        }
    }

    // Reset semua kriteria obyek di preview
    public void ResetSingleObjectPhysicsAndRotation(int slotIndex)
    {
        if (customPreviewObjectsMap.ContainsKey(slotIndex) && customPreviewObjectsMap[slotIndex] != null)
        {
            GameObject targetObj = customPreviewObjectsMap[slotIndex];
            Rigidbody2D rb = targetObj.GetComponent<Rigidbody2D>();
            LocalBoundEnforcer enforcer = targetObj.GetComponent<LocalBoundEnforcer>();
            SpriteRenderer sr = targetObj.GetComponent<SpriteRenderer>();

            if (enforcer != null && rb != null && sr != null && sr.sprite != null)
            {
                // Matikan mode rotasi kustom dan nolkan seluruh momentum fisik
                enforcer.useCustomPanelRotation = false;
                enforcer.maxRotationSpeed = 0f;
                rb.angularVelocity = 0f;

                // Paksa sudut rotasi visual objek kembali tegak lurus sempurna (0 derajat)
                targetObj.transform.rotation = Quaternion.identity;

                // Kembalikan ukuran visual objek ke pertengahan default (slider value = 0.5f)
                float maxSpriteDimension = Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y);
                float normalizer = 1.0f / maxSpriteDimension;
                
                // Menggunakan nilai tengah 1.5f (Pertengahan dari rentang 1.0f - 2.0f)
                float defaultClampedSize = Mathf.Lerp(1.0f, 2.0f, 0.5f); 
                float finalScale = normalizer * defaultClampedSize;
                
                targetObj.transform.localScale = new Vector3(finalScale, finalScale, 1f);

                rb.WakeUp();
            }
        }
    }

    public void ReloadCurrentActivePackFromGlobalIndex()
    {
        CustomPackManager packManager = Object.FindFirstObjectByType<CustomPackManager>();
        if (packManager != null)
        {
            LoadObjectPack(packManager.currentPackIndex);
        }
    }

    public void DestroyCurrentPreviewAnchor()
    {
        if (currentAnchor != null)
        {
            Destroy(currentAnchor);
        }
        activeEnforcers.Clear();
        customPreviewObjectsMap.Clear();
    }
}