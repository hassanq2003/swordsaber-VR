using UnityEngine;
using System.Collections;

public class TileSpawner : MonoBehaviour
{
    [Header("Tile")]
    [SerializeField] private GameObject tilePrefab;

    [Header("Spawn")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform targetPoint;
    [SerializeField] private float spawnGap = 1f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Song")]
    [SerializeField] private MidiJsonPlayer midiPlayer;

    private int currentIndex;

    public void StartSong()
    {
        if (midiPlayer == null || midiPlayer.song == null)
        {
            Debug.LogError("Midi Player or Song is missing.");
            return;
        }

        StopAllCoroutines();

        currentIndex = 0;

        StartCoroutine(SpawnRoutine());
    }
    void Start()
    {
        StartSong();
    }
    private IEnumerator SpawnRoutine()
    {
        while (currentIndex < midiPlayer.song.notes.Length)
        {
            SpawnTile();

            yield return new WaitForSeconds(spawnGap);
        }
    }

    private void SpawnTile()
    {
        GameObject obj = Instantiate(
            tilePrefab,
            spawnPoint.position,
            spawnPoint.rotation);

        PressableTile tile = obj.GetComponent<PressableTile>();

        tile.Initialize(
            targetPoint,
            moveSpeed,
            midiPlayer.song.notes[currentIndex],
            this);
    }

    // Called by PressableTile when pressed
    public void TilePressed(PressableTile tile, MidiNote note)
    {
        midiPlayer.PlayNote(note.note, note.velocity);

        currentIndex++;

        Destroy(tile.gameObject);
    }

    // Called by PressableTile when it reaches the target
    public void TileMissed(PressableTile tile, MidiNote note)
    {
        currentIndex++;

        Destroy(tile.gameObject);
    }
}