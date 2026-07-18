using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class MidiNote
{
    public int note;
    public int velocity;
    public float time;
    public float duration;
    public float end;
    [System.NonSerialized] public int clipIndex;
}

[System.Serializable]
public class MidiSong
{
    public MidiNote[] notes;
}

public class MidiJsonPlayer : MonoBehaviour
{
    [Header("Background Audio")]
    [SerializeField] private AudioSource backgroundAudioSource;

    [Header("Volume")]
    [Range(0f, 1f)]
    public float backgroundVolume = 0.85f;
    [Range(0f, 1f)]
    public float noteVolume = 0.9f;

    [Header("Note Release")]
    [Range(0.1f, 2f)]
    public float releaseTime = 0.5f;

    [Header("Pool")]
    public int maxVoices = 36;

    private List<AudioClip> noteClips = new List<AudioClip>();
    private List<AudioSource> audioSources = new List<AudioSource>();
    public MidiSong song;

    private int current = 0;
    private bool isPlaying = false;
    [Header("Note Combining")]
[Range(0f, 2f)]
public float combineThreshold = 0.15f;   // Seconds - notes closer than this will be combined
    void Awake()
    {
        if (backgroundAudioSource == null)
            backgroundAudioSource = gameObject.AddComponent<AudioSource>();

        backgroundAudioSource.playOnAwake = false;
        backgroundAudioSource.loop = false;

        for (int i = 0; i < maxVoices; i++)
        {
            AudioSource src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            audioSources.Add(src);
        }

        LoadAudioAndJSON();
    }

   private void LoadAudioAndJSON()
{
    TextAsset jsonAsset = Resources.Load<TextAsset>("song");
    if (jsonAsset == null)
    {
        Debug.LogError("song.json not found!");
        return;
    }

    song = JsonUtility.FromJson<MidiSong>(jsonAsset.text);

    if (song?.notes == null || song.notes.Length == 0)
    {
        Debug.LogError("No notes in JSON!");
        return;
    }

    for (int ii = 0; ii < song.notes.Length; ii++)
    {
        MidiNote n = song.notes[ii];
        n.time = n.time / 1000f;
        n.duration = n.duration / 1000f;
        n.end = n.end / 1000f;
        n.clipIndex = ii;
    }

    System.Array.Sort(song.notes, (a, b) => a.time.CompareTo(b.time));

    // Slice audio
    noteClips.Clear();
    AudioClip fullClip = backgroundAudioSource.clip ?? Resources.Load<AudioClip>("song");

    if (fullClip == null)
    {
        Debug.LogError("song.wav not found!");
        return;
    }

    float[] fullSamples = new float[fullClip.samples * fullClip.channels];
    fullClip.GetData(fullSamples, 0);

    int sampleRate = fullClip.frequency;
    int channels = fullClip.channels;

    int i = 0;
    while (i < song.notes.Length)
    {
        MidiNote startNote = song.notes[i];
        int startSample = Mathf.FloorToInt(startNote.time * sampleRate);
        int endSample = Mathf.FloorToInt(startNote.end * sampleRate);

        // Combine nearby notes if within threshold
        int j = i + 1;
        while (j < song.notes.Length && (song.notes[j].time - startNote.end) <= combineThreshold)
        {
            endSample = Mathf.Max(endSample, Mathf.FloorToInt(song.notes[j].end * sampleRate));
            j++;
        }

        int sampleCount = endSample - startSample;

        if (sampleCount > 0)
        {
            float[] noteSamples = new float[sampleCount * channels];
            System.Array.Copy(fullSamples, startSample * channels, noteSamples, 0, noteSamples.Length);

            // Fade out on the final combined clip
            int fadeSamples = Mathf.FloorToInt(releaseTime * sampleRate);
            fadeSamples = Mathf.Min(fadeSamples, sampleCount);

            for (int k = 0; k < fadeSamples; k++)
            {
                float fade = Mathf.SmoothStep(1f, 0f, (float)k / fadeSamples);
                int sampleIdx = sampleCount - fadeSamples + k;
                for (int ch = 0; ch < channels; ch++)
                {
                    int idx = sampleIdx * channels + ch;
                    if (idx >= 0 && idx < noteSamples.Length)
                        noteSamples[idx] *= fade;
                }
            }

            AudioClip clip = AudioClip.Create($"Combined_Note_{startNote.note}", sampleCount, channels, sampleRate, false);
            clip.SetData(noteSamples, 0);
            noteClips.Add(clip);

            // Assign the same clipIndex to all combined notes
            for (int k = i; k < j; k++)
            {
                song.notes[k].clipIndex = noteClips.Count - 1;
            }
        }

        i = j;
    }

    Debug.Log($"Loaded {noteClips.Count} combined sliced clips.");
}

    public void PlaySong()
    {
        if (song == null || isPlaying) return;
        isPlaying = true;
        current = 0;
        StartCoroutine(PlaySongCoroutine());
    }

    private IEnumerator PlaySongCoroutine()
    {
        float startTime = Time.time;

        while (current < song.notes.Length)
        {
            while (Time.time - startTime < song.notes[current].time)
                yield return null;

            PlayCurrentNote();
            current++;
        }

        isPlaying = false;
        Debug.Log("Song finished!");
    }

    private void PlayCurrentNote()
    {
        if (current >= song.notes.Length) return;

        MidiNote n = song.notes[current];
        if (n.clipIndex < 0 || n.clipIndex >= noteClips.Count) return;

        AudioSource source = GetFreeSource();
        if (source == null) return;

        float volume = Mathf.Clamp01(n.velocity / 127f) * noteVolume;
        source.PlayOneShot(noteClips[n.clipIndex], volume);
    }

    private AudioSource GetFreeSource()
    {
        foreach (var src in audioSources)
            if (!src.isPlaying)
                return src;
        return null;
    }

    public void PlayNote(MidiNote note)
    {
        if (note == null || note.clipIndex < 0 || note.clipIndex >= noteClips.Count) return;

        AudioSource source = GetFreeSource();
        if (source == null) return;

        float volume = Mathf.Clamp01(note.velocity / 127f) * noteVolume;
        source.PlayOneShot(noteClips[note.clipIndex], volume);
    }

    // Called from TileSpawner
    public void StartBackgroundMusic(float delay = 0f)
    {
        if (backgroundAudioSource == null || backgroundAudioSource.clip == null)
        {
            Debug.LogWarning("Background AudioSource or clip not assigned!");
            return;
        }

        if (delay > 0f)
            StartCoroutine(StartBackgroundAfterDelay(delay));
        else
            PlayBackgroundImmediately();
    }

    private IEnumerator StartBackgroundAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        PlayBackgroundImmediately();
    }

    private void PlayBackgroundImmediately()
    {
        if (backgroundAudioSource == null) return;

        backgroundAudioSource.volume = backgroundVolume;
        backgroundAudioSource.Play();
        Debug.Log("Background music started.");
    }

    public void StopSong()
    {
        StopAllCoroutines();
        isPlaying = false;
        current = 0;

        if (backgroundAudioSource != null) backgroundAudioSource.Stop();
    }

    public void ResetSong()
    {
        StopSong();
    }
}