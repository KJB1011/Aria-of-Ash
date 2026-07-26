// ============================================================================
// PlayerSkillProjectile.cs
// ----------------------------------------------------------------------------
// 우클릭 스킬(파이어볼) 발사를 담당합니다. AttackHitbox(근접 판정)와 달리 원거리라
// AttackArea 쪽엔 넣지 않고 Player 오브젝트에 별도로 붙이는 컴포넌트입니다.
//
// Skill 애니메이션 클립에서 "손에서 파이어볼이 실제로 떠나는 프레임"에 걸어둔
// Animation Event(OnFireballRelease)가 호출되면, 그 순간 FireballProjectile을
// 생성해서 날립니다. PlayerTargeting이 감지한 가장 가까운 적이 있으면 그쪽을 정확히
// 조준해서 날아가고, 감지된 적이 없으면 캐릭터가 그 순간 바라보고 있는 정면으로
// 곧게 날아갑니다 (Skill 시작 시 PlayerController.FaceNearestTargetIfAny가 이미
// 캐릭터를 타겟 쪽으로 스냅 회전시켜두기 때문에, 사실상 대부분 같은 방향입니다 -
// 여기서는 정확도를 위해 캐릭터 정면이 아니라 타겟 위치를 직접 다시 조준합니다).
//
// 데미지는 여기서 미리 계산해서 넣어주지 않고, damagePercent와 playerStats(발사자 스탯) 참조를
// 그대로 파이어볼에 실어 보냅니다 - 폭발 범위 안에 여러 대상이 있을 수 있고, 대상마다 방어력
// (MonsterStats.TotalDefense)이 다를 수 있어서 실제 데미지 계산은 FireballProjectile이 맞는
// 순간 대상별로 따로 합니다 (AttackHitbox와 동일한 CalculateDamage 방식, 치명타 확률/피해량 반영).
//
// [씬 준비]
//   1) Player 오브젝트에 이 스크립트를 추가하세요.
//   2) Fire Point에 파이어볼이 생성될 위치(손이나 지팡이 끝)를 빈 자식 오브젝트로 만들어
//      연결하세요. 비워두면 이 오브젝트 자신의 위치에서 발사됩니다.
//   3) Fireball Prefab에 FireballProjectile.cs가 붙은 프리팹을 연결하세요.
//   4) Skill 애니메이션 클립에서 파이어볼이 손을 떠나는 프레임에 OnFireballRelease
//      Animation Event를 추가하세요. Animator가 Player와 다른 모델 오브젝트에 있다면
//      AnimationEventRelay에도 이미 전달 경로가 추가되어 있으니 그대로 쓰시면 됩니다.
//
// [스킬강화 - SkillInfo 트리]
//   playerStats.HasSkillUpgrade가 켜져있으면(SkillInfo에서 '스킬강화'를 해제하면) 발사되는 순간
//   세 가지가 한꺼번에 바뀝니다: 크기가 projectileScale(기본 0.3) 대신 upgradedProjectileScale
//   (기본 0.5)로 발사되고, 폭발 범위(FireballProjectile.explosionRadius)에 upgradedExplosionRadiusMultiplier
//   (기본 1.5 = +50%)가 곱해지고, damagePercent에 upgradedDamagePercentMultiplier(기본 1.3 = +30%)가
//   곱해집니다. 강화 전에는 지금까지와 완전히 동일하게 동작합니다.
// ============================================================================

using UnityEngine;

public class PlayerSkillProjectile : MonoBehaviour
{
    [Header("참조")]
    public FireballProjectile fireballPrefab;
    [Tooltip("파이어볼이 생성될 위치. 보통 손이나 지팡이 끝에 빈 오브젝트를 만들어 연결하세요. " +
              "비워두면 이 오브젝트 자신의 위치에서 발사됩니다.")]
    public Transform firePoint;
    [Tooltip("비워두면 같은 오브젝트에서 PlayerTargeting을 자동으로 찾습니다. 감지된 적이 있으면 그쪽을 조준합니다.")]
    public PlayerTargeting targeting;
    [Tooltip("비워두면 같은 오브젝트에서 PlayerStats를 자동으로 찾습니다.")]
    public PlayerStats playerStats;

    [Header("발사")]
    public float projectileSpeed = 20f;
    [Tooltip("이 스킬의 데미지 배율(%). 최종 데미지는 폭발이 터지는 순간 FireballProjectile이 대상별로 " +
              "PlayerStats.CalculateDamage(damagePercent, 그 대상의 방어력)를 호출해서 계산합니다 " +
              "(총 공격력 × damagePercent%, 치명타 확률/피해량 반영).")]
    public float damagePercent = 150f;
    [Tooltip("이 스킬이 적중했을 때 충전되는 필살기 에너지량입니다(기획 스펙: 스킬 적중 시 30 충전). " +
              "AttackHitbox.energyOnHit와 같은 개념이지만, 스킬은 근접 히트박스가 아니라 이 파이어볼 " +
              "발사체를 통해서만 적을 맞히므로 여기 별도로 둡니다. 폭발 범위 안의 여러 대상을 동시에 " +
              "맞혀도(광역) 한 번만 충전되고, 아무도 못 맞혔다면(허공에 쏨) 충전되지 않습니다 - " +
              "FireballProjectile.Explode()가 실제로 데미지가 들어간 대상이 있을 때만 충전합니다.")]
    public float energyOnHit = 30f;
    [Tooltip("타겟을 조준할 때 발밑이 아니라 몸통 높이를 겨냥하도록 더해주는 값(미터).")]
    public float aimHeightOffset = 1f;
    [Tooltip("파이어볼이 발사되는(손을 떠나는) 순간 재생할 효과음 이름입니다(Resources/SFX/ 아래 클립 " +
              "이름과 일치해야 함). 비워두면 발사음 없이 파이어볼만 나갑니다. 맞았을 때 나는 소리는 " +
              "여기가 아니라 Fireball Prefab(FireballProjectile)의 Hit Sfx Name에서 설정하세요.")]
    public string castSfxName;

    [Header("스킬강화 (SkillInfo에서 해제 시)")]
    [Tooltip("평소(강화 전) 파이어볼의 시각적 크기입니다(Transform.localScale, 세 축 동일). 기획 스펙: 0.3.")]
    public float projectileScale = 0.3f;
    [Tooltip("SkillInfo의 '스킬강화'를 해제한 뒤 파이어볼의 시각적 크기입니다. 기획 스펙: 0.5.")]
    public float upgradedProjectileScale = 0.5f;
    [Tooltip("SkillInfo의 '스킬강화'를 해제하면 폭발 범위(FireballProjectile.explosionRadius)에 곱해지는 " +
              "배율입니다. 기획 스펙: 범위 +50%.")]
    public float upgradedExplosionRadiusMultiplier = 1.5f;
    [Tooltip("SkillInfo의 '스킬강화'를 해제하면 damagePercent에 곱해지는 배율입니다. 기획 스펙: 데미지 +30%.")]
    public float upgradedDamagePercentMultiplier = 1.3f;

    private void Awake()
    {
        if (targeting == null) targeting = GetComponent<PlayerTargeting>();
        if (playerStats == null) playerStats = GetComponent<PlayerStats>();
    }

    /// <summary>Animation Event 전용. 손에서 파이어볼이 실제로 떠나는 프레임에 이 이벤트를 추가하세요.</summary>
    public void OnFireballRelease()
    {
        if (fireballPrefab == null)
        {
            Debug.LogWarning("[PlayerSkillProjectile] fireballPrefab이 비어있어 파이어볼을 발사할 수 없습니다.", this);
            return;
        }

        Vector3 spawnPosition = firePoint != null ? firePoint.position : transform.position;
        Vector3 direction = GetAimDirection(spawnPosition);

        if (!string.IsNullOrEmpty(castSfxName))
        {
            SoundManager.Instance.PlaySFX(castSfxName, spawnPosition);
        }

        bool upgraded = playerStats != null && playerStats.HasSkillUpgrade;

        FireballProjectile fireball = Instantiate(fireballPrefab, spawnPosition, Quaternion.identity);

        fireball.damagePercent = upgraded ? damagePercent * upgradedDamagePercentMultiplier : damagePercent;
        fireball.sourceStats = playerStats;
        fireball.energyOnHit = energyOnHit;

        float scale = upgraded ? upgradedProjectileScale : projectileScale;
        fireball.transform.localScale = Vector3.one * scale;

        if (upgraded)
        {
            fireball.explosionRadius *= upgradedExplosionRadiusMultiplier;
        }

        fireball.Launch(direction, projectileSpeed);
    }

    private Vector3 GetAimDirection(Vector3 fromPosition)
    {
        if (targeting != null && targeting.CurrentTarget != null)
        {
            Vector3 aimPoint = targeting.CurrentTarget.position + Vector3.up * aimHeightOffset;
            Vector3 direction = aimPoint - fromPosition;
            if (direction.sqrMagnitude > 0.0001f) return direction.normalized;
        }

        return transform.forward;
    }
}