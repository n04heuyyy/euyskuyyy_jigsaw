using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.Networking;

// Jika di-build ke WebGL, kita buat jembatan JavaScript interaktif agar browser membuka file picker asli
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

public partial class CustomPackManager : MonoBehaviour
{
    [Header("UI Panels (Sliding Setup)")]
    [SerializeField] private RectTransform customPackPanel; 
    [SerializeField] private RectTransform deleteConfirmationPopup; // UBAH: Jadikan RectTransform agar bisa sliding halus 

    [Header("Input Fields & Sliders")]
    [SerializeField] private TMP_InputField packNameInputField;
    [SerializeField] private Button[] slotButtons; 
    [SerializeField] private Button[] slotDeleteButtons; // MASUKKAN: Tombol hapus (X) untuk masing-masing slot (Ukuran harus 3)
    [SerializeField] private Slider sizeSlider;    
    [SerializeField] private Slider rotSpeedSlider;
    [SerializeField] private TextMeshProUGUI packTitleHeaderText; 
    [SerializeField] private Button resetButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button editButton;
    [SerializeField] private Button deleteButton;

    [Header("Item Preview Slot in Panel")]
    [SerializeField] private Image panelItemPreviewImage;
    [SerializeField] private GameObject previewPlaceholderText;
    [SerializeField] private TextMeshProUGUI placeholderTextMesh;

    [Header("Sliding Animation Settings")]
    [SerializeField] private float hidePositionY = -1080f; // FIX: Taruh di -1080f agar keluar ke bawah layar
    [SerializeField] private float targetShowY = 0f;
    [SerializeField] private float animDuration = 0.4f;

    [Header("Connections to Other Managers")]
    [SerializeField] private ObjectPreviewManager previewManager; 

    [Header("Image Dimension Restriction Setup")]
    [SerializeField] private int maxAllowedDimension = 3000; // Batas dimensi aman piksel (Mencegah amblas/gagal muat)

    // Tempat menampung database gabungan (Bawaan + Kustom) secara seragam
    public List<ObjectPackConfig> allLoadedPacks = new List<ObjectPackConfig>();
    public int currentPackIndex = 0;

    private ObjectPackConfig currentEditingPack = null;
    private ObjectPackConfig packToDelete = null;
    private int currentSelectedSlotIndex = -1;
    private CustomItemData[] temporarySlots = new CustomItemData[3] { null, null, null };

    private string originalPackName = "";
    private List<CustomItemData> originalSlotsBackup = new List<CustomItemData>();

    private string rootCustomPacksPath;
    private string rootDefaultPacksPath;
    private Dictionary<RectTransform, Coroutine> activePanelCoroutines = new Dictionary<RectTransform, Coroutine>();
    private float lastSaveClickTime = 0f;

    // --- DI BAGIAN VARIABEL ATAS CUSTOMPACKMANAGER.CS, TAMBAHKAN DUA SLOT VARIABEL INI ---
    [Header("Multiplatform Build Direct Path Setup")]
    [SerializeField] private TMP_InputField directPathInputField; // Kolom Input Teks Jalur Gambar (Opsional untuk versi Build)
    [SerializeField] private Button directLoadPathButton;         // Tombol pemicu muat gambar di versi Build

    // Jembatan JavaScript untuk WebGL agar memicu File Picker Browser asli (HTML5)
    #if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void TriggerWebGLFilePicker(string objectName, string callbackMethod);
    #endif

    void Start()
    {
        // 1. Inisialisasi Jalur Folder
        rootCustomPacksPath = Path.Combine(Application.persistentDataPath, "Object Packs");
        if (!Directory.Exists(rootCustomPacksPath)) Directory.CreateDirectory(rootCustomPacksPath);
        
        rootDefaultPacksPath = Path.Combine(Application.streamingAssetsPath, "Default Packs");
        if (!Directory.Exists(rootDefaultPacksPath)) Directory.CreateDirectory(rootDefaultPacksPath);

        if (sizeSlider != null) { sizeSlider.minValue = 0f; sizeSlider.maxValue = 1f; }
        if (rotSpeedSlider != null) { rotSpeedSlider.minValue = 0f; rotSpeedSlider.maxValue = 1f; }

        // 2. Hubungkan Fungsi Tombol Utama Panel Bawah
        if (resetButton != null) { resetButton.onClick.RemoveAllListeners(); resetButton.onClick.AddListener(ResetCustomPanelSliders); }
        if (cancelButton != null) { cancelButton.onClick.RemoveAllListeners(); cancelButton.onClick.AddListener(CancelAndRestorePack); }
        // KUNCI SINKRONISASI: Bersihkan paksa listener tombol save di Inspector agar tidak terpanggil ganda
        if (saveButton != null) 
        { 
            saveButton.onClick.RemoveAllListeners(); 
            saveButton.onClick.AddListener(SavePack); 
        }
        // FIX INPUT NAMA: Matikan paksa fitur auto-highlight select all bawaan TMP agar pointer bisa diletakkan bebas di mana saja
        packNameInputField.onFocusSelectAll = false;
        packNameInputField.onValueChanged.RemoveAllListeners(); 
        packNameInputField.onValueChanged.AddListener((string txt) => UpdateSaveButtonInteractivity());

        if (directLoadPathButton != null) {
        directLoadPathButton.onClick.RemoveAllListeners();
        directLoadPathButton.onClick.AddListener(() => { if(currentSelectedSlotIndex != -1) StartCoroutine(UploadImageRoutine(currentSelectedSlotIndex)); });
        }
        // PENTING: Setup Listener tombol slot harus paling pertama!
        SetupSlotListeners();

        // --- FIX OUT OF RANGE SAAT RESTART GAME ---
        LoadAllPacksFromFolders();

        // Kunci indeks agar tidak melompat keluar dari rentang total kapasitas list folder yang ter-load
        currentPackIndex = PlayerPrefs.GetInt("SelectedObjectPackIndex", 0);
        if (currentPackIndex >= allLoadedPacks.Count) currentPackIndex = 0;

        if (deleteConfirmationPopup != null)
        {
            deleteConfirmationPopup.gameObject.SetActive(true);
            deleteConfirmationPopup.anchoredPosition = new Vector2(deleteConfirmationPopup.anchoredPosition.x, hidePositionY);
        }

        UpdatePackPreviewDisplay();
        
        if (sizeSlider != null) { sizeSlider.onValueChanged.RemoveAllListeners(); sizeSlider.onValueChanged.AddListener(OnSizeSliderChanged); }
        if (rotSpeedSlider != null) { rotSpeedSlider.onValueChanged.RemoveAllListeners(); rotSpeedSlider.onValueChanged.AddListener(OnRotSpeedSliderChanged); }
    }

    void SetupSlotListeners()
    {
        int safeLength = Mathf.Min(slotButtons.Length, temporarySlots.Length);
        for (int i = 0; i < safeLength; i++)
        {
            int index = i;
            slotButtons[i].onClick.RemoveAllListeners();
            slotButtons[i].onClick.AddListener(() => OnSlotButtonClicked(index));
            
            if (i < slotDeleteButtons.Length && slotDeleteButtons[i] != null)
            {
                slotDeleteButtons[i].onClick.RemoveAllListeners();
                slotDeleteButtons[i].onClick.AddListener(() => OnDeleteObjectSlotClicked(index));
            }
        }
    }

    public void LoadAllPacksFromFolders()
    {
        allLoadedPacks.Clear();

        // 1. MEMUAT PACK DEFAULT (Dukungan Web Request Khusus WebGL)
        #if UNITY_WEBGL && !UNITY_EDITOR
        string[] webDefaultPackIDs = { "Default 1", "Default 2", "Default 3" }; 
        StartCoroutine(LoadAllWebPacksSequentially(webDefaultPackIDs));
        #else
        if (Directory.Exists(rootDefaultPacksPath))
        {
            try {
                string[] defaultDirs = Directory.GetDirectories(rootDefaultPacksPath);
                foreach (string dir in defaultDirs)
                {
                    string jsonPath = Path.Combine(dir, "pack_config.json");
                    if (File.Exists(jsonPath))
                    {
                        string jsonText = File.ReadAllText(jsonPath);
                        ObjectPackConfig pack = JsonUtility.FromJson<ObjectPackConfig>(jsonText);
                        pack.isCustom = false;
                        allLoadedPacks.Add(pack);
                    }
                }
            } catch { }
        }
        #endif

        // 2. MEMUAT PACK KUSTOM USER (Fix Mutlak Muncul di Windows Standalone .EXE)
        #if !UNITY_WEBGL || UNITY_EDITOR
        // FIX EXE BUILD: Jika folder custom pack belum ada di AppData Windows, paksa buat foldernya sekarang!
        if (!Directory.Exists(rootCustomPacksPath)) 
        {
            Directory.CreateDirectory(rootCustomPacksPath);
        }

        try {
            string[] customDirs = Directory.GetDirectories(rootCustomPacksPath);
            foreach (string dir in customDirs)
            {
                string jsonPath = Path.Combine(dir, "pack_config.json");
                if (File.Exists(jsonPath))
                {
                    string jsonText = File.ReadAllText(jsonPath);
                    ObjectPackConfig pack = JsonUtility.FromJson<ObjectPackConfig>(jsonText);
                    pack.isCustom = true;
                    allLoadedPacks.Add(pack);
                }
            }
        } catch { }
        #endif

        // Memuat save data kustom dari PlayerPrefs khusus WebGL browser
        #if UNITY_WEBGL && !UNITY_EDITOR
        for (int i = 1; i <= 5; i++)
        {
            string key = "WebGL_CustomPack_Slot_" + i;
            if (PlayerPrefs.HasKey(key))
            {
                string jsonText = PlayerPrefs.GetString(key);
                ObjectPackConfig pack = JsonUtility.FromJson<ObjectPackConfig>(jsonText);
                pack.isCustom = true;
                allLoadedPacks.Add(pack);
            }
        }
        #endif

        if (allLoadedPacks.Count > 0 && currentPackIndex == -1) currentPackIndex = 0;
    }

    #if UNITY_WEBGL && !UNITY_EDITOR
    // Coroutine khusus WebGL untuk menjamin data pack selesai diunduh berurutan sebelum UI direfresh
    private IEnumerator LoadAllWebPacksSequentially(string[] packIDs)
    {
        foreach (string packID in packIDs)
        {
            string urlPath = Application.streamingAssetsPath + "/Default Packs/" + packID + "/pack_config.json";
            urlPath = urlPath.Replace("\\", "/"); // Paksa format URL Web murni

            using (UnityWebRequest webRequest = UnityWebRequest.Get(urlPath))
            {
                yield return webRequest.SendWebRequest();
                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    ObjectPackConfig pack = JsonUtility.FromJson<ObjectPackConfig>(webRequest.downloadHandler.text);
                    pack.isCustom = false;
                    allLoadedPacks.Add(pack);
                }
            }
        }
        
        // Segarkan total tampilan UI Menu setelah semua data pack default kelar terdownload dari internal web server
        UpdatePackPreviewDisplay();
    }
    #endif

    public void UpdatePackPreviewDisplay()
    {
        if (allLoadedPacks.Count == 0) return;
        
        int safeIndex = Mathf.Clamp(currentPackIndex, 0, allLoadedPacks.Count - 1);
        ObjectPackConfig activePack = allLoadedPacks[safeIndex];

        // 1. FIX SINKRONISASI NAMA: Ganti teks judul merah atas secara live sesuai data .json
        if (packTitleHeaderText != null) 
        {
            packTitleHeaderText.text = activePack.packName + (activePack.isCustom ? " (Custom)" : "");
        }

        // 2. FIX TOMBOL SIDEBAR EDIT & DELETE: Nyalakan/Matikan interactable murni
        if (editButton != null) editButton.interactable = activePack.isCustom;
        if (deleteButton != null) deleteButton.interactable = activePack.isCustom;

        // SIMPAN PARAMETER TIPENYA JUGA AGAR GAMEPLAY TAHU DEFAULT VS CUSTOM
        PlayerPrefs.SetInt("SelectedObjectPackIndex", currentPackIndex);
        PlayerPrefs.SetInt("SelectedObjectPackIsCustom", activePack.isCustom ? 1 : 0);
        PlayerPrefs.Save();

        if (previewManager != null) {
            previewManager.LoadObjectPack(currentPackIndex);
        }

        // Jalankan Coroutine penunggu visual lintas platform (Terutama untuk WebGL pasca-save)
        StartCoroutine(GlobalPackPreviewRefreshRoutine(activePack));
    }

    private IEnumerator GlobalPackPreviewRefreshRoutine(ObjectPackConfig activePack)
    {
        if (previewManager == null) yield break;

        #if UNITY_WEBGL && !UNITY_EDITOR
        // Bersihkan visual sisa objek lama dari hierarchy menu utama
        SpriteRenderer[] oldRenderers = previewManager.gameObject.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer oldSr in oldRenderers)
        {
            if (!oldSr.gameObject.name.Contains("Background") && oldSr.gameObject.layer != LayerMask.NameToLayer("UI"))
            {
                Destroy(oldSr.gameObject);
            }
        }
        yield return null; 
        previewManager.LoadObjectPack(currentPackIndex);
        #endif

        yield return new WaitForSeconds(0.15f); // Berikan waktu yang cukup bagi browser untuk instansiasi objek
        yield return new WaitForEndOfFrame();

        SpriteRenderer[] packMenuRenderers = previewManager.gameObject.GetComponentsInChildren<SpriteRenderer>(true);
        if (packMenuRenderers.Length == 0)
        {
            packMenuRenderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        }

        // AMANKAN PREVIEW MENU UTAMA BERDASARKAN INDEKS NAMA OBJEK
        for (int r = 0; r < packMenuRenderers.Length; r++)
        {
            SpriteRenderer sr = packMenuRenderers[r];
            if (sr == null || sr.gameObject.name.Contains("Background") || sr.gameObject.layer == LayerMask.NameToLayer("UI")) 
                continue;

            for (int itemIndex = 0; itemIndex < activePack.items.Count; itemIndex++)
            {
                // Jika nama objek melayang cocok dengan indeks item konfigurasi pack kustom
                if (sr.gameObject.name.Contains(itemIndex.ToString()) || r == itemIndex)
                {
                    sr.gameObject.SetActive(true);
                    #if UNITY_WEBGL && !UNITY_EDITOR
                    if (activePack.isCustom && WebGLTextureCache.Instance != null && WebGLTextureCache.Instance.cachedCustomSprites.ContainsKey(itemIndex))
                    {
                        Sprite cachedSprite = WebGLTextureCache.Instance.cachedCustomSprites[itemIndex];
                        if (cachedSprite != null) sr.sprite = cachedSprite;
                    }
                    #endif
                }
            }
        }

        previewManager.UpdateBestTimeDisplay();

    }

    // 5. Perbarui fungsi panah kiri/kanan agar ikut mengubah nama teks UI
    public void ChangePackSelectionIndex(int direction)
    {
        if (allLoadedPacks.Count == 0) return;
        currentPackIndex = (currentPackIndex + direction + allLoadedPacks.Count) % allLoadedPacks.Count;
        UpdatePackPreviewDisplay();
    }

    // --- FIX LOGIKA KLIK DINAMIS (BARU VS SUDAH ADA ISI) ---
    void OnSlotButtonClicked(int slotIndex)
    {
        // KONDISI A: Klik di slot kosong -> Langsung buka upload
        if (temporarySlots[slotIndex] == null || string.IsNullOrEmpty(temporarySlots[slotIndex].imageFileName))
        {
            currentSelectedSlotIndex = slotIndex;
            temporarySlots[slotIndex] = new CustomItemData { imageFileName = "", sizeScale = 0.5f, maxRotSpeed = 0.5f };
            StartCoroutine(UploadImageRoutine(slotIndex));
            return;
        }

        // KONDISI B: Klik di slot terisi yang SAAT INI BELUM dipilih -> Pindah fokus ke objek tersebut
        if (currentSelectedSlotIndex != slotIndex)
        {
            currentSelectedSlotIndex = slotIndex;
            ExecuteSingleClick(slotIndex);
        }
        // KONDISI C: Klik di slot terisi yang SAAT INI SUDAH dipilih -> Buka upload untuk ganti gambar
        else
        {
            StartCoroutine(UploadImageRoutine(slotIndex));
        }
    }

    void ExecuteSingleClick(int slotIndex)
    {
        // 1. VALIDASI UKURAN ARRAY
        if (slotIndex < 0 || slotIndex >= temporarySlots.Length || temporarySlots[slotIndex] == null) return;

        // 2. SINKRONISASI AKTIF SLIDER KUNCI (Fix Slider Stuck Lintas Platform!)
        if (sizeSlider != null) sizeSlider.value = temporarySlots[slotIndex].sizeScale;
        if (rotSpeedSlider != null) rotSpeedSlider.value = temporarySlots[slotIndex].maxRotSpeed;

        // 3. APLIKASIKAN VISUAL MINI PREVIEW TENGAH
        #if UNITY_WEBGL && !UNITY_EDITOR
        if (WebGLTextureCache.Instance != null && WebGLTextureCache.Instance.cachedCustomSprites.ContainsKey(slotIndex))
        {
            Sprite cachedSprite = WebGLTextureCache.Instance.cachedCustomSprites[slotIndex];
            if (cachedSprite != null && panelItemPreviewImage != null)
            {
                panelItemPreviewImage.gameObject.SetActive(true);
                panelItemPreviewImage.sprite = cachedSprite;
                panelItemPreviewImage.preserveAspect = true;
                if (previewPlaceholderText != null) previewPlaceholderText.SetActive(false);
                
                float panelVisualScale = Mathf.Lerp(0.6f, 1.2f, sizeSlider.value);
                panelItemPreviewImage.rectTransform.localScale = new Vector3(panelVisualScale, panelVisualScale, 1f);
            }
        }
        #else
        string packID = currentEditingPack == null ? "Temp" : currentEditingPack.packID;
        string path = Path.Combine(rootCustomPacksPath, "Pack_" + packID, temporarySlots[slotIndex].imageFileName);
        StartCoroutine(LoadSinglePreviewToPanel(path));
        #endif

        // 4. EMBUSKAN PERINTAH KE PREVIEW MANAGER UTAMA
        if (previewManager != null)
        {
            previewManager.UpdateLiveScaleFromSlider(slotIndex, sizeSlider.value);
            previewManager.UpdateLiveRotationFromSlider(slotIndex, rotSpeedSlider.value);
            
            // FIX SINKRONISASI KLIK WEBGL: Cari objek berdasarkan deteksi nama string
            SpriteRenderer[] activeRenderers = previewManager.gameObject.GetComponentsInChildren<SpriteRenderer>(true);
            for (int r = 0; r < activeRenderers.Length; r++)
            {
                SpriteRenderer sr = activeRenderers[r];
                if (sr.gameObject.name.Contains("Background") || sr.gameObject.layer == LayerMask.NameToLayer("UI")) continue;

                // Cari apakah nama objek mengandung angka index slot yang dituju (misal: "Object_0", "Slot_0", dll)
                // Atau gunakan fallback urutan jika namanya sama semua
                if (sr.gameObject.name.Contains(slotIndex.ToString()) || r == slotIndex)
                {
                    sr.gameObject.SetActive(true);
                    #if UNITY_WEBGL && !UNITY_EDITOR
                    if (WebGLTextureCache.Instance != null && WebGLTextureCache.Instance.cachedCustomSprites.ContainsKey(slotIndex))
                        sr.sprite = WebGLTextureCache.Instance.cachedCustomSprites[slotIndex];
                    #endif
                }
            }
        }

        UpdateSlotDeleteButtonsVisibility();
        UpdateSaveButtonInteractivity();
    }

    #if UNITY_WEBGL && !UNITY_EDITOR
    // Sub-Coroutine khusus WebGL untuk mendownload isi konfigurasi item pack default via web server
    private IEnumerator LoadWebPackConfigRoutine(string packID)
    {
        // Pastikan URL menggunakan garis miring web '/' bukan '\' khas Windows
        string urlPath = Application.streamingAssetsPath + "/Default Packs/" + packID + "/pack_config.json";
        urlPath = urlPath.Replace("\\", "/");

        using (UnityWebRequest webRequest = UnityWebRequest.Get(urlPath))
        {
            yield return webRequest.SendWebRequest();
            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                ObjectPackConfig pack = JsonUtility.FromJson<ObjectPackConfig>(webRequest.downloadHandler.text);
                pack.isCustom = false;
                allLoadedPacks.Add(pack);
                
                // Jika ini pack pertama yang berhasil dimuat dari web, langsung segarkan UI
                if(allLoadedPacks.Count == 1) UpdatePackPreviewDisplay();
            }
            else
            {
                Debug.LogError($"Gagal memuat pack default web {packID}: {webRequest.error} di URL: {urlPath}");
            }
        }
    }
    #endif

    private IEnumerator UploadImageRoutine(int slotIndex)
    {
        #if UNITY_WEBGL && !UNITY_EDITOR
        // Jalur A: Khusus WebGL Build asli di browser
        if (placeholderTextMesh != null) placeholderTextMesh.text = "Membuka File Picker Browser...";
        TriggerWebGLFilePicker(gameObject.name, "OnWebGLImageSelected");
        yield break; // Langsung keluar di sini, aman dari unreachable code!
        
        #else
        // Jalur B: Khusus PC Standalone .EXE & Unity Editor
        string selectedPath = "";
        
        #if UNITY_EDITOR
        selectedPath = UnityEditor.EditorUtility.OpenFilePanel("Pilih Gambar Objek (PNG/JPG)", "", "png,jpg,jpeg");
        if (string.IsNullOrEmpty(selectedPath)) yield break;
        
        #elif UNITY_STANDALONE_WIN
        // Jalur B: Di dalam Versi Windows Standalone .EXE Build
        try
        {
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "powershell.exe";
            process.StartInfo.Arguments = "-NoProfile -Command \"Add-Type -AssemblyName System.Windows.Forms; $g = New-Object System.Windows.Forms.OpenFileDialog; $g.Filter = 'Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg'; if($g.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK){ Write-Output $g.FileName }\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            
            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            
            if (!string.IsNullOrEmpty(output)) selectedPath = output;
        }
        catch { }
        if (string.IsNullOrEmpty(selectedPath)) yield break;
        #endif

        if (!File.Exists(selectedPath)) yield break;

        // Jalur C: Di dalam Browser Web (WebGL Build asli)
        if (placeholderTextMesh != null) placeholderTextMesh.text = "Loading image asset...";

        string fileExtension = Path.GetExtension(selectedPath);
        string shortName = Path.GetFileNameWithoutExtension(selectedPath);
        if (shortName.Length > 8) shortName = shortName.Substring(0, 8);
        string finalFileName = "img_" + slotIndex + "_" + shortName + fileExtension;

        string packID = currentEditingPack == null ? "Temp" : currentEditingPack.packID;
        string packFolderPath = Path.Combine(rootCustomPacksPath, "Pack_" + packID);

        if (!Directory.Exists(packFolderPath)) Directory.CreateDirectory(packFolderPath);
        string targetCopyPath = Path.Combine(packFolderPath, finalFileName);

        File.Copy(selectedPath, targetCopyPath, true);

        yield return StartCoroutine(LoadTextureToSlotUI(targetCopyPath, slotIndex, finalFileName));
        #endif
    }

    public void OnWebGLImageSelected(string base64Data)
    {
        if (string.IsNullOrEmpty(base64Data)) return;
        // Alihkan ke fungsi dekode base64 murni yang aman dari masalah URL Blob
        StartCoroutine(LoadWebGLBase64TextureRoutine(base64Data, currentSelectedSlotIndex));
    }

    // FIX VISUAL ITEM DI GAMEPLAY & PREVIEW: Mengunduh Blob URL secara asinkron (Aman di semua Scene)
    private IEnumerator LoadWebGLBase64TextureRoutine(string base64String, int slotIndex)
    {
        if (slotIndex == -1) yield break;
        
        if (placeholderTextMesh != null) 
        {
            placeholderTextMesh.text = "Processing Browser Image...";
        }

        // Konversi string Base64 langsung menjadi array byte mentah di memori RAM
        byte[] imageBytes = System.Convert.FromBase64String(base64String);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        
        if (texture.LoadImage(imageBytes))
        {
            // Berikan jeda frame agar sinkronisasi memori GPU grafik browser selesai sempurna
            yield return null;
            yield return new WaitForEndOfFrame();

            if (texture.width > maxAllowedDimension || texture.height > maxAllowedDimension)
            {
                if (placeholderTextMesh != null) placeholderTextMesh.text = "<color=red>Gambar Terlalu Besar!</color>";
                Destroy(texture);
                yield break;
            }

            Sprite newSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));

            // Masukkan langsung ke RAM Cache global agar bisa diambil di arena Gameplay
            if (WebGLTextureCache.Instance != null)
            {
                if (WebGLTextureCache.Instance.cachedCustomSprites.ContainsKey(slotIndex))
                {
                    if (WebGLTextureCache.Instance.cachedCustomSprites[slotIndex] != null)
                    {
                        Destroy(WebGLTextureCache.Instance.cachedCustomSprites[slotIndex].texture);
                        Destroy(WebGLTextureCache.Instance.cachedCustomSprites[slotIndex]);
                    }
                    WebGLTextureCache.Instance.cachedCustomSprites[slotIndex] = newSprite;
                }
                else
                {
                    WebGLTextureCache.Instance.cachedCustomSprites.Add(slotIndex, newSprite);
                }
            }

            // Kunci penanda string nama file dummy untuk internal pack kustom web
            temporarySlots[slotIndex].imageFileName = "cached_custom_slot_" + slotIndex; 

            if (slotIndex < slotButtons.Length && slotButtons[slotIndex] != null)
            {
                slotButtons[slotIndex].GetComponentInChildren<TextMeshProUGUI>().text = "Slot_" + slotIndex + "_Ready";
                Image[] childImages = slotButtons[slotIndex].GetComponentsInChildren<Image>(true);
                foreach(Image img in childImages)
                {
                    if(img.gameObject != slotButtons[slotIndex].gameObject)
                    {
                        img.sprite = newSprite;
                        img.gameObject.SetActive(true);
                    }
                }
            }
            if (previewManager != null)
            {
                SpriteRenderer[] checkRenderers = previewManager.gameObject.GetComponentsInChildren<SpriteRenderer>(true);
                if (checkRenderers.Length == 0)
                {
                    previewManager.LoadObjectPackFromCustomData(temporarySlots, Path.Combine(rootCustomPacksPath, "Pack_Temp"));
                    yield return new WaitForEndOfFrame();
                    yield return new WaitForSeconds(0.05f);
                }

                SpriteRenderer[] activeRenderers = previewManager.gameObject.GetComponentsInChildren<SpriteRenderer>(true);
                if (activeRenderers.Length == 0) activeRenderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
                
                // Tata ulang penugasan Sprite berdasarkan nama identitas string objek internal
                for (int r = 0; r < activeRenderers.Length; r++)
                {
                    SpriteRenderer sr = activeRenderers[r];
                    if (sr.gameObject.name.Contains("Background") || sr.gameObject.layer == LayerMask.NameToLayer("UI")) continue;

                    // Cari angka indeks slot di dalam nama gameobject renderer tersebut (0, 1, atau 2)
                    for (int i = 0; i < temporarySlots.Length; i++)
                    {
                        if (sr.gameObject.name.Contains(i.ToString()) || r == i)
                        {
                            if (WebGLTextureCache.Instance != null && WebGLTextureCache.Instance.cachedCustomSprites.ContainsKey(i))
                            {
                                sr.sprite = WebGLTextureCache.Instance.cachedCustomSprites[i];
                                sr.gameObject.SetActive(true);
                            }
                            else if (i == slotIndex)
                            {
                                sr.sprite = newSprite;
                                sr.gameObject.SetActive(true);
                            }
                        }
                    }
                }
            }

            TriggerMainPreviewRefresh();
            UpdateSlotDeleteButtonsVisibility();
            UpdateSlotInteractivity();
            
            // Paksa slider size & rotasi menembak ke indeks slot yang murni
            sizeSlider.value = temporarySlots[slotIndex].sizeScale;
            rotSpeedSlider.value = temporarySlots[slotIndex].maxRotSpeed;
            OnSizeSliderChanged(sizeSlider.value);
            OnRotSpeedSliderChanged(rotSpeedSlider.value);
            
            if (placeholderTextMesh != null) placeholderTextMesh.text = "<color=green>Image Loaded Successfully!</color>";
        }
    }

    private IEnumerator LoadTextureToSlotUI(string path, int slotIndex, string finalFileName)
    {
        string url = "file://" + path;
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                texture.filterMode = FilterMode.Point;
                Sprite newSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));

                temporarySlots[slotIndex].imageFileName = finalFileName;
                if (slotIndex < slotButtons.Length)
                    slotButtons[slotIndex].GetComponentInChildren<TextMeshProUGUI>().text = finalFileName;

                if (previewManager != null)
                {
                    // PENGAMAN ADD PACK EDITOR: Paksa inisialisasi objek tiruan jika hierarki masih kosong melompong
                    SpriteRenderer[] checkRenderers = previewManager.gameObject.GetComponentsInChildren<SpriteRenderer>(true);
                    if (checkRenderers.Length == 0)
                    {
                        string packID = currentEditingPack == null ? "Temp" : currentEditingPack.packID;
                        string targetFolderPath = Path.Combine(rootCustomPacksPath, "Pack_" + packID);
                        
                        previewManager.LoadObjectPackFromCustomData(temporarySlots, targetFolderPath);
                        yield return new WaitForEndOfFrame();
                    }

                    SpriteRenderer[] activeRenderers = previewManager.gameObject.GetComponentsInChildren<SpriteRenderer>(true);
                    if (activeRenderers.Length == 0) activeRenderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);

                    int validItemIndex = 0;
                    for (int r = 0; r < activeRenderers.Length; r++)
                    {
                        SpriteRenderer sr = activeRenderers[r];
                        if (sr.gameObject.name.Contains("Background") || sr.gameObject.layer == LayerMask.NameToLayer("UI")) continue;

                        if (validItemIndex == slotIndex)
                        {
                            sr.sprite = newSprite;
                            sr.gameObject.SetActive(true);
                            break; 
                        }
                        validItemIndex++;
                    }
                }

                TriggerMainPreviewRefresh();
                UpdateSlotDeleteButtonsVisibility();
                UpdateSlotInteractivity();
                
                sizeSlider.value = temporarySlots[slotIndex].sizeScale;
                rotSpeedSlider.value = temporarySlots[slotIndex].maxRotSpeed;
                OnSizeSliderChanged(sizeSlider.value);
                OnRotSpeedSliderChanged(rotSpeedSlider.value);
            }
        }
    }

    // --- LOGIKA BARU: HAPUS OBJEK INDIVIDUAL & SHIFT-UP ---
    void OnDeleteObjectSlotClicked(int slotIndex)
    {
        int totalFilled = 0;
        foreach (var item in temporarySlots) {
            if (item != null && !string.IsNullOrEmpty(item.imageFileName)) totalFilled++;
        }

        // Proteksi: Jika hanya tersisa 1 objek, dilarang delete
        if (totalFilled <= 1) return;

        // Jalankan alur pergeseran slot (Shift-Up)
        for (int i = slotIndex; i < temporarySlots.Length - 1; i++)
        {
            temporarySlots[i] = temporarySlots[i + 1];
        }
        temporarySlots[temporarySlots.Length - 1] = null;

        currentSelectedSlotIndex = 0;
        
        RefreshAllSlotTextsAndVisuals();
        TriggerMainPreviewRefresh();
        UpdateSlotDeleteButtonsVisibility();
        UpdateSlotInteractivity();
    }

    void RefreshAllSlotTextsAndVisuals()
    {
        int safeLength = Mathf.Min(temporarySlots.Length, slotButtons.Length);
        for (int i = 0; i < safeLength; i++)
        {
            if (temporarySlots[i] != null && !string.IsNullOrEmpty(temporarySlots[i].imageFileName))
            {
                slotButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = "Slot_" + i + "_Filled";

                #if UNITY_WEBGL && !UNITY_EDITOR
                // Pemulihan visual thumbnail tombol slot dari memori RAM khusus WebGL
                if (WebGLTextureCache.Instance != null && WebGLTextureCache.Instance.cachedCustomSprites.ContainsKey(i))
                {
                    Sprite s = WebGLTextureCache.Instance.cachedCustomSprites[i];
                    Image[] childImages = slotButtons[i].GetComponentsInChildren<Image>(true);
                    foreach(Image img in childImages)
                    {
                        if(img.gameObject != slotButtons[i].gameObject)
                        {
                            img.sprite = s;
                            img.gameObject.SetActive(true);
                        }
                    }
                }
                #endif
            }
            else
            {
                slotButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = "Upload";
                
                #if UNITY_WEBGL && !UNITY_EDITOR
                // Sembunyikan thumbnail jika slot kosong
                Image[] childImages = slotButtons[i].GetComponentsInChildren<Image>(true);
                foreach(Image img in childImages)
                {
                    if(img.gameObject != slotButtons[i].gameObject) img.gameObject.SetActive(false);
                }
                #endif
            }
        }
        
        if (temporarySlots[0] != null && !string.IsNullOrEmpty(temporarySlots[0].imageFileName)) {
            ExecuteSingleClick(0);
        } else {
            ClearPanelPreviewAsset();
            if (panelItemPreviewImage != null) panelItemPreviewImage.gameObject.SetActive(false);
            if (previewPlaceholderText != null) previewPlaceholderText.SetActive(true);
        }
    }

    void UpdateSlotDeleteButtonsVisibility()
    {
        int totalFilled = 0;
        foreach (var item in temporarySlots) {
            if (item != null && !string.IsNullOrEmpty(item.imageFileName)) totalFilled++;
        }

        for (int i = 0; i < slotDeleteButtons.Length; i++)
        {
            if (slotDeleteButtons[i] == null) continue;
            bool hasItem = temporarySlots[i] != null && !string.IsNullOrEmpty(temporarySlots[i].imageFileName);
            
            // Mengembalikan tombol X secara mantap jika slot terisi dan total item buatan > 1
            slotDeleteButtons[i].gameObject.SetActive(hasItem && totalFilled > 1);
        }
    }

    void OnSizeSliderChanged(float val)
    {
        if (currentSelectedSlotIndex != -1 && temporarySlots[currentSelectedSlotIndex] != null)
        {
            temporarySlots[currentSelectedSlotIndex].sizeScale = val;
            
            // Perbarui visual mini preview di panel menggunakan pemetaan interpolasi (Lerp)
            float panelVisualScale = Mathf.Lerp(0.6f, 1.2f, val);
            if (panelItemPreviewImage != null)
            {
                panelItemPreviewImage.rectTransform.localScale = new Vector3(panelVisualScale, panelVisualScale, 1f);
            }
            
            if (previewManager != null)
            {
                previewManager.UpdateLiveScaleFromSlider(currentSelectedSlotIndex, val);
            }
            UpdateSaveButtonInteractivity();
        }
    }

    void OnRotSpeedSliderChanged(float val)
    {
        if (currentSelectedSlotIndex != -1 && temporarySlots[currentSelectedSlotIndex] != null)
        {
            temporarySlots[currentSelectedSlotIndex].maxRotSpeed = val;
            if (previewManager != null) previewManager.UpdateLiveRotationFromSlider(currentSelectedSlotIndex, val);
            UpdateSaveButtonInteractivity();
        }
    }

    public void UpdateSaveButtonInteractivity()
    {
        if (saveButton == null) return;

        bool isNameFilled = !string.IsNullOrEmpty(packNameInputField.text);
        bool isSlot1Filled = temporarySlots[0] != null && !string.IsNullOrEmpty(temporarySlots[0].imageFileName);

        if (!isNameFilled || !isSlot1Filled)
        {
            saveButton.interactable = false;
            return;
        }

        // KUNCI COGNITIVE: Jika list backup data original belum siap, hentikan agar tidak melempar Out Of Range Exception
        if (originalSlotsBackup == null || originalSlotsBackup.Count < temporarySlots.Length) {
            saveButton.interactable = false;
            return;
        }

        // --- PROTEKSI SAVE: CEK JIKA SAMA DENGAN ORIGINAL / TIDAK ADA PERUBAHAN ---
        bool hasChanges = false;
        if (packNameInputField.text != originalPackName) hasChanges = true;

        for (int i = 0; i < temporarySlots.Length; i++)
        {
            if (temporarySlots[i] == null && originalSlotsBackup[i] == null) continue;
            if ((temporarySlots[i] == null && originalSlotsBackup[i] != null) || (temporarySlots[i] != null && originalSlotsBackup[i] == null)) {
                hasChanges = true; 
                break;
            }
            
            // Menggunakan Delta absolut agar peka terhadap pergeseran slider mikro sekecil apa pun
            if (temporarySlots[i].imageFileName != originalSlotsBackup[i].imageFileName ||
                Mathf.Abs(temporarySlots[i].sizeScale - originalSlotsBackup[i].sizeScale) > 0.005f ||
                Mathf.Abs(temporarySlots[i].maxRotSpeed - originalSlotsBackup[i].maxRotSpeed) > 0.005f) 
            {
                hasChanges = true; 
                break;
            }
        }
        saveButton.interactable = hasChanges;
    }

    public void SavePack()
    {
        if (string.IsNullOrEmpty(packNameInputField.text) || temporarySlots[0] == null) return;
        if (Time.time - lastSaveClickTime < 0.5f) return;
        lastSaveClickTime = Time.time;

        if (saveButton != null) saveButton.interactable = false;

        string packID = currentEditingPack == null ? System.DateTime.Now.Ticks.ToString() : currentEditingPack.packID;
        // Racik data konfigurasi pack baru
        ObjectPackConfig newPack = new ObjectPackConfig { packID = packID, packName = packNameInputField.text, isCustom = true };
        foreach (var item in temporarySlots) {
            if (item != null && !string.IsNullOrEmpty(item.imageFileName)) newPack.items.Add(item);
        }

        #if UNITY_WEBGL && !UNITY_EDITOR
        // FIX SAVE WEBGL: Serialisasikan seluruh kelas pack kustom, lalu amankan di memori PlayerPrefs lokal browser
        string jsonWebData = JsonUtility.ToJson(newPack);
        
        // Cari slot kosong dari 1 s/d 5 untuk menyimpan di PlayerPrefs WebGL
        int savedSlot = 1;
        for (int i = 1; i <= 5; i++)
        {
            string keyCheck = "WebGL_CustomPack_Slot_" + i;
            if (currentEditingPack != null && PlayerPrefs.HasKey(keyCheck))
            {
                ObjectPackConfig old = JsonUtility.FromJson<ObjectPackConfig>(PlayerPrefs.GetString(keyCheck));
                if(old.packID == newPack.packID) { savedSlot = i; break; }
            }
            else if (!PlayerPrefs.HasKey(keyCheck)) { savedSlot = i; break; }
        }

        PlayerPrefs.SetString("WebGL_CustomPack_Slot_" + savedSlot, jsonWebData);
        PlayerPrefs.Save();
        #else

        // VERSI PC EXE & EDITOR tetap menulis file fisik .json asli ke harddisk komputer
        string packFolderPath = Path.Combine(rootCustomPacksPath, "Pack_" + packID);
        string tempFolderPath = Path.Combine(rootCustomPacksPath, "Pack_Temp");
        if (!Directory.Exists(packFolderPath)) Directory.CreateDirectory(packFolderPath);

        if (currentEditingPack != null)
        {
            string bestTimeFilePath = Path.Combine(packFolderPath, "best_times.json");
            if (File.Exists(bestTimeFilePath)) try { File.Delete(bestTimeFilePath); } catch { }
        }

        if (currentEditingPack == null && Directory.Exists(tempFolderPath))
        {
            string[] files = Directory.GetFiles(tempFolderPath);
            foreach (string file in files)
            {
                string destFile = Path.Combine(packFolderPath, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }
        }

        string json = JsonUtility.ToJson(newPack, true);
        File.WriteAllText(Path.Combine(packFolderPath, "pack_config.json"), json);
        #endif

        TriggerPanelAnimation(customPackPanel, hidePositionY);

        #if UNITY_WEBGL && !UNITY_EDITOR
        // SINKRONISASI INDEKS RAM CACHE PRE-SAVE: 
        // Bersihkan penanda string dan tata ulang posisi cache agar tidak bergeser saat kembali ke menu awal
        if (WebGLTextureCache.Instance != null)
        {
            for (int i = 0; i < temporarySlots.Length; i++)
            {
                if (temporarySlots[i] == null || string.IsNullOrEmpty(temporarySlots[i].imageFileName))
                {
                    if (WebGLTextureCache.Instance.cachedCustomSprites.ContainsKey(i))
                        WebGLTextureCache.Instance.cachedCustomSprites.Remove(i);
                }
            }
        }
        #endif
        
        #if UNITY_WEBGL && !UNITY_EDITOR
        LoadAllPacksFromFolders();
        currentPackIndex = allLoadedPacks.FindIndex(p => p.packID == packID);
        UpdatePackPreviewDisplay();
        MainMenuUIManager menuUI = Object.FindFirstObjectByType<MainMenuUIManager>();
        if (menuUI != null) menuUI.SetSidebarInteractable(true, false);
        #else
        StartCoroutine(PostSaveCleanupRoutine(tempFolderPath, packID, currentEditingPack != null));
        #endif
    }

    public void SaveCurrentEditingPack()
    {
        if (currentEditingPack == null) return;

        // Ambil data input nama pack dari UI kamu
        // pastikan inputFieldNamaPack disesuaikan dengan variabel komponen nama paket di skripmu
        // currentEditingPack.packName = inputFieldNamaPack.text; 

        #if UNITY_WEBGL && !UNITY_EDITOR
        // SOLUSI TOTAL SAVE DI WEB: Ubah data kelas menjadi string JSON, lalu amankan di PlayerPrefs Browser (IndexedDB)
        string packJsonData = JsonUtility.ToJson(currentEditingPack);
        PlayerPrefs.SetString("WebGL_CustomPack_" + currentEditingPack.packID, packJsonData);
        PlayerPrefs.Save();
        
        if (placeholderTextMesh != null) placeholderTextMesh.text = "<color=green>Paket Sukses Disimpan di Browser!</color>";
        #else
        // Untuk Versi Windows .EXE dan Editor, tetap simpan dalam bentuk file fisik asli di harddisk
        string packFolderPath = Path.Combine(rootCustomPacksPath, "Pack_" + currentEditingPack.packID);
        if (!Directory.Exists(packFolderPath)) Directory.CreateDirectory(packFolderPath);

        string jsonPath = Path.Combine(packFolderPath, "pack_config.json");
        string jsonText = JsonUtility.ToJson(currentEditingPack, true);
        File.WriteAllText(jsonPath, jsonText);
        
        if (placeholderTextMesh != null) placeholderTextMesh.text = "<color=green>Paket Sukses Disimpan di PC!</color>";
        #endif

        // Segarkan ulang daftar paket agar paket yang baru disimpan langsung muncul di list menu utama
        LoadAllPacksFromFolders();
    }

    private IEnumerator PostSaveCleanupRoutine(string tempPath, string targetPackID, bool isEditMode)
    {
        ClearPanelPreviewAsset();
        if (previewManager != null) previewManager.DestroyCurrentPreviewAnchor();

        yield return new WaitForSeconds(animDuration + 0.05f);

        // Jika dalam mode EDIT, kita dilarang keras menghapus folder induk asli pack-nya 
        // agar file 'best_times.json' yang berisi seluruh rekor lamamu tetap terjaga aman di tempatnya!
        if (!isEditMode && Directory.Exists(tempPath))
        {
            try {
                string[] files = Directory.GetFiles(tempPath);
                foreach (string f in files) File.Delete(f);
                Directory.Delete(tempPath, true);
            } catch { }
        }

        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.05f);

        LoadAllPacksFromFolders(); 
        currentPackIndex = allLoadedPacks.FindIndex(p => p.packID == targetPackID);
        if (currentPackIndex == -1) currentPackIndex = allLoadedPacks.Count - 1;

        UpdatePackPreviewDisplay();

        System.GC.Collect();
        Resources.UnloadUnusedAssets();

        // FIX UNLOCK UI INTERACTIVITY PASCA-SAVE: Pastikan pembukaan blokir dipanggil paling akhir di coroutine agar tidak tumpang tindih
        MainMenuUIManager menuUI = Object.FindFirstObjectByType<MainMenuUIManager>();
        if (menuUI != null) menuUI.SetSidebarInteractable(true, false);
    }

    // --- FIX EDIT TEXT BUG: INISIALISASI BERSIH DI ADD & EDIT PANEL ---
    private void BackupOriginalState()
    {
        originalPackName = packNameInputField.text;
        originalSlotsBackup.Clear();
        for (int i = 0; i < temporarySlots.Length; i++) {
            if (temporarySlots[i] != null) {
                originalSlotsBackup.Add(new CustomItemData { 
                    imageFileName = temporarySlots[i].imageFileName, 
                    sizeScale = temporarySlots[i].sizeScale, 
                    maxRotSpeed = temporarySlots[i].maxRotSpeed 
                });
            } else { 
                originalSlotsBackup.Add(null); 
            }
        }
    }

    // --- UTILITY UNTUK MENGHANCURKAN FILE LOCK RUNTIME ---
    private void ClearPanelPreviewAsset()
    {
        if (panelItemPreviewImage != null && panelItemPreviewImage.sprite != null)
        {
            Sprite s = panelItemPreviewImage.sprite;
            Texture2D t = s.texture;
            panelItemPreviewImage.sprite = null;
            if (t != null) Destroy(t);
            if (s != null) Destroy(s);
        }
    }

    public void OpenAddPackPanel()
    {
        currentEditingPack = null; 
        packNameInputField.text = "";
        currentSelectedSlotIndex = -1;

        // Paksa nilai slider mereset tegak lurus ke nilai tengah default (0.5f) murni bebas sisa edit lama
        if (sizeSlider != null) sizeSlider.value = 0.5f;
        if (rotSpeedSlider != null) rotSpeedSlider.value = 0.5f;

        if (placeholderTextMesh != null) placeholderTextMesh.text = "No images selected!";

        // FIX OUT OF RANGE: Paksa pengosongan total memori array slot rakitan baru
        System.Array.Clear(temporarySlots, 0, temporarySlots.Length);

        // PENTING: Lakukan backup dummification di awal untukAdd Pack agar event listener tidak Null
        BackupOriginalState();

        RefreshAllSlotTextsAndVisuals();
        UpdateSlotDeleteButtonsVisibility();
        TriggerPanelAnimation(customPackPanel, targetShowY);
        UpdateSlotInteractivity();
        
        UpdateSaveButtonInteractivity();
        // KUNCI BLOKIR UTAMA PANEL: Blokir Sidebar kecuali panah Speed
        MainMenuUIManager menuUI = Object.FindFirstObjectByType<MainMenuUIManager>();
        if (menuUI != null) menuUI.SetSidebarInteractable(false, true);
        
    }

    public void OpenEditPackPanel()
    {
        if (allLoadedPacks.Count == 0) return;
        
        // FIX CRASH OUT OF RANGE PROTECTION VIA MATH CLAMP ABSOLUT
        int safeIndex = Mathf.Clamp(currentPackIndex, 0, allLoadedPacks.Count - 1);
        ObjectPackConfig activePack = allLoadedPacks[safeIndex];
        if (!activePack.isCustom) return;
        
        currentEditingPack = activePack;
        currentSelectedSlotIndex = 0;

        System.Array.Clear(temporarySlots, 0, temporarySlots.Length);
        string packFolderPath = Path.Combine(rootCustomPacksPath, "Pack_" + activePack.packID);

        // Pengaman ukuran isi list items agar tidak meledak melebihi slot maksimal 3
        int safeItemCount = Mathf.Min(activePack.items.Count, 3);
        for (int i = 0; i < safeItemCount; i++) {
            temporarySlots[i] = new CustomItemData { 
                imageFileName = activePack.items[i].imageFileName, 
                sizeScale = activePack.items[i].sizeScale, 
                maxRotSpeed = activePack.items[i].maxRotSpeed 
            };
        }

        // Paksa nilai slider mengikuti memori objek indeks pertama (Slot 1) dari pack yang di-edit
        if (temporarySlots[0] != null)
        {
            if (sizeSlider != null) sizeSlider.value = temporarySlots[0].sizeScale;
            if (rotSpeedSlider != null) rotSpeedSlider.value = temporarySlots[0].maxRotSpeed;
        }
        else
        {
            if (sizeSlider != null) sizeSlider.value = 0.5f;
            if (rotSpeedSlider != null) rotSpeedSlider.value = 0.5f;
        }

        // KUNCI SINKRON: Tembak nilai teks dan buat backup SEBELUM UI Refresh dijalankan
        packNameInputField.text = activePack.packName;
        BackupOriginalState();

        RefreshAllSlotTextsAndVisuals(); 
        // Pemicu ulang status visibilitas tombol X tepat saat mode edit baru terbuka pertama kali
        UpdateSlotDeleteButtonsVisibility();

        if (temporarySlots[0] != null && !string.IsNullOrEmpty(temporarySlots[0].imageFileName)) {
            StartCoroutine(LoadSinglePreviewToPanel(Path.Combine(packFolderPath, temporarySlots[0].imageFileName)));
        }

        TriggerPanelAnimation(customPackPanel, targetShowY);
        UpdateSlotInteractivity();
        UpdateSaveButtonInteractivity();
        // KUNCI BLOKIR UTAMA PANEL: Blokir Sidebar kecuali panah Speed
        MainMenuUIManager menuUI = Object.FindFirstObjectByType<MainMenuUIManager>();
        if (menuUI != null) menuUI.SetSidebarInteractable(false, true);
    }

    // --- FIX CANCEL BUTTON: TARIK PANEL KEMBALI DAN KEMBALIKAN PREVIEW ASLI ---
    public void CancelAndRestorePack()
    {
        ClearPanelPreviewAsset();
        string tempPath = Path.Combine(rootCustomPacksPath, "Pack_Temp");
        try { if (Directory.Exists(tempPath)) Directory.Delete(tempPath, true); } catch {}

        // 2. Tutup panel perakit bawah dengan animasi geser keluar layar
        TriggerPanelAnimation(customPackPanel, hidePositionY);
        MainMenuUIManager menuUI = Object.FindFirstObjectByType<MainMenuUIManager>();
        if (menuUI != null) menuUI.SetSidebarInteractable(true, false);

        // 3. Reset status pelacakan indeks perakit
        currentSelectedSlotIndex = -1;
        currentEditingPack = null;

        // 4. Paksa ObjectPreviewManager mematikan objek kustom rakitan dan menampilkan kembali paket global utama
        if (previewManager != null)
        {
            previewManager.ReloadCurrentActivePackFromGlobalIndex();
        }
    }

    public void TriggerMainPreviewRefresh()
    {
        // Alihkan logika utama ke dalam Coroutine agar aman dari masalah delay spawn objek
        StartCoroutine(TriggerMainPreviewRefreshRoutine());
    }

    private IEnumerator TriggerMainPreviewRefreshRoutine()
    {
        // Tunggu hingga akhir frame agar Unity selesai men-spawn objek preview 3D/2D di layar
        yield return new WaitForEndOfFrame();

        int slotIndex = currentSelectedSlotIndex; 
        if (slotIndex == -1) yield break;

        Sprite spriteToRender = null;

        #if UNITY_WEBGL && !UNITY_EDITOR
        // JALUR WEBGL: Ambil gambar langsung dari RAM Cache
        if (WebGLTextureCache.Instance != null && WebGLTextureCache.Instance.cachedCustomSprites.ContainsKey(slotIndex))
        {
            spriteToRender = WebGLTextureCache.Instance.cachedCustomSprites[slotIndex];
        }
        #else
        // JALUR PC EXE & EDITOR: Cari berkas fisik gambar secara berlapis
        string tempFolderPath = Path.Combine(rootCustomPacksPath, "Pack_Temp");
        string packID = currentEditingPack == null ? "Temp" : currentEditingPack.packID;
        string backupFolderPath = Path.Combine(rootCustomPacksPath, "Pack_" + packID);

        string targetImagePath = Path.Combine(tempFolderPath, temporarySlots[slotIndex].imageFileName);
        
        if (!File.Exists(targetImagePath))
        {
            targetImagePath = Path.Combine(backupFolderPath, temporarySlots[slotIndex].imageFileName);
        }

        if (File.Exists(targetImagePath))
        {
            byte[] bytes = File.ReadAllBytes(targetImagePath);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (texture.LoadImage(bytes))
            {
                texture.filterMode = FilterMode.Point;
                spriteToRender = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
        }
        #endif

        if (spriteToRender != null)
        {
            if (placeholderTextMesh != null) 
            {
                placeholderTextMesh.text = "<color=green>Image Loaded Successfully!</color>";
            }

            if (panelItemPreviewImage != null)
            {
                panelItemPreviewImage.gameObject.SetActive(true);
                panelItemPreviewImage.sprite = spriteToRender;
                panelItemPreviewImage.preserveAspect = true;
                if (previewPlaceholderText != null) previewPlaceholderText.SetActive(false);
            }

            // AMANKAN SUNTIKAN SPRITE PREVIEW TENGAH (WEB & EDITOR)
            if (previewManager != null)
            {
                // Berikan jeda mikro tambahan agar komponen internal prefab preview siap
                yield return new Vector2(0f, 0f); 

                SpriteRenderer[] activeRenderers = previewManager.gameObject.GetComponentsInChildren<SpriteRenderer>(true);
                
                // Cek jika struktur objek anak terdeteksi
                if (activeRenderers.Length > 0)
                {
                    foreach (SpriteRenderer sr in activeRenderers)
                    {
                        if (!sr.gameObject.name.Contains("Background")) 
                        {
                            sr.sprite = spriteToRender;
                            sr.gameObject.SetActive(true);
                        }
                    }
                }
                previewManager.UpdateBestTimeDisplay();
            }
        }
    }

    void UpdateSlotInteractivity()
    {
        slotButtons[0].interactable = true;
        slotButtons[1].interactable = (temporarySlots[0] != null && !string.IsNullOrEmpty(temporarySlots[0].imageFileName));
        slotButtons[2].interactable = (temporarySlots[1] != null && !string.IsNullOrEmpty(temporarySlots[1].imageFileName));
        UpdateSaveButtonInteractivity();
    }

    public void ResetCustomPanelSliders()
    {
        if (currentSelectedSlotIndex == -1 || temporarySlots[currentSelectedSlotIndex] == null) return;

        // FIX RESET EDIT: Kembalikan teks input nama ke kondisi cadangan original awal
        packNameInputField.text = originalPackName;

        // 1. Kembalikan data internal ke nilai tengah default
        temporarySlots[currentSelectedSlotIndex].sizeScale = 0.5f;
        temporarySlots[currentSelectedSlotIndex].maxRotSpeed = 0.5f;

        // 2. Setel posisi handle UI Slider ke tengah
        sizeSlider.value = 0.5f;
        rotSpeedSlider.value = 0.5f;

        // 3. Paksa mini preview kembali ke skala default
        float panelVisualScale = Mathf.Lerp(0.6f, 1.2f, 0.5f);
        if (panelItemPreviewImage != null)
        {
            panelItemPreviewImage.rectTransform.localScale = new Vector3(panelVisualScale, panelVisualScale, 1f);
        }

        // 4. Paksa Main Preview Atas me-reset rotasi visual objek ke 0 derajat (Quaternion.identity)
        if (previewManager != null)
        {
            previewManager.ResetSingleObjectPhysicsAndRotation(currentSelectedSlotIndex);
        }

        UpdateSaveButtonInteractivity();
    }

    public void TriggerDeleteConfirmation()
    {
        if (allLoadedPacks.Count == 0) return;
        ObjectPackConfig activePack = allLoadedPacks[currentPackIndex];
        if (!activePack.isCustom) return; 

        packToDelete = activePack;
        // FIX: Pemicu animasi sliding naik untuk jendela pop-up delete
        TriggerPanelAnimation(deleteConfirmationPopup, targetShowY);

        MainMenuUIManager menuUI = Object.FindFirstObjectByType<MainMenuUIManager>();
        if (menuUI != null) menuUI.SetSidebarInteractable(false, false);
    }

    public void ConfirmDeleteYes()
    {
        if (packToDelete != null)
        {
            // KUNCI MUTLAK FIX HAPUS PERMANEN DI WEBGL BROWSER:
            // ==================================================================
            #if UNITY_WEBGL && !UNITY_EDITOR
            // Cari slot 1 sampai 5 di PlayerPrefs untuk menemukan pack yang mau dihapus
            for (int i = 1; i <= 5; i++)
            {
                string keyCheck = "WebGL_CustomPack_Slot_" + i;
                if (PlayerPrefs.HasKey(keyCheck))
                {
                    ObjectPackConfig savedPack = JsonUtility.FromJson<ObjectPackConfig>(PlayerPrefs.GetString(keyCheck));
                    // Jika ID paket kustom cocok dengan target hapus, musnahkan key-nya dari browser!
                    if (savedPack.packID == packToDelete.packID)
                    {
                        PlayerPrefs.DeleteKey(keyCheck);
                        break;
                    }
                }
            }
            PlayerPrefs.Save(); // Paksa browser melakukan sinkronisasi IndexedDB detik ini juga
            #else
            // VERSI PC EXE & EDITOR (Bawaan Asli Anda)
            string packFolderPath = Path.Combine(rootCustomPacksPath, "Pack_" + packToDelete.packID);
            if (Directory.Exists(packFolderPath)) Directory.Delete(packFolderPath, true);
            #endif
            // ==================================================================

            allLoadedPacks.Remove(packToDelete);
            packToDelete = null;

            currentPackIndex = Mathf.Clamp(currentPackIndex - 1, 0, allLoadedPacks.Count - 1);
            
            TriggerPanelAnimation(deleteConfirmationPopup, hidePositionY);
            TriggerPanelAnimation(customPackPanel, hidePositionY);

            MainMenuUIManager menuUI = Object.FindFirstObjectByType<MainMenuUIManager>();
            if (menuUI != null) menuUI.SetSidebarInteractable(true, false);

            if (WebGLTextureCache.Instance != null)
            {
                WebGLTextureCache.Instance.ClearCache();
            }
            
            UpdatePackPreviewDisplay();
        }
    }

    public void ConfirmDeleteNo()
    {
        packToDelete = null;
        // FIX ANIMASI SLIDING TURUN UTK PANEL POPUP SAAT BATAL
        TriggerPanelAnimation(deleteConfirmationPopup, hidePositionY);
        MainMenuUIManager menuUI = Object.FindFirstObjectByType<MainMenuUIManager>();
        if (menuUI != null) menuUI.SetSidebarInteractable(true, false);
    }

    private void TriggerPanelAnimation(RectTransform panel, float targetY)
    {
        if (panel == null) return;
        if (activePanelCoroutines.ContainsKey(panel) && activePanelCoroutines[panel] != null)
        {
            StopCoroutine(activePanelCoroutines[panel]);
        }
        activePanelCoroutines[panel] = StartCoroutine(SlidePanelRoutine(panel, targetY));
    }

    System.Collections.IEnumerator SlidePanelRoutine(RectTransform panel, float targetY)
    {
        float elapsed = 0f;
        Vector2 startPos = panel.anchoredPosition;
        Vector2 targetPos = new Vector2(startPos.x, targetY);

        if (targetY == targetShowY) panel.gameObject.SetActive(true);

        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animDuration);
            panel.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }

        panel.anchoredPosition = targetPos;
        if (targetY == hidePositionY) panel.gameObject.SetActive(false);
    }

    private IEnumerator LoadSinglePreviewToPanel(string path)
    {
        if (!File.Exists(path)) yield break;
        string url = "file://" + path;
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                texture.filterMode = FilterMode.Point;
                if (panelItemPreviewImage != null)
                {
                    panelItemPreviewImage.gameObject.SetActive(true);
                    panelItemPreviewImage.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    panelItemPreviewImage.preserveAspect = true;
                }
                if (previewPlaceholderText != null) previewPlaceholderText.SetActive(false);
                float val = temporarySlots[currentSelectedSlotIndex].sizeScale;
                float panelVisualScale = Mathf.Lerp(0.6f, 1.2f, val);
                panelItemPreviewImage.rectTransform.localScale = new Vector3(panelVisualScale, panelVisualScale, 1f);
            }
        }
    }
    
    public bool IsCurrentPackCustom()
    {
        if (allLoadedPacks.Count == 0) return false;
        return allLoadedPacks[currentPackIndex].isCustom;
    }


    public ObjectPackConfig GetActivePackConfig()
    {
        if (allLoadedPacks.Count == 0) return null;
        return allLoadedPacks[currentPackIndex];
    }

    public void SelectSlot(int slotIndex)
    {
        currentSelectedSlotIndex = slotIndex;
        if (temporarySlots[slotIndex] == null)
        {
            temporarySlots[slotIndex] = new CustomItemData { imageFileName = "", sizeScale = 1.0f, maxRotSpeed = 250f };
        }

        sizeSlider.value = temporarySlots[slotIndex].sizeScale;
        rotSpeedSlider.value = temporarySlots[slotIndex].maxRotSpeed;

        // Pemicu sistem upload gambar nyata
        StartCoroutine(UploadImageRoutine(slotIndex));
    }

    // --- COROUTINE UPLOAD GAMBAR DAN REFRESH PREVIEW PANEL ---

    // Mengubah skala ukuran gambar preview kecil di panel secara real-time saat slider digeser
    void UpdatePreviewScaleVisual()
    {
        if (currentSelectedSlotIndex != -1 && panelItemPreviewImage != null && temporarySlots[currentSelectedSlotIndex] != null)
        {
            float scaleFactor = temporarySlots[currentSelectedSlotIndex].sizeScale;
            // Skala gambar preview kecil di panel akan ikut membesar/mengecil secara proporsional
            panelItemPreviewImage.rectTransform.localScale = new Vector3(scaleFactor, scaleFactor, 1f);
        }
    }

    void UploadImageForSlot(int slotIndex)
    {
        // Catatan: Jika kamu menggunakan plugin seperti "SimpleFileBrowser" atau "NativeGallery",
        // ganti baris pembuka ini dengan fungsi pemanggil milik plugin tersebut.
        
        // Di bawah ini adalah logika standar Unity untuk mendeteksi file dan menyalinnya:
        string contohPathGambarDariUser = OpenFilePanelSimulated(); 

        if (string.IsNullOrEmpty(contohPathGambarDariUser) || !File.Exists(contohPathGambarDariUser))
        {
            Debug.LogWarning("Upload dibatalkan atau file tidak valid.");
            return;
        }

        // Ambil nama file asli (misal: "kucing_imut.png")
        string fileName = Path.GetFileName(contohPathGambarDariUser);

        // Tentukan folder tujuan paket kustom saat ini
        string packID = currentEditingPack == null ? System.DateTime.Now.Ticks.ToString() : currentEditingPack.packID;
        string packFolderPath = Path.Combine(rootCustomPacksPath, "Pack_" + packID);

        // Buat foldernya terlebih dahulu jika belum ada (mode rakit baru)
        if (!Directory.Exists(packFolderPath)) Directory.CreateDirectory(packFolderPath);

        // Tentukan jalur absolut file di dalam folder Object Packs
        string targetCopyPath = Path.Combine(packFolderPath, fileName);

        try
        {
            // Salin file gambar asli kiriman pemain ke dalam folder Object Packs game secara permanen
            File.Copy(contohPathGambarDariUser, targetCopyPath, true);
            Debug.Log($"[Upload Sukses] Gambar berhasil disimpan ke: {targetCopyPath}");

            // Catat nama file ke dalam data temporary slots
            temporarySlots[slotIndex].imageFileName = fileName;
            
            // Perbarui teks tombol di UI agar menampilkan nama file gambar yang sukses diunggah
            slotButtons[slotIndex].GetComponentInChildren<TextMeshProUGUI>().text = fileName;

            // --- REFRESH VISUAL PREVIEW ---
            // Perintahkan ObjectPreviewManager untuk langsung me-load gambar baru ini ke dalam kotak preview menu
            if (previewManager != null)
            {
                previewManager.LoadObjectPackFromCustomData(temporarySlots, packFolderPath);
            }

            UpdateSlotInteractivity();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Gagal menyalin file gambar: {e.Message}");
        }
    }

    // Fungsi Pembantu Simulasi: Kamu bisa mengganti return string ini dengan hasil path absolut dari plugin File Browser pilihanmu
    private string OpenFilePanelSimulated()
    {
        // Jika kamu build di PC/Windows, kamu bisa menggunakan fungsi bawaan Unity Editor untuk testing:
        #if UNITY_EDITOR
        return UnityEditor.EditorUtility.OpenFilePanel("Pilih Gambar Objek (PNG/JPG)", "", "png,jpg,jpeg");
        #else
        // Untuk versi build (.exe / Android), silakan ganti dengan return path dari plugin Native Gallery / File Browser kamu
        return ""; 
        #endif
    }
}