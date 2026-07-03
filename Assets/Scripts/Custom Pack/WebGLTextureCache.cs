using System.Collections.Generic;
using UnityEngine;

public class WebGLTextureCache : MonoBehaviour
{
    public static WebGLTextureCache Instance { get; private set; }

    public Dictionary<int, Sprite> cachedCustomSprites = new Dictionary<int, Sprite>();

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

    public void ClearCache()
    {
        foreach (var sprite in cachedCustomSprites.Values)
        {
            if (sprite != null)
            {
                if (sprite.texture != null) Destroy(sprite.texture);
                Destroy(sprite);
            }
        }
        cachedCustomSprites.Clear();
    }
}