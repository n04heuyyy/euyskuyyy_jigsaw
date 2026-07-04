using UnityEngine;

public class PuzzlePiece : MonoBehaviour
{
    public int correctX, correctY;
    public int currentRotation = 0; 
    
    private int currentX, currentY; // Menyimpan koordinat kotak saat ini di papan
    private bool isLockedPermanently = false;
    private Vector3 correctWorldPosition;
    private int gridX;
    private int gridY;
    private int currentGridSize;
    private bool isUVInitialized = false;
    private GameManager gameManager;
    private Renderer myRenderer;
    private MaterialPropertyBlock propBlock; 

    // Digunakan GameManager agar bisa membaca koordinatnya
    public int GetCurrentX() => currentX;
    public int GetCurrentY() => currentY;

    void Awake()
    {
        myRenderer = GetComponent<Renderer>();
    }

    // Paksa potongan gambar terus bergerak mengikuti kamera live-feed
    void Update()
    {
        if (isUVInitialized && myRenderer != null)
        {
            // Hitung ulang skala (Tiling) dan pergeseran (Offset) video live feed
            float tilingX = 1f / currentGridSize;
            float tilingY = 1f / currentGridSize;
            float offsetX = gridX * tilingX;
            float offsetY = gridY * tilingY;

            // Ambil data Property Block saat ini dari renderer kepingan
            myRenderer.GetPropertyBlock(propBlock);

            // Suntikkan koordinat potong baru ke shader standar Unity (_MainTex_ST)
            propBlock.SetVector("_MainTex_ST", new Vector4(tilingX, tilingY, offsetX, offsetY));

            // Terapkan kembali ke renderer secara instan di frame ini
            myRenderer.SetPropertyBlock(propBlock);
        }
    }

    public void SetupPiece(int x, int y, int totalColumns, int totalRows, Vector3 targetWorldPos, GameManager manager)
    {
        correctX = x;
        correctY = y;
        correctWorldPosition = targetWorldPos;
        gameManager = manager;
        isLockedPermanently = false;

        // Set koordinat awal ke minus/luar grid agar tidak langsung dianggap menang saat acak
        currentX = -99;
        currentY = -99;

        float tilingX = 1f / totalColumns;
        float tilingY = 1f / totalRows;
        float offsetX = x * tilingX;
        float offsetY = y * tilingY;

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        myRenderer.GetPropertyBlock(block);
        block.SetVector("_MainTex_ST", new Vector4(tilingX, tilingY, offsetX, offsetY));
        myRenderer.SetPropertyBlock(block);
    }

    public void RotatePiece()
    {
        if (isLockedPermanently) return;

        transform.Rotate(0, 0, -90);
        currentRotation = (currentRotation + 1) % 4;
        
        // Setiap rotasi, cek apakah mengubah status kemenangan game
        gameManager.CheckWinCondition();
    }

    // FSnap magnetik bebas ke grid terdekat
    public void SnapToNearestGrid()
    {
        if (isLockedPermanently) return;

        // Hitung seberapa jauh posisi kepingan saat ini dari titik awal pojok kiri bawah grid
        float localX = transform.position.x - gameManager.originGridPos.x;
        float localY = transform.position.y - gameManager.originGridPos.y;

        // Cari indeks grid terdekat menggunakan pembulatan matematis
        int targetGridX = Mathf.RoundToInt(localX / gameManager.pieceSize);
        int targetGridY = Mathf.RoundToInt(localY / gameManager.pieceSize);

        // Cek apakah posisi lepasnya masih masuk area papan puzzle
        if (targetGridX >= 0 && targetGridX < gameManager.gridSize &&
            targetGridY >= 0 && targetGridY < gameManager.gridSize)
        {
            // Cek apakah koordinat petak ini sudah ada kepingan lain
            if (gameManager.IsGridSlotOccupied(targetGridX, targetGridY, this))
            {
                // Jika sudah diisi, batalkan snap
                // Tendang status kepingan ini ke luar grid dan biarkan posisinya tetap di tempat dilepas
                currentX = -1;
                currentY = -1;
            }
            else
            {
                // Jika kosong, izinkan snap ke tengah petak tersebut
                currentX = targetGridX;
                currentY = targetGridY;

                Vector3 snapWorldPos = new Vector3(
                    gameManager.originGridPos.x + (currentX * gameManager.pieceSize),
                    gameManager.originGridPos.y + (currentY * gameManager.pieceSize),
                    transform.position.z
                );
                transform.position = snapWorldPos;
            }
        }
        else
        {
            // Jika dilepas di luar area papan puzzle (di tempat sebaran), reset status koordinatnya
            currentX = -1;
            currentY = -1;
        }

        // Selalu cek kondisi menang setiap kali ada kepingan yang berpindah tempat
        gameManager.CheckWinCondition();
    }

    public bool CheckIfCorrect()
    {
        // Kepingan dianggap benar hanya jika posisi grid tepat dan rotasinya kembali tegak (0)
        return (currentX == correctX && currentY == correctY && currentRotation == 0);
    }

    public void LockPiecePermanently()
    {
        isLockedPermanently = true;
    }

    public bool IsLocked() => isLockedPermanently; // Kunci posisi piece (dipakai kalau win)

    // Fungsi pembantu yang dipanggil oleh gameManager saat inisialisasi
    public void SetLiveUVCoordinates(int x, int y, int size)
    {
        gridX = x;
        gridY = y;
        currentGridSize = size;
        
        myRenderer = GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();
        isUVInitialized = true;
    }
}