using UnityEngine;

/// <summary>Slow camera sway for menu backgrounds.</summary>
public class MenuCameraDrift : MonoBehaviour
{
    Vector3 basePos; Quaternion baseRot;
    void Start() { basePos = transform.position; baseRot = transform.rotation; }
    void Update()
    {
        float t = Time.unscaledTime * 0.25f;
        transform.position = basePos + new Vector3(Mathf.Sin(t) * 0.25f, Mathf.Sin(t * 1.3f) * 0.1f, 0);
        transform.rotation = baseRot * Quaternion.Euler(Mathf.Sin(t * 0.7f) * 1f, Mathf.Sin(t) * 1.5f, 0);
    }
}
