// ============================================================================
// ArcProjectile.cs
// ----------------------------------------------------------------------------
// 슬라임의 침 뱉기처럼 포물선을 그리며 날아가는 곡사형 투사체.
// Rigidbody의 useGravity를 켠 채로, "flightTime초 뒤에 targetPosition에 도착하는" 초기 속도를
// 역산해서 던지는 방식입니다 (게임에서 흔히 쓰는 projectile motion 공식).
// ============================================================================

using UnityEngine;

public class ArcProjectile : ProjectileBase
{
    private void Update()
    {
        // 날아가는 동안 속도 방향을 바라보도록 회전시켜서 포물선 궤적이 시각적으로 자연스럽게 보이게 합니다.
        if (rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity);
        }
    }

    /// <summary>
    /// startPosition에서 출발해 flightTime초 뒤 targetPosition에 도착하도록 포물선 초기 속도를 계산해서 발사합니다.
    /// </summary>
    public void Launch(Vector3 startPosition, Vector3 targetPosition, float flightTime)
    {
        transform.position = startPosition;

        Vector3 displacement = targetPosition - startPosition;
        Vector3 displacementXZ = new Vector3(displacement.x, 0f, displacement.z);
        float gravity = Mathf.Abs(Physics.gravity.y);

        // 수평 속도: 그냥 거리/시간
        Vector3 velocityXZ = displacementXZ / flightTime;

        // 수직 속도: y(t) = v0y*t - 0.5*g*t^2 을 v0y에 대해 풀면 아래와 같습니다.
        float velocityY = displacement.y / flightTime + 0.5f * gravity * flightTime;

        rb.useGravity = true;
        rb.linearVelocity = velocityXZ + Vector3.up * velocityY;
    }
}