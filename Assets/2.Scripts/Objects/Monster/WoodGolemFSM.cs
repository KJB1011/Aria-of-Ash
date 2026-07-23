// ============================================================================
// WoodGolemFSM.cs
// ----------------------------------------------------------------------------
// 우드골렘. Idle/Trace/Chase/BodyAttack/Hit/Return 등 기본 행동은 MonsterFSM(부모)의
// 슬라임과 동일한 로직을 그대로 씁니다. 다른 건 원거리 공격뿐입니다.
//
// 슬라임(SplashAttack)은 한 번 던지고 끝나지만, 우드골렘은 제자리에 서서
// 플레이어를 계속 조준한 채 splashAttackStateDuration(기본 3초) 동안
// fireInterval 간격으로 투사체를 반복 발사합니다.
//
// [조준 높이]
// target.position은 보통 플레이어 캐릭터 루트(CharacterController 기준 발밑) 좌표라, 그대로 조준하면
// 투사체가 계속 발을 향해 날아갑니다. aimHeightOffset만큼 위로 띄운 지점을 조준하도록 했습니다.
//
// [씬 준비]
//   - MonsterFSM/SlimeFSM과 동일합니다 (NavMeshAgent, Animator 파라미터 등).
//   - Projectile Prefab과 Projectile Spawn Point(비워두면 자기 자신)를 인스펙터에서 연결하세요.
//   - Animator의 SplashAttack 스테이트는 3초짜리 "계속 쏘는" 모션(루프 또는 3초 길이 클립)으로 구성하세요.
// ============================================================================

using UnityEngine;

public class WoodGolemFSM : MonsterFSM
{
    [Header("우드골렘 - 연속 원거리 공격")]
    [Tooltip("발사할 투사체 프리팹")]
    public GameObject projectilePrefab;
    [Tooltip("투사체가 생성될 위치. 비워두면 자신의 Transform을 사용합니다.")]
    public Transform projectileSpawnPoint;
    [Tooltip("연속 발사 간격(초). splashAttackStateDuration 동안 이 간격마다 한 발씩 나갑니다.")]
    public float fireInterval = 0.5f;
    [Tooltip("투사체 이동 속도")]
    public float projectileSpeed = 15f;
    [Tooltip("조준 지점을 target.position에서 위로 얼마나 띄울지(미터). target.position은 보통 플레이어 " +
              "루트(발밑) 기준이라 그대로 조준하면 투사체가 발을 향해 날아갑니다 - 몸통/가슴 높이 정도로 " +
              "띄워서 실제로 몸에 맞는 것처럼 보이게 하세요.")]
    public float aimHeightOffset = 1f;

    private float fireTimer;

    // 컴포넌트를 처음 추가하거나 인스펙터에서 Reset했을 때만 호출됩니다.
    // 우드골렘은 3초간 서서 계속 쏘는 원거리 공격을 가지므로 기본 지속시간을 3초로 맞춰둡니다.
    private void Reset()
    {
        splashAttackStateDuration = 3f;
    }

    protected override void OnSplashAttackTrigger()
    {
        // 상태에 진입하자마자 곧바로 첫 발이 나가도록 타이머를 0으로 초기화합니다.
        fireTimer = 0f;
    }

    protected override void UpdateSplashAttack()
    {
        // 조준 보정(aimTurnSpeed), 지속시간 카운트다운, 시간이 다 되면 다음 행동으로 넘어가는 처리는
        // 부모 클래스 로직을 그대로 재사용합니다.
        base.UpdateSplashAttack();

        // base 처리 중 이미 다른 상태로 넘어갔다면(지속시간 종료 등) 더 이상 발사하지 않습니다.
        if (CurrentState != State.SplashAttack) return;

        fireTimer -= Time.deltaTime;
        if (fireTimer <= 0f)
        {
            FireProjectile();
            fireTimer = fireInterval;
        }
    }

    private void FireProjectile()
    {
        if (projectilePrefab == null || target == null) return;

        Transform spawnPoint = projectileSpawnPoint != null ? projectileSpawnPoint : transform;
        Vector3 aimPoint = target.position + Vector3.up * aimHeightOffset;
        Vector3 direction = aimPoint - spawnPoint.position;
        if (direction.sqrMagnitude < 0.0001f) return;

        GameObject instance = Instantiate(projectilePrefab, spawnPoint.position, Quaternion.identity);

        LinearProjectile projectile = instance.GetComponent<LinearProjectile>();
        if (projectile != null)
        {
            // 투사체는 Instantiate로 매번 새로 생기는 독립된 오브젝트라 부모 관계로 MonsterStats를 자동으로
            // 찾을 수 없습니다 - 발사하는 이 시점에 직접 연결해줍니다(FireballProjectile과 같은 방식).
            projectile.sourceMonsterStats = stats;
            projectile.Launch(direction, projectileSpeed);
        }
    }
}