using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking; 
using System.IO;

public class PuzzleObjectManager : MonoBehaviour
{
    [Header("Render Texture Setup")]
    public int textureResolution = 1024;
    public Shader puzzleShader; 

    private Material masterMaterial;
    private RenderTexture masterRenderTexture;

    [Header("Base Target Prefab")]
    [SerializeField] private GameObject baseTargetPrefab;
    private List<LocalBoundEnforcer> activeEnforcers = new List<LocalBoundEnforcer>();

    public void InitializeMultiObjectPuzzle(GameManager gameManager)
    {
        if (gameManager == null) return;

        GameObject sharedIsolationAnchor = new GameObject("Puzzle Anchor");
        sharedIsolationAnchor.transform.position = new Vector3(200f, -50f, 0f);

        activeEnforcers.Clear();

        // Mulai jalankan proses pemuatan via Coroutine
        StartCoroutine(SetupPuzzleObjectsSequenceRoutine(gameManager, sharedIsolationAnchor));
    }

    private IEnumerator SetupPuzzleObjectsSequenceRoutine(GameManager gameManager, GameObject sharedIsolationAnchor)
    {
        string chosenPackID = PlayerPrefs.GetString("SelectedObjectPackID", "");
        bool isCustomPack = PlayerPrefs.GetInt("SelectedObjectPackIsCustom", 0) == 1;

        List<CustomItemData> targetItems = new List<CustomItemData>();

        #if UNITY_WEBGL && !UNITY_EDITOR
        if (isCustomPack)
        {
            // Ambil data pack kustom dari PlayerPrefs Browser yang sudah didamankan sebelumnya
            string webKey = "";
            for (int i = 1; i <= 5; i++)
            {
                string keyCheck = "WebGL_CustomPack_Slot_" + i;
                if (PlayerPrefs.HasKey(keyCheck))
                {
                    ObjectPackConfig savedPack = JsonUtility.FromJson<ObjectPackConfig>(PlayerPrefs.GetString(keyCheck));
                    if (savedPack.packID == chosenPackID) { webKey = keyCheck; break; }
                }
            }

            if (!string.IsNullOrEmpty(webKey))
            {
                string jsonText = PlayerPrefs.GetString(webKey);
                ObjectPackConfig pack = JsonUtility.FromJson<ObjectPackConfig>(jsonText);
                targetItems = pack.items;
            }
        }
        else
        {
            // Ambil data pack default dari internal web server StreamingAssets via UnityWebRequest
            string urlPath = Application.streamingAssetsPath + "/Default Packs/" + chosenPackID + "/pack_config.json";
            urlPath = urlPath.Replace("\\", "/");

            using (UnityWebRequest webRequest = UnityWebRequest.Get(urlPath))
            {
                yield return webRequest.SendWebRequest();
                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    ObjectPackConfig pack = JsonUtility.FromJson<ObjectPackConfig>(webRequest.downloadHandler.text);
                    targetItems = pack.items;
                }
            }
        }
        #else
        // PC/Editor: Gunakan operasi file IO lokal instan bawaan
        string packFolderPath = "";
        if (isCustomPack)
            packFolderPath = Path.Combine(Application.persistentDataPath, "Object Packs", "Pack_" + chosenPackID);
        else
            packFolderPath = Path.Combine(Application.streamingAssetsPath, "Default Packs", chosenPackID);

        string configFilePath = Path.Combine(packFolderPath, "pack_config.json");
        if (File.Exists(configFilePath))
        {
            string jsonText = File.ReadAllText(configFilePath);
            ObjectPackConfig pack = JsonUtility.FromJson<ObjectPackConfig>(jsonText);
            targetItems = pack.items;
        }
        yield return null; 
        #endif

        // Spawn dan load objek sprite puzzle
        int maxItemsCount = targetItems != null ? targetItems.Count : 0;
        int objectCount = Mathf.Min(maxItemsCount, 3);

        for (int i = 0; i < objectCount; i++)
        {
            CustomItemData itemData = targetItems[i];
            if (itemData == null || string.IsNullOrEmpty(itemData.imageFileName)) continue;

            Sprite loadedSprite = null;

            #if UNITY_WEBGL && !UNITY_EDITOR
            if (isCustomPack)
            {
                // Jika pack kustom, bypass ambil Sprite langsung dari RAM Cache Global
                if (WebGLTextureCache.Instance != null && WebGLTextureCache.Instance.cachedCustomSprites.ContainsKey(i))
                {
                    loadedSprite = WebGLTextureCache.Instance.cachedCustomSprites[i];
                }
            }
            else
            {
                // Jika pack default di WebGL, unduh file gambarnya asinkron dari folder StreamingAssets web server
                string imgUrl = Application.streamingAssetsPath + "/Default Packs/" + chosenPackID + "/" + itemData.imageFileName;
                imgUrl = imgUrl.Replace("\\", "/");

                using (UnityWebRequest texRequest = UnityWebRequestTexture.GetTexture(imgUrl))
                {
                    yield return texRequest.SendWebRequest();
                    if (texRequest.result == UnityWebRequest.Result.Success)
                    {
                        Texture2D tex = DownloadHandlerTexture.GetContent(texRequest);
                        tex.filterMode = FilterMode.Point;
                        loadedSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    }
                }
            }
            #else
            // PC dan editor: Ambil berkas fisik gambar di harddisk secara instan
            string imagePath = Path.Combine(packFolderPath, itemData.imageFileName);
            if (File.Exists(imagePath))
            {
                loadedSprite = LoadSpriteFromPath(imagePath);
            }
            #endif

            // Jika gagal memuat gambar pada iterasi ini, lewati spawn objek agar tidak merusak arena
            if (loadedSprite == null) continue;

            GameObject spawnedTarget = Instantiate(baseTargetPrefab, sharedIsolationAnchor.transform);
            spawnedTarget.transform.localPosition = new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(-1.2f, 1.2f), 0f);

            SpriteRenderer sr = spawnedTarget.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sprite = loadedSprite;

            LocalBoundEnforcer enforcer = spawnedTarget.GetComponent<LocalBoundEnforcer>();
            if (enforcer == null) enforcer = spawnedTarget.AddComponent<LocalBoundEnforcer>();
            
            enforcer.SetupBounds(3f, 3f);
            
            // Atur skala normalisasi visual agar seimbang
            float maxSpriteDimension = Mathf.Max(loadedSprite.bounds.size.x, loadedSprite.bounds.size.y);
            float normalizer = 1.0f / maxSpriteDimension;
            float targetClampedSize = Mathf.Lerp(2.4f, 4.6f, itemData.sizeScale);
            spawnedTarget.transform.localScale = new Vector3(normalizer * targetClampedSize, normalizer * targetClampedSize, 1f);
            
            float startDeviation = itemData.maxRotSpeed - 0.5f;
            enforcer.maxRotationSpeed = (Mathf.Abs(startDeviation) < 0.04f) ? 0f : startDeviation * -500f;
            enforcer.useCustomPanelRotation = true;
            enforcer.ApplyCustomPanelRotation();
            
            activeEnforcers.Add(enforcer);
        }

        // Buat trender texture dan kirim material ke game manager
        GameObject camObj = new GameObject("MasterTargetCamera");
        camObj.transform.SetParent(sharedIsolationAnchor.transform);
        camObj.transform.localPosition = new Vector3(0f, 0f, -10f);

        Camera masterCam = camObj.AddComponent<Camera>();
        masterRenderTexture = new RenderTexture(textureResolution, textureResolution, 24, RenderTextureFormat.ARGB32);
        masterRenderTexture.useMipMap = false;
        masterRenderTexture.filterMode = FilterMode.Point;
        masterRenderTexture.Create();

        masterCam.targetTexture = masterRenderTexture;
        masterCam.orthographic = true;
        masterCam.orthographicSize = 3f; 
        masterCam.aspect = 1.0f;
        masterCam.cullingMask = ~0; 
        masterCam.clearFlags = CameraClearFlags.SolidColor;
        masterCam.backgroundColor = new Color(1f, 1f, 1f, 0.2f);

        masterMaterial = new Material(puzzleShader);
        masterMaterial.mainTexture = masterRenderTexture;
        masterMaterial.mainTexture.wrapMode = TextureWrapMode.Clamp;

        List<Material> matsToSend = new List<Material> { masterMaterial };
        gameManager.SetupMultiObjectGrid(matsToSend);
    }

    private Sprite LoadSpriteFromPath(string path)
    {
        if (!File.Exists(path)) return null;
        byte[] textureBytes = File.ReadAllBytes(path);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (texture.LoadImage(textureBytes)) {
            texture.filterMode = FilterMode.Point;
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
        return null;
    }
}