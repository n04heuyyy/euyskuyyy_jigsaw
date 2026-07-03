using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // Wajib diaktifkan untuk memanipulasi UI Image Canvas

public class BGManager : MonoBehaviour
{
    [Header("UI Canvas Background Component")]
    // Tarik objek UI Image Background Canvas kamu ke kolom ini di Inspector (Berlaku untuk Menu maupun Gameplay)
    [SerializeField] private Image canvasBackgroundImage; 
    
    [Header("Asset Koleksi Latar Belakang")]
    [SerializeField] private List<Sprite> backgroundSprites = new List<Sprite>();

    void Start()
    {
        // Otomatis membaca memori kiriman indeks dari menu utama saat scene dimuat
        if (PlayerPrefs.HasKey("ChosenBackgroundIndex"))
        {
            int savedBGIndex = PlayerPrefs.GetInt("ChosenBackgroundIndex", 0);
            ApplyBackground(savedBGIndex);
        }
    }

    public void ApplyBackground(int bgIndex)
    {
        if (backgroundSprites == null || backgroundSprites.Count == 0) return;

        // Amankan indeks agar looping memutar kembali jika melebihi total jumlah aset
        int safeIndex = bgIndex % backgroundSprites.Count;

        // Terapkan gambar ke komponen UI Image Canvas
        if (canvasBackgroundImage != null)
        {
            canvasBackgroundImage.sprite = backgroundSprites[safeIndex];
            Debug.Log($"[BackgroundManager] UI Canvas Background berhasil disinkronkan ke: {backgroundSprites[safeIndex].name}");
        }
        else
        {
            Debug.LogWarning("[BackgroundManager] Komponen 'canvasBackgroundImage' belum ditarik di Inspector!");
        }
    }
}