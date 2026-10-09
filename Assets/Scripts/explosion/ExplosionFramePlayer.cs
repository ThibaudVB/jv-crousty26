using System.Collections;
using UnityEngine;

public class ExplosionFramePlayer : MonoBehaviour
{
    [Header("Lecture")]
    [SerializeField] private float fps = 24f;
    [SerializeField] private bool playOnAwake = true;
    [SerializeField] private bool loop = false;
    [SerializeField] private bool destroyAtEnd = true;

    private Transform[] frames;
    private Coroutine playRoutine;

    private void Awake()
    {
        // Chaque enfant direct = une frame
        frames = new Transform[transform.childCount];
        for (int i = 0; i < frames.Length; i++)
        {
            frames[i] = transform.GetChild(i);
            frames[i].gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (playOnAwake) Play();
    }

    public void Play()
    {
        if (playRoutine != null) StopCoroutine(playRoutine);
        playRoutine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        float delay = 1f / fps;
        int current = -1;

        do
        {
            for (int i = 0; i < frames.Length; i++)
            {
                if (current >= 0) frames[current].gameObject.SetActive(false);
                frames[i].gameObject.SetActive(true);
                current = i;
                yield return new WaitForSeconds(delay);
            }
        } while (loop);

        if (current >= 0) frames[current].gameObject.SetActive(false);

        if (destroyAtEnd) Destroy(gameObject);
        else gameObject.SetActive(false);
    }
}