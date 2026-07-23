// ============================================================================
// LinearProjectile.cs
// ----------------------------------------------------------------------------
// 우드골렘의 연속 사격처럼 중력 영향 없이 곧게 날아가는 직선형 투사체.
// ============================================================================

using UnityEngine;

public class LinearProjectile : ProjectileBase
{
    /// <summary>direction 방향으로 speed 속도만큼 곧게 발사합니다. 중력의 영향을 받지 않습니다.</summary>
    public void Launch(Vector3 direction, float speed)
    {
        rb.useGravity = false;

        if (direction.sqrMagnitude > 0.0001f)
        {
            direction.Normalize();
            transform.rotation = Quaternion.LookRotation(direction);
        }

        rb.linearVelocity = direction * speed;
    }
}