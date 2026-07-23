using UnityEngine;

public class FloatingBoat : MonoBehaviour
{
    [SerializeField] private float amplitude = 0.2f;  // 위아래로 움직일 높이 (0.2)
    [SerializeField] private float duration = 5.0f;   // 한 방향으로 움직이는 시간 (5초)

    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        float offset = Mathf.Sin((Time.time / duration * Mathf.PI) + Mathf.PI) * amplitude;
        transform.position = startPosition + new Vector3(0, offset, 0);
    }
}