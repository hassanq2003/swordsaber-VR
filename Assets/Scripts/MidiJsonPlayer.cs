using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class MidiNote
{
    public int note;
    public int velocity;
    public float time;
}

[System.Serializable]
public class MidiSong
{
    public MidiNote[] notes;
}

public class MidiJsonPlayer : MonoBehaviour
{
    [Header("Settings")]
    public int maxVoices = 36;

    private AudioClip[] pianoNotes = new AudioClip[128];
    private float[] notePitch = new float[128];        // Added for missing notes
    private List<AudioSource> audioSources = new List<AudioSource>();
    public MidiSong song;
    private int current = 0;
    private bool isPlaying = false;

    void Awake()
    {
        // Create AudioSource pool
        for (int i = 0; i < maxVoices; i++)
        {
            AudioSource src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            audioSources.Add(src);
        }

        AutoLoadPianoSamples();
        LoadSong();
    }

    private void AutoLoadPianoSamples()
    {
        AudioClip[] loadedClips = Resources.LoadAll<AudioClip>("Piano");

        // Reset arrays
        for (int i = 0; i < 128; i++)
        {
            pianoNotes[i] = null;
            notePitch[i] = 1f;
        }

        // Load real samples
        foreach (AudioClip clip in loadedClips)
        {
            int midiNote = NoteNameToMidi(clip.name);
            if (midiNote >= 0 && midiNote < 128)
            {
                pianoNotes[midiNote] = clip;
                notePitch[midiNote] = 1f;        // Original pitch
            }
        }

        PopulateMissingNotes();

        Debug.Log($"Auto-loaded {loadedClips.Length} real samples and filled missing notes with pitch shifting.");
    }

    private void PopulateMissingNotes()
    {
        for (int target = 0; target < 128; target++)
        {
            if (pianoNotes[target] != null) 
                continue;

            int nearest = -1;
            int bestDistance = int.MaxValue;

            for (int sample = 0; sample < 128; sample++)
            {
                if (pianoNotes[sample] == null) continue;

                int distance = Mathf.Abs(sample - target);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = sample;
                }
            }

            if (nearest != -1)
            {
                pianoNotes[target] = pianoNotes[nearest];
                notePitch[target] = Mathf.Pow(2f, (target - nearest) / 12f);
            }
        }
    }

    void LoadSong()
    {
        TextAsset json = Resources.Load<TextAsset>("song");
        if (json == null)
        {
            Debug.LogError("song.json not found in Resources folder!");
            return;
        }

        song = JsonUtility.FromJson<MidiSong>(json.text);
        
        if (song?.notes != null)
        {
            foreach (var n in song.notes)
                n.time = n.time / 1000f;

            System.Array.Sort(song.notes, (a, b) => a.time.CompareTo(b.time));
            
            Debug.Log($"Loaded {song.notes.Length} notes from JSON.");
        }
    }

    void Update()
    {
        
    }

    public void PlaySong()
    {
        if (song == null || isPlaying) return;
        StartCoroutine(PlaySongCoroutine());
    }

    public void StopSong()
    {
        StopAllCoroutines();
        isPlaying = false;
        current = 0;
        foreach (var src in audioSources) src.Stop();
    }

    private IEnumerator PlaySongCoroutine()
    {
        isPlaying = true;
        current = 0;
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

        if (n.note < 0 || n.note >= pianoNotes.Length || pianoNotes[n.note] == null)
            return;

        AudioSource source = GetFreeSource();
        if (source == null) return;

        float volume = Mathf.Clamp01(n.velocity / 127f) * 0.85f;

        source.pitch = notePitch[n.note];           // Use correct pitch
        source.PlayOneShot(pianoNotes[n.note], volume);
    }

    private AudioSource GetFreeSource()
    {
        foreach (var src in audioSources)
            if (!src.isPlaying) return src;
        return null;
    }

    public void PlayNextNote()
    {
        if (current >= song.notes.Length) return;
        PlayCurrentNote();
        current++;
    }

    public void ResetSong()
    {
        StopSong();
    }

    // Convert "C4", "Ab3", etc. to MIDI number
    private int NoteNameToMidi(string name)
    {
        if (string.IsNullOrEmpty(name)) return -1;

        string n = name.Trim().ToUpper();
        int octave = 0;

        for (int i = n.Length - 1; i >= 0; i--)
        {
            if (char.IsDigit(n[i]))
            {
                octave = int.Parse(n.Substring(i));
                n = n.Substring(0, i);
                break;
            }
        }

        int baseNote = n switch
        {
            "C" => 0,
            "C#" or "DB" => 1,
            "D" => 2,
            "D#" or "EB" => 3,
            "E" => 4,
            "F" => 5,
            "F#" or "GB" => 6,
            "G" => 7,
            "G#" or "AB" => 8,
            "A" => 9,
            "A#" or "BB" => 10,
            "B" => 11,
            _ => -1
        };

        return baseNote + (octave * 12);
    }
    public void PlayNote(int midiNote, int velocity)
{
    if (midiNote < 0 || midiNote >= pianoNotes.Length || pianoNotes[midiNote] == null)
        return;

    // Use your existing logic with pitch if needed
    AudioSource source = GetFreeSource(); // reuse your pool
    if (source != null)
    {
        float volume = Mathf.Clamp01(velocity / 127f) * 0.85f;
        source.pitch = notePitch[midiNote];
        source.PlayOneShot(pianoNotes[midiNote], volume);
    }
}
}