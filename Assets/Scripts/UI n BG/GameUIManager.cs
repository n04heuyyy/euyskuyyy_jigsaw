using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; // Wajib untuk membaca klik sembarang
using TMPro;

public class GameUIManager : MonoBehaviour
{
    [Header("Timer UI")]
    [SerializeField] private TextMeshProUGUI timerText;
    private float elapsedTime = 0f;
    private bool isTimerRunning = true;

    [Header("Reset Button UI (Vertical Slide)")]
    [SerializeField] private Button resetButton;
    [SerializeField] private RectTransform confirmationTextPanel; // Panel teks konfirmasi
    [SerializeField] private float slideSpeed = 600f;

    [Header("Pause Configurations")]
    [SerializeField] private GameObject pauseOverlayPanel; // Panel transparan penanda Pause (opsional)
    [SerializeField] private AudioSource bgmAudioSource; // Tarik objek musik ke sini
    private bool isPaused = false;
    
    private bool isWaitingForConfirmation = false;
    private Coroutine currentResetCoroutine;
    private Vector2 textTargetPosition; // Posisi muncul (di bawah tombol)
    private Vector2 textHiddenPosition; // Posisi sembunyi (di belakang tombol)

    [Header("Connections & References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private FadeManager fadeManager;
    [SerializeField] private WinManager winManager;

    void Start()
    {
        // 1. Catat posisi awal panel saat tersembunyi tepat di belakang tombol reset
        textHiddenPosition = confirmationTextPanel.anchoredPosition;
        
        // 2. Karena meluncur ke BAWAH, nilai Y dikurangi (negatif).
        // Hubungkan tinggi panel pesanmu di sini (misal jika tinggi panel pesan 80 pixel, masukkan -80f)
        float panelHeight = confirmationTextPanel.rect.height;
        textTargetPosition = textHiddenPosition + new Vector2(0f, -(panelHeight + 10));
        if (pauseOverlayPanel != null) pauseOverlayPanel.SetActive(false);
        
        StartCoroutine(fadeManager.FadeInRoutine());
    }

    void Update()
    {
        // 1. Jalankan Timer jika game tidak dijeda
        if (isTimerRunning && !isPaused)
        {
            elapsedTime += Time.deltaTime;
            UpdateTimerDisplay();
        }

        // 2. LOGIKA UNPAUSE: DETEKSI KLIK SEMBARANG SAAT PAUSE
        if (isPaused)
        {
            Mouse currentMouse = Mouse.current;
            if (currentMouse != null && currentMouse.leftButton.wasPressedThisFrame)
            {
                // Gunakan Coroutine singkat agar klik unpause tidak tidak sengaja memicu drag kepingan puzzle di frame yang sama
                StartCoroutine(UnpauseGameRoutine());
            }
        }
    }

    void UpdateTimerDisplay()
    {
        int minutes = Mathf.FloorToInt(elapsedTime / 60f);
        int seconds = Mathf.FloorToInt(elapsedTime % 60f);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    // --- FIX PENYALURAN DATA WIN SYSTEM ---
    public void StopTimerOnWin()
    {
        isTimerRunning = false; // Matikan detak jam

        // JALUR PEMICU UTAMA: Hubungi WinManager untuk mencatat rekor dan memunculkan panel!
        if (winManager != null)
        {
            winManager.HandleGameplayWinSystem(elapsedTime);
        }
        else
        {
            Debug.LogError("[Missing Reference] WinManager belum dipasang di Inspector GameUIManager!");
        }
    }

    public void OnPauseButtonClicked()
    {
        if (isPaused) return; // Jika sudah pause, fungsi unpause diatur oleh klik sembarang di Update

        isPaused = true;

        // A. Hentikan Waktu Fisika Game (Menghentikan pergerakan Chimmy memantul & drag puzzle)
        Time.timeScale = 0f;

        // B. Hentikan Musik
        if (bgmAudioSource != null && bgmAudioSource.isPlaying)
        {
            bgmAudioSource.Pause();
        }

        // C. Munculkan Layar Jeda (Jika ada)
        if (pauseOverlayPanel != null) pauseOverlayPanel.SetActive(true);

        Debug.Log("Game Dijeda. Timer, Objek, dan Musik BERHENTI. Klik di mana saja untuk lanjut.");
    }

    IEnumerator UnpauseGameRoutine()
    {
        // Tunggu hingga akhir frame agar klik tidak bocor ke sistem drag kepingan
        yield return new WaitForEndOfFrame();

        isPaused = false;

        // A. Kembalikan Waktu Fisika Game ke Normal
        Time.timeScale = 1f;

        // B. Jalankan Musik Kembali
        if (bgmAudioSource != null)
        {
            bgmAudioSource.UnPause();
        }

        // C. Sembunyikan Layar Jeda
        if (pauseOverlayPanel != null) pauseOverlayPanel.SetActive(false);

        Debug.Log("Game Dilanjutkan!");
    }

    // --- MEKANISME KEMBALI KE MAIN MENU ---
    public void OnQuitToMainMenuClicked()
    {
        StartCoroutine(QuitRoutine());
    }

    IEnumerator QuitRoutine()
    {
        // 1. Pastikan waktu berjalan normal agar pemrosesan perintah Unity tidak freeze
        Time.timeScale = 1f; 

        // 2. Jalankan efek menggelap (Fade Out) dan tunggu hingga durasi 0.8 detiknya beres
        if (fadeManager != null)
        {
            yield return StartCoroutine(fadeManager.FadeOutRoutine());
        }
        else
        {
            // Antisipasi jika lupa pasang FadeManager di Inspector agar game tidak softlock
            yield return new WaitForSeconds(0.8f); 
        }

        Debug.Log("Layar sudah hitam total, pindah ke Main Menu.");
        SceneManager.LoadScene("Euyskuyyy Menu"); 
    }

    // --- LOGIKA UTAMA TOMBOL RESET ---
    public void OnResetButtonClicked()
    {
        if (!isWaitingForConfirmation)
        {
            // Klik Pertama: Meluncur ke bawah dari belakang tombol
            if (currentResetCoroutine != null) StopCoroutine(currentResetCoroutine);
            currentResetCoroutine = StartCoroutine(ShowConfirmationRoutine());
        }
        else
        {
            // Klik Kedua: Meluncur kembali ke atas dulu baru reset game
            if (currentResetCoroutine != null) StopCoroutine(currentResetCoroutine);
            currentResetCoroutine = StartCoroutine(ExecuteResetWithAnimationRoutine());
        }
    }

    // Coroutine untuk klik pertama (Muncul lalu sembunyi otomatis jika didiamkan 2 detik)
    IEnumerator ShowConfirmationRoutine()
    {
        isWaitingForConfirmation = true;

        // Animasikan meluncur ke bawah
        while (Vector2.Distance(confirmationTextPanel.anchoredPosition, textTargetPosition) > 0.5f)
        {
            confirmationTextPanel.anchoredPosition = Vector2.MoveTowards(
                confirmationTextPanel.anchoredPosition, 
                textTargetPosition, 
                slideSpeed * Time.deltaTime
            );
            yield return null;
        }

        // Tunggu 2 detik
        yield return new WaitForSeconds(2f);

        // Jika tidak ada klik kedua, meluncur kembali ke atas (Sembunyi)
        isWaitingForConfirmation = false;
        yield return StartCoroutine(SlideBackUpRoutine());
    }

    // Coroutine untuk klik kedua (Meluncur ke atas dengan cepat, lalu acak puzzle)
    IEnumerator ExecuteResetWithAnimationRoutine()
    {
        isWaitingForConfirmation = false;

        // Jalankan animasi meluncur ke atas terlebih dahulu hingga selesai
        yield return StartCoroutine(SlideBackUpRoutine());

        // Setelah benar-benar sembunyi di belakang tombol, eksekusi reset datanya
        elapsedTime = 0f;
        isTimerRunning = true;
        gameManager.GeneratePuzzleGrid();
        Debug.Log("Game Berhasil Di-reset setelah animasi tutup selesai!");
    }

    // Sub-Coroutine khusus untuk menggerakkan panel kembali ke belakang tombol (Sumbu Y awal)
    IEnumerator SlideBackUpRoutine()
    {
        while (Vector2.Distance(confirmationTextPanel.anchoredPosition, textHiddenPosition) > 0.5f)
        {
            confirmationTextPanel.anchoredPosition = Vector2.MoveTowards(
                confirmationTextPanel.anchoredPosition, 
                textHiddenPosition, 
                slideSpeed * Time.deltaTime
            );
            yield return null;
        }
    }
}