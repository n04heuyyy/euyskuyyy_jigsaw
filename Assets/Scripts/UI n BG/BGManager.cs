using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BGManager : MonoBehaviour
{
    [Header("UI Canvas Background Component")]
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
        }
    }
}