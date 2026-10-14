using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Vies du bus affichées en cœurs dans le HUD (sous l'argent et les commandes).
// Elles suivent BusDamage : chaque choc enlève de la vie, le garage remet tous les cœurs.
//   Hits To Destroy = 10 et 5 cœurs -> un cœur se vide tous les 2 chocs.
//
// À placer sur un objet UI (RectTransform) du HUD : les cœurs sont créés dedans, en ligne et centrés.
public class BusHealthHearts : MonoBehaviour
{
    [Header("Bus")]
    [Tooltip("Dégâts du bus. Si vide : cherché dans la scène.")]
    public BusDamage busDamage;

    [Header("Cœurs")]
    [Min(1)] public int heartCount = 5;
    public Sprite fullHeart;
    public Sprite emptyHeart;
    [Tooltip("Taille d'un cœur (pixels, écran de référence 1920 x 1080)")]
    public Vector2 heartSize = new Vector2(51f, 42f);
    [Tooltip("Écart entre deux cœurs (pixels)")]
    public float spacing = 4.5f;

    [Header("Animation")]
    [Tooltip("Un cœur qui se vide grossit un instant (1 = aucun effet)")]
    [Min(1f)] public float lostPopScale = 1.35f;
    [Min(0.01f)] public float lostPopDuration = 0.3f;

    private readonly List<Image> hearts = new List<Image>();
    private readonly List<float> popTimes = new List<float>();
    private int shownLives = -1;

    private void Start()
    {
        if (busDamage == null)
            busDamage = FindAnyObjectByType<BusDamage>();
        BuildHearts();
    }

    private void BuildHearts()
    {
        float totalWidth = heartCount * heartSize.x + (heartCount - 1) * spacing;
        for (int i = 0; i < heartCount; i++)
        {
            var heart = new GameObject("Coeur " + (i + 1), typeof(RectTransform), typeof(Image));
            heart.transform.SetParent(transform, false);
            var rect = (RectTransform)heart.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = heartSize;
            rect.anchoredPosition = new Vector2(-totalWidth * 0.5f + heartSize.x * 0.5f + i * (heartSize.x + spacing), 0f);
            var image = heart.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            hearts.Add(image);
            popTimes.Add(-999f);
        }
    }

    // Nombre de cœurs encore pleins
    private int LivesLeft()
    {
        if (busDamage == null) return heartCount;
        int maxHits = Mathf.Max(1, busDamage.hitsToDestroy);
        int hitsLeft = Mathf.Clamp(maxHits - busDamage.HitsTaken, 0, maxHits);
        return Mathf.CeilToInt(hitsLeft * heartCount / (float)maxHits);
    }

    private void Update()
    {
        int lives = LivesLeft();
        if (lives != shownLives)
        {
            // Les cœurs qui viennent de se vider font un petit « pop »
            if (shownLives >= 0)
                for (int i = lives; i < shownLives && i < hearts.Count; i++)
                    popTimes[i] = Time.unscaledTime;
            shownLives = lives;
            for (int i = 0; i < hearts.Count; i++)
                hearts[i].sprite = i < lives ? fullHeart : emptyHeart;
        }

        for (int i = 0; i < hearts.Count; i++)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - popTimes[i]) / lostPopDuration);
            hearts[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(lostPopScale, 1f, t);
        }
    }
}
