using UnityEngine;

// Opens the masked view without changing the surrounding stone geometry.
public sealed class PortalReveal : MonoBehaviour
{
    [SerializeField] float delay = 0.6f;
    [SerializeField] float duration = 1.8f;
    Transform ring;
    Transform swirl;
    Vector3 ringScale;
    Vector3 swirlScale;
    float elapsed;

    void Awake()
    {
        ring = transform.Find("PS_Portal");
        swirl = transform.Find("FX_MOD_PortalSwirl");
        ringScale = ring.localScale;
        swirlScale = swirl.localScale;
        Replay();
    }

    public void Replay()
    {
        elapsed = 0;
        Apply(0.001f);
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01((elapsed - delay) / Mathf.Max(0.01f, duration));
        Apply(Mathf.Max(0.001f, t * t * (3 - 2 * t)));
    }

    void Apply(float amount)
    {
        ring.localScale = new Vector3(ringScale.x * amount, ringScale.y * amount, ringScale.z);
        swirl.localScale = new Vector3(swirlScale.x * amount, swirlScale.y * amount, swirlScale.z);
    }

    void OnDisable()
    {
        if (ring != null) ring.localScale = ringScale;
        if (swirl != null) swirl.localScale = swirlScale;
    }
}
