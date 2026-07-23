// ============================================================================
// ProjectileBase.cs
// ----------------------------------------------------------------------------
// 모든 투사체의 공용 베이스 클래스. 데미지, 충돌 판정, 수명, 피격 이펙트처럼
// "발사 방식과 무관하게 공통인 것"만 여기서 처리합니다.
// 실제로 "어떻게 날아가는지"(포물선/직선)는 이 클래스를 상속한 ArcProjectile,
// LinearProjectile 등에서 결정합니다.
//
// [프리팹 준비]
//   1) Rigidbody 추가 (자동으로 요구됨)
//   2) Collider 추가하고 Is Trigger 체크 (물리적으로 밀려나지 않고 판정만 하도록)
//   3) Hit Mask에 플레이어가 속한 레이어를 지정
//   4) 이 클래스를 직접 붙이지 말고, ArcProjectile 또는 LinearProjectile을 붙이세요.
//   5) 맞았을 때 DamageNumberManager.Instance.Show()로 맞은 지점 위에 데미지 숫자를 띄웁니다
//      (몬스터가 플레이어를 쏘는 용도이므로 항상 DamageNumberTeam.Player로 표시됩니다).
//
// [히트 VFX]
//   hitVfxName에 이름을 넣어두면, 맞는 순간 VFXManager.Instance.Play()로 그 이름의 이펙트를
//   재생합니다(Assets/Resources/VFX/ 아래에 그 이름의 프리팹이 있어야 합니다). 비워두면 VFX
//   없이 데미지만 들어갑니다. 예: 슬라임의 ArcProjectile 프리팹은 "FX_SmallSlime_Hit", 우드골렘의
//   LinearProjectile 프리팹은 "FX_WoodGolem_Hit"로 설정하세요.
//   [주의] 예전에는 이 필드가 GameObject를 직접 참조하는 hitEffectPrefab이라 Instantiate/Destroy로
//   재생했지만, VFXManager의 오브젝트 풀링 방식으로 바꿨습니다. 기존에 hitEffectPrefab에 프리팹을
//   연결해두셨다면 그 연결은 사라지므로, 대신 이 이름(hitVfxName)에 그 프리팹과 같은 이름을
//   문자열로 넣어주세요 (프리팹은 Assets/Resources/VFX/ 아래에 있어야 VFXManager가 찾을 수 있습니다).
//
// [데미지 계산 - Player의 AttackHitbox/MonsterAttackHitbox와 동일한 방식]
//   고정 데미지 대신, 이 투사체를 쏜 몬스터의 MonsterStats.CalculateDamage()로 최종 데미지를
//   계산합니다 - (몬스터의 총 공격력 - 맞은 플레이어의 총 방어력) × (monsterDamagePercent%)입니다.
//   sourceMonsterStats는 발사한 몬스터가 직접 참조를 넘겨줘야 합니다(SlimeFSM.OnSplashAttackTrigger()/
//   WoodGolemFSM.FireProjectile()가 Launch() 호출 전에 자동으로 연결해줍니다) - 이 프리팹은 씬에
//   미리 배치된 게 아니라 Instantiate로 매번 새로 생기기 때문에, 부모 관계로 자동으로 찾을 수 없어서
//   그렇습니다(FireballProjectile의 sourceStats/damagePercent와 같은 이유).
//   [이름 주의] FireballProjectile은 플레이어 스킬용이라 이미 자신만의 damagePercent(float)/
//   sourceStats(PlayerStats) 필드를 따로 갖고 있습니다 - 부모 클래스인 여기에 같은 이름으로 필드를
//   두면 중복 직렬화 경고가 나기 때문에, 몬스터 전용 필드는 일부러 다른 이름(monsterDamagePercent/
//   sourceMonsterStats)을 썼습니다.
// ============================================================================

using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public abstract class ProjectileBase : MonoBehaviour
{
    [Header("공통 - 투사체")]
    [Tooltip("이 투사체를 쏜 몬스터 기준 데미지 배율(%). 최종 데미지는 sourceMonsterStats.CalculateDamage(" +
              "monsterDamagePercent, 맞은 플레이어의 방어력)로 계산됩니다. 예: 80을 넣으면 몬스터 공격력의 " +
              "80%가 데미지입니다.")]
    public float monsterDamagePercent = 100f;
    [Tooltip("데미지 계산에 쓸 발사자(몬스터)의 스탯입니다. 이 투사체를 발사하는 SlimeFSM/WoodGolemFSM 등이 " +
              "Launch() 호출 전에 자동으로 연결해줍니다 - 직접 값을 넣을 필요는 없습니다.")]
    public MonsterStats sourceMonsterStats;
    [Tooltip("이 레이어에 속한 대상만 맞은 것으로 판정합니다. (보통 Player 레이어)")]
    public LayerMask hitMask;
    [Tooltip("아무것도 안 맞아도 이 시간(초)이 지나면 자동으로 사라집니다.")]
    public float lifeTime = 5f;
    [Tooltip("맞았을 때 재생할 VFX 이름 (Resources/VFX/ 아래 프리팹 이름과 일치해야 함). 비워두면 VFX " +
              "없이 데미지만 적용됩니다. 예: \"FX_SmallSlime_Hit\", \"FX_WoodGolem_Hit\"")]
    public string hitVfxName;
    [Tooltip("데미지가 들어갈 때, 데미지 숫자를 맞은 대상 위로 얼마나 띄울지(미터).")]
    public float damageNumberHeightOffset = 0.8f;

    protected Rigidbody rb;

    // 트리거 충돌은 "같은 물리 프레임"에 여러 콜라이더와 동시에 겹치면 OnTriggerEnter가 여러 번
    // 호출될 수 있습니다(예: 맞은 대상이 콜라이더를 2개 이상 가진 경우). 이 투사체는 한 번 맞으면
    // 바로 Destroy(gameObject)를 호출하지만, 실제 파괴는 그 프레임의 끝까지 미뤄지기 때문에
    // 그 사이에 OnTriggerEnter가 또 호출되면 데미지/VFX/데미지 숫자가 중복으로 발생할 수 있습니다.
    // hasHit 플래그로 "이미 한 번 처리됐다"는 걸 기록해서 이런 중복 처리를 막습니다.
    protected bool hasHit;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        Destroy(gameObject, lifeTime);
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (hasHit) return; // 이미 한 번 맞아서 처리 중(파괴 대기 중)이면 무시합니다.
        if (((1 << other.gameObject.layer) & hitMask) == 0) return;

        hasHit = true;

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            PlayerStats playerStats = other.GetComponentInParent<PlayerStats>();
            float targetDefense = playerStats != null ? playerStats.TotalDefense : 0f;

            DamageResult result = sourceMonsterStats != null
                ? sourceMonsterStats.CalculateDamage(monsterDamagePercent, targetDefense)
                : default;
            damageable.TakeDamage(result.damage);

            // ProjectileBase는 몬스터가 플레이어를 노리고 쏘는 투사체 전용이라 항상 Player 팀으로 표시합니다.
            Vector3 numberPosition = other.ClosestPoint(transform.position) + Vector3.up * damageNumberHeightOffset;
            DamageNumberManager.Instance.Show(result.damage, numberPosition, false, DamageNumberTeam.Player);
        }

        if (!string.IsNullOrEmpty(hitVfxName))
        {
            VFXManager.Instance.Play(hitVfxName, transform.position);
        }

        Destroy(gameObject);
    }
}