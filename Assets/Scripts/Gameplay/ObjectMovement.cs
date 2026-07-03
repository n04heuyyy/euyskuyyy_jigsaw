using UnityEngine;

public class ObjectMovement : MonoBehaviour
{
    private Rigidbody2D rb;
    public float speed = 5f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        
        // Berikan kecepatan awal ke arah diagonal agar memantul
        rb.linearVelocity = new Vector2(1f, 1f).normalized * speed; 
    }
}