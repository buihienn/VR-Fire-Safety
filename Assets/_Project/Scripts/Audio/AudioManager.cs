using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    [System.Serializable]
    public class Sound
    {
        public string name;
        public AudioClip clip;

        [Range(0f, 1f)]
        public float volume = 1f;

        [Range(-3f, 3f)]
        public float pitch = 2f;

        public bool loop = false;
        public bool playOnAwake = false;

        public AudioSource source;

        [Header("Optional")]
        public AudioMixerGroup output;

    }

    public static AudioManager Instance { get; private set; }

    [SerializeField] private Sound[] sounds;
    [SerializeField] private bool dontDestroyOnLoad = true;

    private readonly Dictionary<string, Sound> soundLookup = new Dictionary<string, Sound>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[AudioManager] Duplicate AudioManager found. Destroying this one.", gameObject);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        InitializeSounds();
    }

    private void InitializeSounds()
    {
        soundLookup.Clear();

        foreach (Sound s in sounds)
        {
            if (s == null)
                continue;

            if (string.IsNullOrWhiteSpace(s.name))
            {
                Debug.LogWarning("[AudioManager] Found a sound with empty name.", gameObject);
                continue;
            }

            if (s.clip == null)
            {
                Debug.LogWarning($"[AudioManager] Sound '{s.name}' has no AudioClip assigned.", gameObject);
                continue;
            }

            if (soundLookup.ContainsKey(s.name))
            {
                Debug.LogWarning($"[AudioManager] Duplicate sound name '{s.name}'. Only the first one will be used.", gameObject);
                continue;
            }

            // Nếu chưa có source được kéo sẵn thì tự tạo trên AudioManager
            if (s.source == null)
            {
                s.source = gameObject.AddComponent<AudioSource>();
            }

            ApplySoundSettings(s);

            soundLookup.Add(s.name, s);
        }

        // playOnAwake sau khi init xong toàn bộ
        foreach (Sound s in soundLookup.Values)
        {
            if (s.playOnAwake)
            {
                s.source.Play();
            }
        }
    }

    private void ApplySoundSettings(Sound s)
    {
        if (s.source == null)
            return;

        s.source.clip = s.clip;
        s.source.volume = s.volume;
        s.source.pitch = s.pitch;
        s.source.loop = s.loop;
        s.source.playOnAwake = false;

        if (s.output != null)
            s.source.outputAudioMixerGroup = s.output;
    }

    public void Play(string name)
    {
        if (!TryGetSound(name, out Sound s))
            return;

        s.source.Play();
    }

    public void Stop(string name)
    {
        if (!TryGetSound(name, out Sound s))
            return;

        s.source.Stop();
    }

    public void Pause(string name)
    {
        if (!TryGetSound(name, out Sound s))
            return;

        s.source.Pause();
    }

    public void UnPause(string name)
    {
        if (!TryGetSound(name, out Sound s))
            return;

        s.source.UnPause();
    }

    public void PlayOneShot(string name)
    {
        if (!TryGetSound(name, out Sound s))
            return;

        s.source.PlayOneShot(s.clip, s.volume);
        Debug.Log($"[AudioManager] Playing one-shot sound '{name}' at volume {s.volume}.");
    }

    public bool IsPlaying(string name)
    {
        if (!TryGetSound(name, out Sound s))
            return false;

        return s.source.isPlaying;
    }

    public void SetVolume(string name, float volume)
    {
        if (!TryGetSound(name, out Sound s))
            return;

        s.volume = Mathf.Clamp01(volume);
        s.source.volume = s.volume;
    }

    public void SetPitch(string name, float pitch)
    {
        if (!TryGetSound(name, out Sound s))
            return;

        s.pitch = Mathf.Clamp(pitch, -3f, 3f);
        s.source.pitch = s.pitch;
    }

    public AudioSource GetSource(string name)
    {
        if (!TryGetSound(name, out Sound s))
            return null;

        return s.source;
    }

    public void RefreshSoundSettings(string name)
    {
        if (!TryGetSound(name, out Sound s))
            return;

        ApplySoundSettings(s);
    }

    private bool TryGetSound(string name, out Sound sound)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Debug.LogWarning("[AudioManager] Sound name is null or empty.");
            sound = null;
            return false;
        }

        if (!soundLookup.TryGetValue(name, out sound))
        {
            Debug.LogWarning($"[AudioManager] Sound '{name}' not found.");
            return false;
        }

        if (sound.source == null)
        {
            Debug.LogWarning($"[AudioManager] Sound '{name}' has no AudioSource reference.");
            return false;
        }

        return true;
    }
}