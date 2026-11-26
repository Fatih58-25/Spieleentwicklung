using UnityEngine;

public class SchluesselSchweben : MonoBehaviour
{
    public float drehGeschwindigkeit = 45f;
    public float hub = 0.04f;
    public float tempo = 1.5f;

    Vector3 start;

    void Start()
    {
        start = transform.localPosition;
    }

    void Update()
    {
        transform.Rotate(0f, drehGeschwindigkeit * Time.deltaTime, 0f, Space.World);
        transform.localPosition = start + Vector3.up * Mathf.Sin(Time.time * tempo) * hub;
    }
}
