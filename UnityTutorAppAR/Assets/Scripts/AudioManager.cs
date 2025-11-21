using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

/// <summary>
/// Simple AudioManager singleton for playing audio clips from files or URLs
/// </summary>
public class AudioManager : MonoBehaviour
{
    private static AudioManager instance;
    public static AudioManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("AudioManager");
                instance = go.AddComponent<AudioManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource audioSource;
    
    [Header("Character Reference")]
    [SerializeField] private PlayerController playerController;
    
    [Header("Settings")]
    [SerializeField] private float volume = 1f;
    [SerializeField] private bool autoSetIdleOnPlay = true;
    [SerializeField] private bool streamAudio = true; // Stream vs download completely

    private void Awake()
    {
        // Singleton pattern
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Create AudioSource if not assigned
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
        
        audioSource.volume = volume;
    }

    /// <summary>
    /// Play an audio clip and wait until it finishes
    /// </summary>
    /// <param name="audioClip">The audio clip to play</param>
    /// <param name="onComplete">Optional callback when audio finishes</param>
    public void PlayAudio(AudioClip audioClip, System.Action onComplete = null)
    {
        if (audioClip == null)
        {
            Debug.LogWarning("AudioManager: Cannot play null audio clip!");
            onComplete?.Invoke();
            return;
        }
        
        StartCoroutine(PlayAudioCoroutine(audioClip, onComplete));
    }

    /// <summary>
    /// Load and play audio from a URL
    /// </summary>
    /// <param name="url">URL to MP3/WAV/OGG file</param>
    /// <param name="onComplete">Optional callback when audio finishes</param>
    public void PlayAudioFromURL(string url, System.Action onComplete = null)
    {
        StartCoroutine(LoadAndPlayAudioFromURL(url, onComplete));
    }

    /// <summary>
    /// Load audio from URL and return the AudioClip (for caching/reuse)
    /// </summary>
    /// <param name="url">URL to MP3/WAV/OGG file</param>
    /// <param name="onLoaded">Callback with loaded AudioClip</param>
    public void LoadAudioFromURL(string url, System.Action<AudioClip> onLoaded)
    {
        StartCoroutine(LoadAudioClipFromURL(url, onLoaded));
    }

    private IEnumerator LoadAndPlayAudioFromURL(string url, System.Action onComplete)
    {
        Debug.Log($"Loading audio from URL: {url}");
        
        // Determine audio type from URL
        AudioType audioType = GetAudioTypeFromURL(url);
        
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, audioType))
        {
            // Set streaming mode
            DownloadHandlerAudioClip handler = (DownloadHandlerAudioClip)www.downloadHandler;
            handler.streamAudio = streamAudio;
            
            yield return www.SendWebRequest();
            
            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                
                if (clip != null)
                {
                    Debug.Log($"Successfully loaded audio from URL (Length: {clip.length}s)");
                    
                    // Play the loaded clip
                    yield return PlayAudioCoroutine(clip, onComplete);
                }
                else
                {
                    Debug.LogError("Failed to extract AudioClip from download");
                    onComplete?.Invoke();
                }
            }
            else
            {
                Debug.LogError($"Failed to load audio from URL: {www.error}");
                onComplete?.Invoke();
            }
        }
    }

    private IEnumerator LoadAudioClipFromURL(string url, System.Action<AudioClip> onLoaded)
    {
        Debug.Log($"Loading audio from URL: {url}");
        
        AudioType audioType = GetAudioTypeFromURL(url);
        
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, audioType))
        {
            DownloadHandlerAudioClip handler = (DownloadHandlerAudioClip)www.downloadHandler;
            handler.streamAudio = streamAudio;
            
            yield return www.SendWebRequest();
            
            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                Debug.Log($"Successfully loaded audio from URL (Length: {clip.length}s)");
                onLoaded?.Invoke(clip);
            }
            else
            {
                Debug.LogError($"Failed to load audio from URL: {www.error}");
                onLoaded?.Invoke(null);
            }
        }
    }

    private AudioType GetAudioTypeFromURL(string url)
    {
        string lowerUrl = url.ToLower();
        
        if (lowerUrl.EndsWith(".mp3"))
            return AudioType.MPEG;
        else if (lowerUrl.EndsWith(".wav"))
            return AudioType.WAV;
        else if (lowerUrl.EndsWith(".ogg"))
            return AudioType.OGGVORBIS;
        else if (lowerUrl.EndsWith(".aiff") || lowerUrl.EndsWith(".aif"))
            return AudioType.AIFF;
        else
        {
            Debug.LogWarning($"Unknown audio format, defaulting to MPEG. URL: {url}");
            return AudioType.MPEG;
        }
    }

    private IEnumerator PlayAudioCoroutine(AudioClip audioClip, System.Action onComplete)
    {
        // Set character to idle state before playing audio
        if (autoSetIdleOnPlay && playerController != null)
        {
            playerController.ChangeState(CharacterState.Idle);
            Debug.Log("Character set to Idle state for audio playback");
        }
        
        // Stop any currently playing audio
        if (audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        
        // Play the audio clip
        audioSource.clip = audioClip;
        audioSource.Play();
        
        Debug.Log($"Playing audio: {audioClip.name} (Duration: {audioClip.length}s)");
        
        // Wait for the audio to finish
        yield return new WaitForSeconds(audioClip.length);
        
        Debug.Log($"Finished playing audio: {audioClip.name}");
        
        // Invoke callback when done
        onComplete?.Invoke();
    }

    /// <summary>
    /// Play audio and return a coroutine you can yield on
    /// </summary>
    public IEnumerator PlayAudioAsync(AudioClip audioClip)
    {
        if (audioClip == null)
        {
            Debug.LogWarning("AudioManager: Cannot play null audio clip!");
            yield break;
        }
        
        // Set character to idle state before playing audio
        if (autoSetIdleOnPlay && playerController != null)
        {
            playerController.ChangeState(CharacterState.Idle);
            Debug.Log("Character set to Idle state for audio playback");
        }
        
        // Stop any currently playing audio
        if (audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        
        // Play the audio clip
        audioSource.clip = audioClip;
        audioSource.Play();
        
        Debug.Log($"Playing audio: {audioClip.name} (Duration: {audioClip.length}s)");
        
        // Wait for the audio to finish
        yield return new WaitForSeconds(audioClip.length);
        
        Debug.Log($"Finished playing audio: {audioClip.name}");
    }

    /// <summary>
    /// Stop currently playing audio
    /// </summary>
    public void StopAudio()
    {
        if (audioSource.isPlaying)
        {
            audioSource.Stop();
            Debug.Log("Stopped audio playback");
        }
    }

    /// <summary>
    /// Check if audio is currently playing
    /// </summary>
    public bool IsPlaying()
    {
        return audioSource.isPlaying;
    }

    /// <summary>
    /// Set the volume
    /// </summary>
    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);
        audioSource.volume = volume;
    }

    /// <summary>
    /// Get current volume
    /// </summary>
    public float GetVolume()
    {
        return volume;
    }
    
    /// <summary>
    /// Set the player controller reference
    /// </summary>
    public void SetPlayerController(PlayerController controller)
    {
        playerController = controller;
        Debug.Log("PlayerController reference set in AudioManager");
    }
    
    /// <summary>
    /// Enable or disable auto-idle behavior
    /// </summary>
    public void SetAutoIdleOnPlay(bool enabled)
    {
        autoSetIdleOnPlay = enabled;
    }

    /// <summary>
    /// Enable or disable audio streaming (vs full download)
    /// </summary>
    public void SetStreamAudio(bool enabled)
    {
        streamAudio = enabled;
    }
}