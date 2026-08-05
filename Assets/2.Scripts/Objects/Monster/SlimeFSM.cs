// ============================================================================
// SlimeFSM.cs
// ----------------------------------------------------------------------------
// 슬라임 몬스터. 공용 FSM 로직은 MonsterFSM(부모 클래스)에 있고,
// 여기서는 슬라임만의 고유 동작(투사체 생성 등)만 override 합니다.
// 다른 몬스터를 추가할 땐 이 파일처럼 MonsterFSM을 상속한 새 클래스를 만드세요.
//
// [근접 공격(BodyAttack) 데미지]
// 코드에서 처리하지 않습니다 - 슬라임 오브젝트 아래 "MonsterAttackArea" 자식 오브젝트를 만들고
// 그 아래 "BodyAttack" 히트박스(BoxCollider + MonsterAttackHitbox, hitVfxName = "FX_SmallSlime_Hit")를
// 두면, BodyAttack 애니메이션 클립의 Animation Event(OnHitboxOpen/OnHitboxClose)가 알아서 열고
// 닫아줍니다. 자세한 설정은 MonsterAttackAreaController.cs를 참고하세요.
//
// [조준 높이]
// target.position은 보통 플레이어 캐릭터 루트(CharacterController 기준 발밑) 좌표라, 그대로 조준하면
// 투사체가 계속 발을 향해 날아갑니다(WoodGolemFSM과 같은 이유). aimHeightOffset만큼 위로 띄운
// 지점을 조준하도록 했습니다.
//
// [공격 시전 사운드 - meleeAttackSfxName / rangedAttackSfxName]
// 둘 다 "공격이 실제로 나가는 순간" 한 번 재생되는 시전음입니다 - 근접은 BodyAttack 애니메이션
// 트리거 직후(OnBodyAttackTrigger), 원거리는 투사체가 생성되는 순간(OnSplashAttackTrigger)입니다.
// 맞았을 때 나는 타격음(MonsterStats.attackHitSfxName, MonsterAttackHitbox/AttackHitbox가 재생)과는
// 완전히 별개입니다 - 비워두면 재생하지 않습니다.
// ============================================================================

using UnityEngine;

public class SlimeFSM : MonsterFSM
{
    [Header("공격 시전 사운드")]
    [Tooltip("근접 공격(BodyAttack)을 시작하는 순간 재생할 효과음입니다(Resources/SFX/ 아래 클립 이름과 " +
              "일치해야 함). 맞았을 때 나는 타격음(MonsterStats.attackHitSfxName)과는 별개입니다.")]
    public string meleeAttackSfxName;
    [Tooltip("원거리 공격 투사체가 생성되는 순간 재생할 효과음입니다(Resources/SFX/ 아래 클립 이름과 " +
              "일치해야 함).")]
    public string rangedAttackSfxName;

    [Header("슬라임 - 곡사형 투사체")]
    [Tooltip("ArcProjectile 컴포넌트가 붙어있는 프리팹")]
    public GameObject splashProjectilePrefab;
    [Tooltip("투사체가 생성될 위치(입 등). 비워두면 자신의 Transform을 사용합니다.")]
    public Transform projectileSpawnPoint;
    [Tooltip("투사체가 목표 지점까지 도달하는 데 걸리는 시간(초). 짧을수록 빠르고 납작한 포물선, 길수록 높고 느린 포물선이 됩니다.")]
    public float arcFlightTime = 1f;
    [Tooltip("조준 지점을 target.position에서 위로 얼마나 띄울지(미터). target.position은 보통 플레이어 " +
              "루트(발밑) 기준이라 그대로 조준하면 투사체가 발을 향해 날아갑니다 - 몸통 높이 정도로 " +
              "띄워서 실제로 몸에 맞는 것처럼 보이게 하세요.")]
    public float aimHeightOffset = 1f;

    protected override void OnBodyAttackTrigger()
    {
        if (!string.IsNullOrEmpty(meleeAttackSfxName))
        {
            SoundManager.Instance.PlaySFX(meleeAttackSfxName, transform.position);
        }
    }

    protected override void OnSplashAttackTrigger()
    {
        if (!string.IsNullOrEmpty(rangedAttackSfxName))
        {
            SoundManager.Instance.PlaySFX(rangedAttackSfxName, transform.position);
        }

        if (splashProjectilePrefab == null || target == null) return;

        Transform spawnPoint = projectileSpawnPoint != null ? projectileSpawnPoint : transform;
        Vector3 aimPoint = target.position + Vector3.up * aimHeightOffset;
        GameObject instance = Instantiate(splashProjectilePrefab, spawnPoint.position, Quaternion.identity);

        ArcProjectile projectile = instance.GetComponent<ArcProjectile>();
        if (projectile != null)
        {
            // 투사체는 Instantiate로 매번 새로 생기는 독립된 오브젝트라 부모 관계로 MonsterStats를 자동으로
            // 찾을 수 없습니다 - 발사하는 이 시점에 직접 연결해줍니다(FireballProjectile과 같은 방식).
            projectile.sourceMonsterStats = stats;
            projectile.Launch(spawnPoint.position, aimPoint, arcFlightTime);
        }
    }

    protected override void OnHitTrigger()
    {
        // TODO: 피격 이펙트/사운드가 필요하면 여기서 재생하세요.
    }
}