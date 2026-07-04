using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class GameManager : MonoBehaviour
{
    [Header("Grid Configurations")]
    [Range(2, 6)] public int gridSize = 4; 
    public float totalPuzzleAreaSize = 5f; 
    public GameObject piecePrefab;
    public SpriteRenderer puzzleFrameRenderer;

    public GameUIManager uiManager;

    [Tooltip("Geser seluruh area puzzle ke kanan (Sumbu X positif) agar tidak tertutup UI")]
    public float offsetXToRight = 2f; 

    // Variabel untuk melacak koordinat Z paling depan saat ini.
    // Semakin minus nilainya, objek akan semakin dekat ke kamera (berada di paling depan)
    private float currentFrontZ = -0.01f;

    [Header("Scattering Area")]
    public Vector2 scatterMinBounds; 
    public Vector2 scatterMaxBounds; 

    // Variabel publik agar bisa diakses oleh PuzzlePiece untuk rumus Snap
    [HideInInspector] public float pieceSize;
    [HideInInspector] public Vector2 originGridPos;

    private List<PuzzlePiece> allPieces = new List<PuzzlePiece>();
    private List<Material> activeMaterials = new List<Material>();
    private PuzzlePiece selectedPiece;
    private Vector3 dragOffset;

    [Header("Gameplay Audio AudioSources & Clips")]
    [SerializeField] private AudioSource gameplayAudioSource; 
    [SerializeField] private AudioClip dragSFX;
    [SerializeField] private AudioClip dropSFX;
    [SerializeField] private AudioClip rotateSFX;
    [SerializeField] private AudioClip solveSuccessSFX;

    [Header("Manager Connection")]
    [SerializeField] private PuzzleObjectManager puzzleObjectManager;

    // Variabel pembantu untuk mencatat waktu klik ganda di mobile/PC
    private float lastClickTime = 0f;
    private const float DOUBLE_CLICK_TIME_THRESHOLD = 0.3f;

    void Start()
    {
        // PlayerPrefs.GetInt/GetFloat mengambil data berdasarkan kata kunci (Key) yang kita simpan di MainMenuUIManager.
        // Angka di sebelah kanan (seperti 4, 5f, 0) adalah nilai cadangan (Fallback) jika data dari menu tidak ditemukan.

        if (PlayerPrefs.HasKey("ChosenGridSize"))
        {
            gridSize = PlayerPrefs.GetInt("ChosenGridSize", 4);
        }

        float savedMusicVol = PlayerPrefs.GetFloat("MusicVolume", 0.75f);
        float savedSFXVol = PlayerPrefs.GetFloat("SFXVolume", 0.75f);

        AudioSource[] allAudioSources = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        foreach (AudioSource source in allAudioSources)
        {
            if (source != null)
            {
                // Selama AudioSource tersebut di-set Loop, wajib mengikuti slider Music
                if (source.loop)
                {
                    source.volume = savedMusicVol;
                }
                else
                {
                    source.volume = savedSFXVol;
                }
            }
        }

        // Minta data ke PuzzleObjectManager saat dia sudah siap
        if (puzzleObjectManager != null)
        {
            puzzleObjectManager.InitializeMultiObjectPuzzle(this);
        }

        // Jalankan Coroutine pengaman untuk menyuntikkan fisik Speed dan Rotasi tanpa delay
        StartCoroutine(ApplyPhysicsSettingsRoutine());
    }
    
    public void SetupMultiObjectGrid(List<Material> uniqueMaterials)
    {
        activeMaterials = uniqueMaterials;
        // Jalankan pembuatan grid utama
        GeneratePuzzleGrid();
    }
    
    public void GeneratePuzzleGrid()
    {
        // Bersihkan objek lama di Hierarchy
        foreach (PuzzlePiece piece in allPieces) 
        {
            if (piece != null) Destroy(piece.gameObject);
        }
        allPieces.Clear();

        if (activeMaterials == null || activeMaterials.Count == 0) return;

        // Ambil Master Material tunggal
        Material masterMat = activeMaterials[0];

        // 'totalPuzzleAreaSize' di Inspector bertindak sebagai batas maksimal bingkai luar
        // Sesuaikan padding agar kepingan pas berada di dalam garis abu-abu
        float innerPadding = 0.8f; 
        float usablePuzzleArea = totalPuzzleAreaSize - innerPadding;

        pieceSize = usablePuzzleArea / gridSize;
        Vector3 newPieceScale = new Vector3(pieceSize, pieceSize, 1f);

        // Hitung koordinat pojok kiri bawah berdasarkan area konstan
        originGridPos = new Vector2(
            -(usablePuzzleArea / 2f) + (pieceSize / 2f) + offsetXToRight,
            -(usablePuzzleArea / 2f) + (pieceSize / 2f)
        );

        // Set ukuran bingkai luar agar sesuai semua opsi grid
        if (puzzleFrameRenderer != null)
        {
            float framePadding = 0.1f; 
            puzzleFrameRenderer.size = new Vector2(totalPuzzleAreaSize + framePadding, totalPuzzleAreaSize + framePadding);
            puzzleFrameRenderer.transform.position = new Vector3(offsetXToRight, 0f, 0.1f);
        }

        int currentSlotIndex = 0;

        // Looping grid (satu gambar utuh dipotong2)
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                GameObject newPieceObj = Instantiate(piecePrefab, this.transform); 
                newPieceObj.name = $"Piece_Slot_{currentSlotIndex}_Grid_{x}_{y}";
                newPieceObj.transform.localScale = newPieceScale;

                Vector3 correctWorldPos = new Vector3(
                    originGridPos.x + (x * pieceSize),
                    originGridPos.y + (y * pieceSize),
                    0
                );

                PuzzlePiece piece = newPieceObj.GetComponent<PuzzlePiece>();
                piece.SetupPiece(x, y, gridSize, gridSize, correctWorldPos, this);

                Renderer pieceRenderer = newPieceObj.GetComponent<Renderer>();
                if (pieceRenderer != null)
                {
                    // Gunakan Master Material yang sama ke semua kepingan puzzle
                    pieceRenderer.material = masterMat; 

                    // Alirkan koordinat potong agar ditangani secara live di skrip PuzzlePiece
                    piece.SetLiveUVCoordinates(x, y, gridSize);
                }

                // Pengacakan posisi aman agar tidak keluar layar (menggunakan pendorong radius)
                float edgePaddingMultiplier = (gridSize <= 3) ? 1.0f : 0.5f;
                float safeDistance = pieceSize * edgePaddingMultiplier;

                float safeMinX = scatterMinBounds.x + safeDistance;
                float safeMaxX = scatterMaxBounds.x - safeDistance;
                float safeMinY = scatterMinBounds.y + safeDistance;
                float safeMaxY = scatterMaxBounds.y - safeDistance;

                if (safeMinX > safeMaxX) { float temp = safeMinX; safeMinX = safeMaxX; safeMaxX = temp; }
                if (safeMinY > safeMaxY) { float temp = safeMinY; safeMinY = safeMaxY; safeMaxY = temp; }

                Vector3 randomScatterPos = new Vector3(
                    Random.Range(safeMinX, safeMaxX),
                    Random.Range(safeMinY, safeMaxY),
                    0
                );
                newPieceObj.transform.position = randomScatterPos;

                int randomRotationCount = Random.Range(0, 4);
                for (int i = 0; i < randomRotationCount; i++) newPieceObj.transform.Rotate(0, 0, -90);
                piece.currentRotation = randomRotationCount;

                allPieces.Add(piece);
                currentSlotIndex++;
            }
        }
    }

    void Update()
    {
        Mouse currentMouse = Mouse.current;
        if (currentMouse == null) return;

        Vector2 mousePosition = currentMouse.position.ReadValue();

        if (currentMouse.leftButton.wasPressedThisFrame)
        {
            // Ambil selisih waktu ketukan klik kiri / sentuhan layar untuk deteksi Double Click/Tap
            float timeSinceLastClick = Time.time - lastClickTime;
            lastClickTime = Time.time;

            if (timeSinceLastClick <= DOUBLE_CLICK_TIME_THRESHOLD)
            {
                // Mobile: Dobel klik rotate kepingan puzzle
                HandleMobileRotate(mousePosition);
            }
            else
            {
                // Klik kiri: Memulai drag kepingan
                HandleClickLeft(mousePosition);
            }
        }
        if (currentMouse.leftButton.isPressed && selectedPiece != null) HandleDrag(mousePosition);
        if (currentMouse.leftButton.wasReleasedThisFrame && selectedPiece != null) HandleDrop();
        
        // Tetap perbolehkan Klik Kanan sebagai alternatif rotasi tercepat di Windows/Mac
        if (currentMouse.rightButton.wasPressedThisFrame) HandleClickRight(mousePosition);
    }

    void HandleClickLeft(Vector2 mousePos)
    {
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            PuzzlePiece piece = hit.collider.GetComponent<PuzzlePiece>();
            if (piece != null && !piece.IsLocked())
            {
                selectedPiece = piece;

                // Saat di drag, bawa ke paling depan layar
                // Kurangi nilai Z global sedikit saja agar paling dekat dengan kamera
                currentFrontZ -= 0.001f; 
                
                // Berikan nilai Z baru tersebut ke kepingan yang sedang ditarik
                Vector3 tempPos = selectedPiece.transform.position;
                tempPos.z = currentFrontZ;
                selectedPiece.transform.position = tempPos;

                Vector3 worldMousePos = Camera.main.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, Camera.main.nearClipPlane));
                dragOffset = selectedPiece.transform.position - new Vector3(worldMousePos.x, worldMousePos.y, selectedPiece.transform.position.z);
                if (gameplayAudioSource != null && dragSFX != null)
                {
                    gameplayAudioSource.PlayOneShot(dragSFX, PlayerPrefs.GetFloat("SFXVolume", 0.75f));
                }
            }
        }
    }

    void HandleDrag(Vector2 mousePos)
    {
        // Konversi posisi layar mouse ke posisi dunia (World Space)
        Vector3 worldMousePos = Camera.main.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, Mathf.Abs(Camera.main.transform.position.z)));
        
        // Hitung target posisi kepingan berdasarkan pergerakan mouse + offset awal
        float targetX = worldMousePos.x + dragOffset.x;
        float targetY = worldMousePos.y + dragOffset.y;

        // Gunakan setengah ukuran kepingan sebagai padding agar bodi kepingan tidak keluar layar saat ditarik mouse
        float radiusPadding = pieceSize / 2f;

        float clampedX = Mathf.Clamp(targetX, scatterMinBounds.x + radiusPadding, scatterMaxBounds.x - radiusPadding);
        float clampedY = Mathf.Clamp(targetY, scatterMinBounds.y + radiusPadding, scatterMaxBounds.y - radiusPadding);

        selectedPiece.transform.position = new Vector3(clampedX, clampedY, selectedPiece.transform.position.z);
        }

    void HandleDrop()
    {
        // Janagn kembalikan Z ke 0, tapi kunci posisi Z kepingan ini pada nilai 'currentFrontZ'
        // terbaru agar dia tetap berada di atas kepingan yang tidak disentuh
        Vector3 tempPos = selectedPiece.transform.position;
        tempPos.z = currentFrontZ;
        selectedPiece.transform.position = tempPos;

        selectedPiece.SnapToNearestGrid(); 
        selectedPiece = null;

        if (gameplayAudioSource != null && dropSFX != null)
        {
            gameplayAudioSource.PlayOneShot(dropSFX, PlayerPrefs.GetFloat("SFXVolume", 0.75f));
        }
    }

    void HandleClickRight(Vector2 mousePos)
    {
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            PuzzlePiece piece = hit.collider.GetComponent<PuzzlePiece>();
            if (piece != null) piece.RotatePiece();
            if (gameplayAudioSource != null && rotateSFX != null)
            {
                gameplayAudioSource.PlayOneShot(rotateSFX, PlayerPrefs.GetFloat("SFXVolume", 0.75f));
            }
        }
    }

    void HandleMobileRotate(Vector2 mousePos)
    {
        // Batalkan proses seret jika tidak sengaja dipicu double tap saat menggenggam objek
        selectedPiece = null;

        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            PuzzlePiece piece = hit.collider.GetComponent<PuzzlePiece>();
            if (piece != null && !piece.IsLocked())
            {
                piece.RotatePiece();

                if (gameplayAudioSource != null && rotateSFX != null)
                {
                    gameplayAudioSource.PlayOneShot(rotateSFX, PlayerPrefs.GetFloat("SFXVolume", 0.75f));
                }
            }
        }
    }

    public void CheckWinCondition()
    {
        bool isAllCorrect = true;
        foreach (PuzzlePiece piece in allPieces)
        {
            if (!piece.CheckIfCorrect())
            {
                isAllCorrect = false;
                break;
            }
        }

        if (isAllCorrect)
        {
            foreach (PuzzlePiece piece in allPieces) piece.LockPiecePermanently();
            
            if (gameplayAudioSource != null && solveSuccessSFX != null)
            {
                gameplayAudioSource.PlayOneShot(solveSuccessSFX, PlayerPrefs.GetFloat("SFXVolume", 0.75f));
            }

            // Bawa ke GameUIManager untuk mematikan detak jam
            if (uiManager != null)
            {
                uiManager.StopTimerOnWin(); 
            }
        }
    }

    // Fungsi untuk memeriksa apakah koordinat grid tertentu sudah ada penghuninya
    public bool IsGridSlotOccupied(int targetX, int targetY, PuzzlePiece checkingPiece)
    {
        foreach (PuzzlePiece piece in allPieces)
        {
            // Jangan bandingkan kepingan dengan dirinya sendiri
            if (piece == checkingPiece) continue;

            // Jika ada kepingan lain yang koordinat grid-nya saat ini sama dengan yang diincar
            if (piece.GetCurrentX() == targetX && piece.GetCurrentY() == targetY)
            {
                return true; // Slot sudah terisi
            }
        }
        return false; // Slot kosong dan aman ditempati
    }

    private IEnumerator ApplyPhysicsSettingsRoutine()
    {
        // Tunggu sejenak agar komponen Rigidbody2D di semua prefab selesai di-spawn
        // dan status memori fisika Unity sudah terbangun 100%
        yield return new WaitForSeconds(0.05f);

        // Ambil parameter nilai dari menu utama
        float loadedSpeed = PlayerPrefs.GetFloat("ChosenSpeedValue", 5f);
        
        // Mengambil posisi slider terakhir (misal di menu diatur berputar ke kanan/kiri)
        // Jika tidak ada data di menu, default ke 0.5f (artinya diam tidak berputar)
        float loadedSliderRotationValue = PlayerPrefs.GetFloat("ChosenRotationSliderValue", 0.5f); 

        // Cari semua objek memantul yang aktif di gameplay scene
        LocalBoundEnforcer[] gameplayEnforcers = Object.FindObjectsByType<LocalBoundEnforcer>(FindObjectsSortMode.None);
    
        foreach (LocalBoundEnforcer enforcer in gameplayEnforcers)
        {
            if (enforcer != null)
            {
                // Bangunkan paksa
                Rigidbody2D rb = enforcer.GetComponent<Rigidbody2D>();
                if (rb != null) rb.WakeUp();

                // Suntikkan kecepatan gerak
                enforcer.UpdateMovementSpeed(loadedSpeed);

                // Suntikkan arah dan kecepatan rotasi slider
                enforcer.SetRotationFromSlider(loadedSliderRotationValue, enforcer.maxRotationSpeed);
            }
        }
    }
}