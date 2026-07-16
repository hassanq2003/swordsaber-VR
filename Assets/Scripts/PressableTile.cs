using UnityEngine;
using UnityEngine.Events;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class PressableTile : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;

    [Header("Hit Animation")]
    public float pressDistance = 0.2f;
    public float forwardDistance = 0.2f;
    public float pressDuration = 0.15f;

    [Header("Glow")]
    public Color glowColor = Color.cyan;
    public float glowIntensity = 5f;

    [Header("Events")]
    public UnityEvent onPressed;

    private bool isPressed;

    private Transform target;
    private MidiNote noteData;
    private TileSpawner spawner;

    private Rigidbody rb;

    private Renderer tileRenderer;
    private Material tileMaterial;
    private Color originalEmission;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Physics settings
        rb.useGravity = false;
        rb.isKinematic = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation |
                         RigidbodyConstraints.FreezePositionZ;

        tileRenderer = GetComponent<Renderer>();

        if (tileRenderer != null)
        {
            tileMaterial = tileRenderer.material;

            if (tileMaterial.HasProperty("_EmissionColor"))
            {
                tileMaterial.EnableKeyword("_EMISSION");
                originalEmission = tileMaterial.GetColor("_EmissionColor");
            }
        }
    }

    public void Initialize(
        Transform targetPoint,
        float speed,
        MidiNote note,
        TileSpawner owner)
    {
        target = targetPoint;
        moveSpeed = speed;
        noteData = note;
        spawner = owner;

        isPressed = false;

        rb.linearVelocity = Vector3.right * moveSpeed;

        ResetGlow();
    }

    private void FixedUpdate()
    {
        if (isPressed)
            return;

        // Keep moving using physics
        rb.linearVelocity = new Vector3(
            moveSpeed,
            rb.linearVelocity.y,
            0f);

        if (transform.position.x >= target.position.x)
        {
            rb.linearVelocity = Vector3.zero;
            spawner.TileMissed(this, noteData);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log("Collision with: " + collision.gameObject.name);

        if (!collision.gameObject.CompareTag("Player"))
            return;

        if (isPressed)
            return;

        isPressed = true;

        rb.linearVelocity = Vector3.zero;

        onPressed?.Invoke();

        StartCoroutine(HitAnimation());
    }

    private IEnumerator HitAnimation()
    {
        Vector3 hitDirection =
            (Vector3.right * forwardDistance +
             Vector3.down * pressDistance).normalized;

        float hitSpeed = Mathf.Sqrt(
            forwardDistance * forwardDistance +
            pressDistance * pressDistance) / pressDuration;

        rb.linearVelocity = hitDirection * hitSpeed;

        float t = 0f;

        while (t < pressDuration)
        {
            t += Time.deltaTime;

            if (tileMaterial != null)
            {
                float percent = t / pressDuration;
                float intensity = Mathf.Lerp(
                    glowIntensity,
                    0f,
                    percent);

                tileMaterial.SetColor(
                    "_EmissionColor",
                    glowColor * intensity);
            }

            yield return null;
        }

        rb.linearVelocity = Vector3.zero;

        ResetGlow();

        spawner.TilePressed(this, noteData);
    }

    private void ResetGlow()
    {
        if (tileMaterial != null)
        {
            tileMaterial.SetColor(
                "_EmissionColor",
                originalEmission);
        }
    }

    public void ResetTile()
    {
        StopAllCoroutines();

        isPressed = false;

        rb.linearVelocity = Vector3.zero;

        ResetGlow();
    }

    public void Deactivate()
    {
        ResetTile();
        gameObject.SetActive(false);
    }
}