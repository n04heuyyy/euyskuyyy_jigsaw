using System.Collections.Generic;
using UnityEngine;

public class GameplayDataBridge : MonoBehaviour
{
    public static GameplayDataBridge Instance { get; private set; }

    // Menyimpan data paket kustom yang sedang dipilih untuk dimainkan
    public bool isPlayingCustomPack = false;
    public CustomPack activeCustomPack;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}