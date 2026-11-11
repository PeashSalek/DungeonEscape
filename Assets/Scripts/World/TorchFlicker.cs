using UnityEngine;

/// <summary>Makes torch and brazier lights flicker like real fire.</summary>
[RequireComponent(typeof(Light))]
public class TorchFlicker : MonoBehaviour
{
    public float amount = 0.25f;
    public float speed = 6f;
    Light lightSource;
    float baseIntensity, seed;

    void Awake()
    {
        lightSource = GetComponent<Light>();
        baseIntensity = lightSource.intensity;
        seed = Random.value * 100f;
    }

    void Update()
    {
        float n = Mathf.PerlinNoise(seed, Time.time * speed);
        lightSource.intensity = baseIntensity * (1f - amount + n * amount * 2f);
    }
}
