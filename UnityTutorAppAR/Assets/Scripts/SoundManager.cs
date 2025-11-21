using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }
    
    private AudioSource audioSource; // Will be created automatically
    
    [Header("UI Sound Clips")]
    [SerializeField] private AudioClip tap;
    [SerializeField] private AudioClip toggleSound;
    [SerializeField] private AudioClip successSound;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        // Create AudioSource automatically
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D sound
    }
    
    public void PlayTap()
    {
        if (tap != null)
            audioSource.PlayOneShot(tap);
    }
    
    public void PlayToggle()
    {
        if (toggleSound != null)
            audioSource.PlayOneShot(toggleSound);
    }
    
    public void PlaySuccess()
    {
        if (successSound != null)
            audioSource.PlayOneShot(successSound);
    }
}