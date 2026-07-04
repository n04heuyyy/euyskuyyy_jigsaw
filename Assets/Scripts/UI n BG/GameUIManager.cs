using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; 
using TMPro;

public class GameUIManager : MonoBehaviour
{
    [Header("Timer UI")]
    [SerializeField] private TextMeshProUGUI timerText;
    private float elapsedTime = 0f;
    private bool isTimerRunning = true;

    [Header("Reset Button UI")]
    [SerializeField] private Button resetButton;
    [SerializeField] private RectTransform confirmationTextPanel;
    [SerializeField] private float slideSpeed = 600f;

    [Header("Pause Configurations")]
    [SerializeField] private GameObject pauseOverlayPanel;
    [SerializeField] private AudioSource bgmAudioSource;
    private bool isPaused = false;
    
    private bool isWaitingForConfirmation = false;
    private Coroutine currentResetCoroutine;
    private Vector2 textTargetPosition; // Posisi muncul reset confirm
    private Vector2 textHiddenPosition; // Posisi sembunyinya

    [Header("Connections & References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private FadeManager fadeManager;
    [SerializeField] private WinManager winManager;

    void Start()
    {
        // Catat posisi awal panel saat tersembunyi tepat di belakang tombol reset
        textHiddenPosition = confirmationTextPanel.anchoredPosition;
        
        // Karena meluncur ke bawah, nilai Y dikurangi (negatif)
        float panelHeight = confirmationTextPanel.rect.height;
        textTargetPosition = textHiddenPosition + new Vector2(0f, -(panelHeight + 10));
        if (pauseOverlayPanel != null) pauseOverlayPanel.SetActive(false);
        
        StartCoroutine(fadeManager.FadeInRoutine());
    }

    void Update()
    {
        if (isTimerRunning && !isPaused) // Jalankan timer jika game tidak dijeda
        {
            elapsedTime += Time.deltaTime;
            UpdateTimerDisplay();
        }

        if (isPaused) // Deteksi klik sembarang saat pause
        {
            Mouse currentMouse = Mouse.current;
            if (currentMouse != null && currentMouse.leftButton.wasPressedThisFrame)
            {
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

    public void StopTimerOnWin()
    {
        isTimerRunning = false; // Matikan timer

        // Hubungi WinManager untuk mencatat rekor dan memunculkan panel win
        if (winManager != null)
        {
            winManager.HandleGameplayWinSystem(elapsedTime);
        }
    }

    public void OnPauseButtonClicked()
    {
        if (isPaused) return; // Jika sudah pause, fungsi unpause diatur oleh klik sembarang di Update
        isPaused = true;
        // Hentikan time physics (Menghentikan pergerakan objek & drag puzzle)
        Time.timeScale = 0f;

        if (bgmAudioSource != null && bgmAudioSource.isPlaying)
        {
            bgmAudioSource.Pause(); // Hentikan musik
        }

        // Munculkan layar jeda
        if (pauseOverlayPanel != null) pauseOverlayPanel.SetActive(true);
    }

    IEnumerator UnpauseGameRoutine()
    {
        // Tunggu hingga akhir frame agar klik tidak bocor ke sistem drag kepingan
        yield return new WaitForEndOfFrame();
        isPaused = false;
        Time.timeScale = 1f; // Jalankan lagi time physics

        if (bgmAudioSource != null) 
        {
            bgmAudioSource.UnPause(); // Jalankan musik kembali
        }

        // Sembunyikan layar jeda
        if (pauseOverlayPanel != null) pauseOverlayPanel.SetActive(false);
    }

    public void OnQuitToMainMenuClicked()
    {
        StartCoroutine(QuitRoutine());
    }

    IEnumerator QuitRoutine()
    {
        // Pastikan waktu berjalan normal agar pemrosesan perintah Unity tidak freeze
        Time.timeScale = 1f; 

        // Jalankan efek fade out dan tunggu hingga durasimya selesai
        if (fadeManager != null)
        {
            yield return StartCoroutine(fadeManager.FadeOutRoutine());
        }
        else
        {
            yield return new WaitForSeconds(0.8f); 
        }
        SceneManager.LoadScene("Euyskuyyy Menu"); 
    }

    public void OnResetButtonClicked()
    {
        if (!isWaitingForConfirmation)
        {
            // Klik pertama meluncur ke bawah dari belakang tombol
            if (currentResetCoroutine != null) StopCoroutine(currentResetCoroutine);
            currentResetCoroutine = StartCoroutine(ShowConfirmationRoutine());
        }
        else
        {
            // Klik kedua meluncur kembali ke atas dulu baru reset game
            if (currentResetCoroutine != null) StopCoroutine(currentResetCoroutine);
            currentResetCoroutine = StartCoroutine(ExecuteReset());
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

        // Jika tidak ada klik kedua, meluncur kembali ke atas
        isWaitingForConfirmation = false;
        yield return StartCoroutine(SlideBackUpRoutine());
    }

    // Klik kedua reset akan meluncur ke atas dengan cepat, lalu acak puzzle
    IEnumerator ExecuteReset()
    {
        isWaitingForConfirmation = false;

        // Jalankan animasi meluncur ke atas terlebih dahulu hingga selesai
        yield return StartCoroutine(SlideBackUpRoutine());

        // Setelah benar-benar sembunyi di belakang tombol, reset data
        elapsedTime = 0f;
        isTimerRunning = true;
        gameManager.GeneratePuzzleGrid();
    }

    // Sub-Coroutine khusus untuk menggerakkan panel kembali ke belakang tombol
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