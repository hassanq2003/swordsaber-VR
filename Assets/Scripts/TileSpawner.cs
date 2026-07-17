using UnityEngine;
using System.Collections;

public class TileSpawner : MonoBehaviour
{
    [Header("Tile")]
    [SerializeField] private GameObject tilePrefab;

    [Header("Spawn")]
    [SerializeField] private Transform [] spawnPoint;
    [SerializeField] private Transform targetPoint;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Timing")]
    [SerializeField] private float minimumGap = 0.31f;

    [Header("Song")]
    [SerializeField] private MidiJsonPlayer midiPlayer;

    private int spawnIndex;

    private void Start()
    {
        StartSong();
    }

    public void StartSong()
    {
        if (midiPlayer == null || midiPlayer.song == null)
        {
            Debug.LogError("Midi Player or Song is missing.");
            return;
        }

        StopAllCoroutines();

        spawnIndex = 0;

        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        MidiNote[] notes = midiPlayer.song.notes;

        while (spawnIndex < notes.Length)
        {
            SpawnTile(notes[spawnIndex]);

            if (spawnIndex == notes.Length - 1)
                break;

            float gap = notes[spawnIndex + 1].time - notes[spawnIndex].time;

            yield return new WaitForSeconds(Mathf.Max(minimumGap, gap));

            spawnIndex++;
        }
    }

    private void SpawnTile(MidiNote note)
    {
        Transform s=spawnPoint[Random.Range(0, spawnPoint.Length)];
        GameObject obj = Instantiate(
            tilePrefab,
            s.position,
            s.rotation);

        PressableTile tile = obj.GetComponent<PressableTile>();

        tile.Initialize(
            targetPoint,
            moveSpeed,
            note,
            this);
    }

    public void TilePressed(PressableTile tile, MidiNote note)
    {
        midiPlayer.PlayNote(note.note, note.velocity);

        Destroy(tile.gameObject);
    }

    public void TileMissed(PressableTile tile, MidiNote note)
    {
        Destroy(tile.gameObject);
    }
}