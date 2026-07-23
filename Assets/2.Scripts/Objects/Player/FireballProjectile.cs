// ============================================================================
// FireballProjectile.cs
// ----------------------------------------------------------------------------
// 플레이어 스킬(우클릭)용 파이어볼. ProjectileBase를 상속해서 공통 기능(Rigidbody,
// 수명, hitMask)은 그대로 쓰고, 맞았을 때의 동작만 다르게 오버라이드했습니다 -
// LinearProjectile처럼 단일 대상만 맞히는 게 아니라, 맞은 지점을 중심으로
// explosionRadius 범위 안의 모든 대상에게 데미지를 주는 소규모 폭발형입니다.
//
// [프리팹 준비]
//   1) 새 프리팹을 만들고 이 스크립트를 붙이세요 (Rigidbody는 RequireComponent로 자동 추가됩니다).
//   2) Collider를 추가하고 Is Trigger를 체크하세요.
//   3) Hit Mask에는 "적(Enemy)" 레이어를 지정하세요. ProjectileBase 설명에는
//      "보통 Player 레이어"라고 되어있는데, 그건 몬스터가 쏘는 투사체 기준이고
//      이건 반대로 플레이어가 쏘는 거라 Enemy 레이어로 지정해야 합니다.
//   4) 데미지는 이 프리팹에 직접 값을 넣지 않아도 됩니다 - PlayerSkillProjectile이 발사하는 순간
//      damagePercent와 sourceStats(PlayerStats)를 자동으로 연결해주고, 폭발 범위 안의 대상마다
//      맞는 순간 각자의 방어력(MonsterStats.TotalDefense)을 반영해서 데미지를 따로 계산합니다.
//      ProjectileBase에서 물려받은 damage 필드는 이 클래스에서는 사용하지 않습니다.
//   5) 원한다면 폭발 이펙트를 Hit Vfx Name(ProjectileBase에서 물려받은 필드)에 이름으로 연결하세요
//      (Resources/VFX/ 아래 프리팹 이름과 일치해야 합니다).
//   6) 데미지가 들어가는 대상마다 그 위치에 DamageNumberManager.Instance.Show()로 데미지 숫자를
//      띄웁니다(DamageNumberTeam.Enemy로, 치명타면 더 크고 다른 색으로 표시됩니다).
//   7) 필살기 에너지 충전도 여기서 처리합니다 - energyOnHit 값도 damagePercent처럼 PlayerSkillProjectile이
//      발사하는 순간 자동으로 채워주고, 폭발 범위 안에서 실제로 데미지가 들어간 대상이 하나라도
//      있을 때만(Explode() 참고) sourceStats.AddEnergy()를 한 번 호출합니다.
// ============================================================================

using UnityEngine;

public class FireballProjectile : ProjectileBase
{
    [Header("파이어볼 - 폭발")]
    [Tooltip("맞은 지점을 중심으로 이 반지름 안의 모든 대상에게 데미지를 줍니다 (단일 대상이 아니라 범위 피해).")]
    public float explosionRadius = 2f;

    [Header("파이어볼 - 데미지")]
    [Tooltip("이 스킬의 데미지 배율(%). PlayerSkillProjectile이 발사할 때 자동으로 채워줍니다. " +
              "폭발 범위 안의 각 대상마다 sourceStats.CalculateDamage(damagePercent, 그 대상의 방어력)로 " +
              "따로 계산됩니다 - ProjectileBase에 있는 monsterDamagePercent/sourceMonsterStats(몬스터 " +
              "투사체 전용)는 여기서 쓰지 않습니다.")]
    public float damagePercent;
    [Tooltip("데미지 계산에 쓸 발사자(플레이어)의 스탯. PlayerSkillProjectile이 발사할 때 자동으로 연결해줍니다.")]
    public PlayerStats sourceStats;
    // damageNumberHeightOffset은 ProjectileBase에 이미 있는 필드를 그대로 물려받아 씁니다.
    // (전에는 여기서 같은 이름으로 다시 선언해서, 부모/자식 클래스에 같은 필드명이 중복 직렬화되는
    // 문제가 있었습니다 - Unity가 "The same field name is serialized multiple times" 경고를 냅니다.)

    [Header("파이어볼 - 필살기 에너지")]
    [Tooltip("폭발 범위 안의 대상을 실제로 하나 이상 맞혔을 때 충전되는 필살기 에너지량입니다. " +
              "PlayerSkillProjectile이 발사할 때 자동으로 채워줍니다(기획 스펙: 스킬 적중 시 30). " +
              "여러 대상을 동시에 맞혀도 한 번만 충전되고, 허공에 쏴서 아무도 못 맞히면 충전되지 않습니다.")]
    public float energyOnHit;

    /// <summary>direction 방향으로 speed 속도만큼 곧게 날아갑니다. 중력 영향 없음.</summary>
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

    protected override void OnTriggerEnter(Collider other)
    {
        // ProjectileBase처럼 곧바로 그 하나만 때리고 끝내는 대신, 맞는 순간 범위 폭발로 처리합니다.
        // hasHit(ProjectileBase 상속)으로 같은 프레임에 여러 콜라이더와 동시에 부딪혀도 폭발이
        // 한 번만 일어나도록 막습니다 - 안 그러면 Destroy(gameObject)가 그 프레임 끝까지 실제로
        // 적용되지 않는 사이에 Explode()가 또 호출돼서 범위 안의 대상들이 데미지를 두 번 맞을 수 있습니다.
        if (hasHit) return;
        if (((1 << other.gameObject.layer) & hitMask) == 0) return;

        hasHit = true;
        Explode();
    }

    private void Explode()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, hitMask);
        bool hitAnyTarget = false;

        foreach (Collider col in hits)
        {
            IDamageable damageable = col.GetComponentInParent<IDamageable>();
            if (damageable == null) continue;

            hitAnyTarget = true;

            // 폭발 범위 안에 여러 대상이 있으면 각자의 방어력을 따로 반영해서 데미지를 계산합니다.
            MonsterStats targetStats = col.GetComponentInParent<MonsterStats>();
            float targetDefense = targetStats != null ? targetStats.TotalDefense : 0f;
            DamageResult result = sourceStats != null ? sourceStats.CalculateDamage(damagePercent, targetDefense) : default;

            damageable.TakeDamage(result.damage);
            ShowDamageNumber(col, result.damage, result.isCrit);
        }

        // AttackHitbox.ChargeEnergyOnce()와 동일한 규칙: 폭발 범위 안에서 실제로 데미지가 들어간
        // 대상이 하나라도 있을 때만 한 번 충전합니다(여러 대상을 동시에 맞혀도 중복 충전 없음).
        if (hitAnyTarget && energyOnHit > 0f && sourceStats != null)
        {
            sourceStats.AddEnergy(energyOnHit);
        }

        if (!string.IsNullOrEmpty(hitVfxName))
        {
            VFXManager.Instance.Play(hitVfxName, transform.position);
        }

        Destroy(gameObject);
    }

    private void ShowDamageNumber(Collider hitCollider, float damage, bool isCrit)
    {
        Vector3 position = hitCollider.ClosestPoint(transform.position) + Vector3.up * damageNumberHeightOffset;
        DamageNumberManager.Instance.Show(damage, position, isCrit, DamageNumberTeam.Enemy);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}