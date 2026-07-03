using System.Collections.Generic;

// Data untuk masing-masing item di dalam paket
[System.Serializable]
public class CustomItemData
{
    public string imageFileName;    // Mengacu ke nama file gambar di dalam folder paket
    public float sizeScale = 1.0f;   // Batas ukuran (0.75f - 1.5f)
    public float maxRotSpeed = 250f; // Kecepatan rotasi kustom item
}

// Data struktur paket versi JSON Folder yang dipakai CustomPackManager
[System.Serializable]
public class ObjectPackConfig
{
    public string packID;
    public string packName;
    public bool isCustom; // TRUE = Paket Kustom, FALSE = Paket Bawaan (Terkunci)
    public List<CustomItemData> items = new List<CustomItemData>();
}

// Data struktur paket versi Gameplay yang dicari oleh GameplayDataBridge & PuzzleObjectManager
[System.Serializable]
public class CustomPack
{
    public string packID;
    public string packName;
    public List<CustomItemData> items = new List<CustomItemData>();
}