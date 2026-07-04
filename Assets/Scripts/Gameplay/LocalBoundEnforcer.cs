using UnityEngine;

public class LocalBoundEnforcer : MonoBehaviour
{
    private float limitX;
    private float limitY;
    private Rigidbody2D rb2d;
    
    // Tambahkan variabel untuk mengunci kecepatan konstan objek
    private float constantSpeed; 
    // Simpan target kecepatan sudut fisik (bukan rotasi transform)
    private float targetAngularVelocity = 0f;
    public float maxRotationSpeed = 250f;
    [HideInInspector] public bool useCustomPanelRotation = false;

    public void SetupBounds(float rangeX, float rangeY)
    {
        limitX = rangeX * 0.65f;
        limitY = rangeY * 0.65f;
        rb2d = GetComponent<Rigidbody2D>();
        
        // Catat kecepatan awal objek 
        if (rb2d != null)
        {
            // Pastikan gesekan rotasi mati total agar awet berputar
            rb2d.angularDamping = 0f;

            // Ambil data kecepatan dari prefab
            constantSpeed = rb2d.linearVelocity.magnitude;
            if (constantSpeed < 0.1f) constantSpeed = 5f; 

            // Begitu game di-start/reset, objek tidak mulai dari tengah (0,0), 
            // melainkan langsung dilempar ke titik acak di dalam kandang kamera
            float randomStartX = Random.Range(-limitX * 0.7f, limitX * 0.7f);
            float randomStartY = Random.Range(-limitY * 0.7f, limitY * 0.7f);
            transform.localPosition = new Vector3(randomStartX, randomStartY, transform.localPosition.z);

            // Buat arah tembakan awal benar-benar acak ke segala sudut (360 derajat)
            float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector2 randomDirection = new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle));
            
            // Terapkan kecepatan acak baru
            rb2d.linearVelocity = randomDirection * constantSpeed;
        }
    }

    void Update()
    {
        Vector3 localPos = transform.localPosition;
        bool hitWall = false;
        Vector2 currentVelocity = rb2d != null ? rb2d.linearVelocity : Vector2.zero;

        // Batas Kanan / Kiri
        if (localPos.x > limitX && currentVelocity.x > 0) { currentVelocity.x = -currentVelocity.x; hitWall = true; }
        else if (localPos.x < -limitX && currentVelocity.x < 0) { currentVelocity.x = -currentVelocity.x; hitWall = true; }

        // Batas Atas / Bawah
        if (localPos.y > limitY && currentVelocity.y > 0) { currentVelocity.y = -currentVelocity.y; hitWall = true; }
        else if (localPos.y < -limitY && currentVelocity.y < 0) { currentVelocity.y = -currentVelocity.y; hitWall = true; }

        if (hitWall && rb2d != null)
        {
            currentVelocity.x += Random.Range(-0.2f, 0.2f);
            currentVelocity.y += Random.Range(-0.2f, 0.2f);
            rb2d.linearVelocity = currentVelocity.normalized * constantSpeed;

            if (useCustomPanelRotation)
            {
                rb2d.angularVelocity = maxRotationSpeed;
            }
            else
            {
                rb2d.angularVelocity = targetAngularVelocity;
            }
        }

        // Kunci posisi agar tidak tembus batas luar kamera target
        float clampedX = Mathf.Clamp(localPos.x, -limitX, limitX);
        float clampedY = Mathf.Clamp(localPos.y, -limitY, limitY);
        transform.localPosition = new Vector3(clampedX, clampedY, localPos.z);
    }

    // Dipanggil secara konstan oleh PhysX Engine Unity untuk mengunci gaya rotasi angular 
    // agar momentum putaran tidak diredam menjadi 0 setelah benturan atau gesekan geser
    void FixedUpdate()
    {
        if (rb2d != null)
        {
            if (useCustomPanelRotation)
            {
                // Kunci putaran kustom dari slider panel kustom secara mutlak tiap frame fisika
                rb2d.angularVelocity = maxRotationSpeed;
            }
        }
    }

    // Fungsi ini akan dipanggil oleh GameManager untuk apply physics
    public void SetRotationFromSlider(float sliderValue, float maxRotationSpeed)
    {
        if (rb2d == null) return;
        rb2d.WakeUp();

        // Reset total momentum lama agar tidak mengunci koordinat sumbu Z
        rb2d.angularVelocity = 0f;

        // Hitung deviasi baru dari nilai tengah slider (0.5f)
        float deviation = sliderValue - 0.5f;

        // Beri toleransi deadzone tipis di tengah agar di posisi 0.5 benar-benar diam total
        if (Mathf.Abs(deviation) < 0.02f)
        {
            targetAngularVelocity = 0f;
            rb2d.angularVelocity = 0f;
            return;
        }

        // Terapkan kecepatan sudut baru ke target variabel
        targetAngularVelocity = deviation * -maxRotationSpeed; 
        
        // Langsung suntikkan ke Rigidbody di frame yang sama
        rb2d.angularVelocity = targetAngularVelocity;
    }

    public void UpdateMovementSpeed(float newSpeed)
    {
        // Perbarui catatan kecepatan konstan agar pantulan ke depan memakai speed baru ini
        constantSpeed = newSpeed;

        if (rb2d != null)
        {
            // Ambil arah gerak saat ini (Normalized), lalu kalikan dengan kecepatan baru
            Vector2 currentDirection = rb2d.linearVelocity.normalized;
            
            // Jika objek entah bagaimana sedang diam, beri arah acak agar tidak stuck
            if (currentDirection.magnitude < 0.1f)
            {
                float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                currentDirection = new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle));
            }

            // Terapkan kecepatan baru secara instan ke Rigidbody
            rb2d.linearVelocity = currentDirection * constantSpeed;
        }
    }

    public void SetCustomObjectScale(float targetScale)
    {
        // Mengubah ukuran visual objek di dalam game mengikuti hasil slider kustom (0.75f - 1.5f)
        transform.localScale = new Vector3(targetScale, targetScale, 1f);
    }
    public void ApplyCustomPanelRotation()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            useCustomPanelRotation = true; // Kunci agar skrip tahu ini rotasi kustom
            rb.WakeUp();
            
            // Berikan kecepatan putar konstan berdasarkan slider kustom.
            // Kita gunakan nilai maxRotationSpeed yang sudah di-Lerp dari ObjectPreviewManager.
            rb.angularVelocity = maxRotationSpeed; 
        }
    }
}