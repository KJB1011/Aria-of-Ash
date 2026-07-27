// ============================================================================
// MiddleSlimeBoss.cs
// ----------------------------------------------------------------------------
// 중간보스 MiddleSlime. 원형 필드 중앙에 고정되어 움직이지 않습니다.
// 그래서 지금까지 만든 MonsterFSM(NavMeshAgent 기반 이동/추격/배회/복귀)을 상속하지 않고
// 완전히 새로운, 이동이 아예 없는 전용 FSM으로 만들었습니다.
//
// [상태]
//   Idle        : 다음 공격까지 idleDurationMin~Max초 대기 (공격 쿨다운 성격).
//                 대기시간이 다 지나도 플레이어가 detectRange 밖에 있으면 공격을 시작하지 않고
//                 계속 Idle에 머무릅니다 (플레이어가 범위 안으로 들어오면 즉시 공격 시작).
//   SwingAttack : 휘두르기 - 플레이어를 따라다니는 원형 범위 표시 → 고정 → 내려찍기
//   WaveAttack  : 파도보내기 - 차징 + 부채꼴 범위 표시 → 파도 3연발
//
// [피격]
//   MiddleSlime은 별도의 Hit(피격) 모션이 없습니다. TakeDamage()가 호출되어도 애니메이터
//   트리거를 쏘지 않고 데미지만 적용합니다. 진행 중인 SwingAttack/WaveAttack의 범위 표시와
//   타이밍도 그대로 유지됩니다 - 공격이 끊기지 않습니다.
//
// [사망]
//   체력이 0이 되면 Dead 상태로 전환되어 진행 중이던 범위 표시(Swing/Wave 인디케이터)를 즉시
//   정리하고 Die 트리거를 쏩니다. 이때 자신의 모든 Collider도 꺼서(DisableColliders()), dieDelay초
//   동안 시체가 씬에 남아있는 사이에도 더 이상 공격 판정에 걸리지 않습니다. dieDelay초 후 오브젝트가
//   파괴되며, Dead 상태에서는 더 이상 어떤 로직도 실행되지 않습니다.
//
// [체력 - MonsterStats]
//   체력(HP)은 이 스크립트가 아니라 같이 붙는 MonsterStats 컴포넌트가 갖고 있습니다
//   ([RequireComponent]로 자동 추가됩니다). Max Health를 조절하려면 MonsterStats의 Base HP를
//   바꾸세요. TakeDamage()는 그대로 이 스크립트에서 받고(IDamageable), MonsterStats에는 수치
//   반영만 위임합니다.
//
// [휘두르기 타이밍]
//   공격 시작 → (swingFollowDuration초 동안 범위 표시가 플레이어를 따라다님)
//             → 고정
//             → (swingSlamDelay초 후) 내려찍기, 그 자리 반지름 swingRadius 안에 데미지
//
// [파도보내기 타이밍]
//   공격 시작 → (waveChargeDuration초 동안 차징 모션 + 부채꼴 범위 표시)
//             → 손을 뻗는 순간부터 waveFireInterval초 간격으로 waveCount번 파도 발사
//   각 파도(ShockwaveWave)는 origin에서 부채꼴을 따라 바깥으로 퍼지며, 높이가
//   Normal Graph처럼 올라왔다 내려가는 곡선을 그립니다. (ShockwaveWave.cs 참고)
//
// [씬 준비]
//   1) 보스 오브젝트를 원형 필드 중앙에 배치하고 이 스크립트를 붙입니다. (NavMesh 불필요)
//   2) Hit Mask에 플레이어가 속한 레이어를 지정하세요 (공격 판정용).
//   3) Target을 비워두면 Start()에서 태그가 "Player"인 오브젝트를 자동으로 찾습니다.
//   4) Animator 파라미터: SwingAttack(Trigger), WaveAttack(Trigger), Die(Trigger) - Hit(피격) 트리거는 없습니다.
//   5) 플레이어 쪽에 IDamageable을 구현한 컴포넌트(체력 스크립트 등)가 있어야 데미지가 실제로 들어갑니다.
//   6) 인스펙터의 "높이 조절" 섹션(swingHeight/waveHeight/waveMaxHeight/lootDropHeight)에서 각 공격
//      패턴의 범위 표시/판정 높이, 파도가 솟아오르는 높이, 전리품이 튀어나오기 시작하는 높이를
//      조절할 수 있습니다.
//   7) 전리품/보상이 튀어나오는 위치를 이 보스의 몸통(transform.position)이 아니라 직접 배치한
//      지점에서 나오게 하고 싶다면, "참조" 섹션의 Loot Drop Point에 미리 씬에 만들어둔 빈
//      오브젝트를 연결하세요 - 비워두면 기존처럼 이 보스의 위치를 기준으로 흩뿌립니다.
//
// [전리품이 안 나올 때 확인할 것 - LootDropper]
//   전리품 드롭은 이 스크립트가 아니라 [RequireComponent]로 자동으로 붙는 LootDropper가 담당합니다
//   (Die()에서 lootDropper.DropLoot(origin)/DropRewards(origin)을 호출만 해줍니다 - origin은
//   lootDropPoint가 있으면 그 위치, 없으면 이 보스의 위치입니다). 코드 흐름 자체는 정상이라,
//   전리품이 하나도 안 나온다면 거의 항상 씬/에셋 설정 문제입니다 - 순서대로 확인하세요:
//     1) 이 오브젝트의 LootDropper 컴포넌트(MiddleSlimeBoss 아래에 자동으로 붙어있음)의 Loot Table
//        필드가 비어있지 않은지. 비어있으면 DropLoot()가 아무 경고 없이 조용히 리턴합니다(일반
//        몬스터는 "드롭 없음"이 정상적인 디자인이라 원래 경고를 안 띄우는데, 보스는 거의 항상 드롭이
//        있어야 하므로 이 스크립트의 Die()에서는 비어있으면 콘솔에 경고를 띄우도록 해뒀습니다).
//     2) Loot Table 애셋 안의 entries 각각의 Item이 비어있지 않은지, Drop Chance가 0으로 되어있지
//        않은지(0이면 항상 안 나옵니다).
//     3) 각 LootItemData의 World Pickup Prefab이 연결되어 있고 그 프리팹에 LootPickup 컴포넌트가
//        붙어있는지(비어있으면 DropLoot()에서 바로 NullReferenceException이 나서 콘솔에 에러가
//        보일 겁니다 - 에러가 없다면 이 경우는 아닙니다).
//     4) 경험치/골드 오브젝트(전리품과 별개)가 안 보인다면 LootDropper의 Exp Orb Prefab/Gold Orb
//        Prefab이 비어있지 않은지도 확인하세요.
//
// [Animator 상반신 레이어 설정] (에디터 작업, 코드 아님)
//   이 보스가 휴머노이드 리그에서 상반신만 움직인다면, Swing/Wave 애니메이션을 Base Layer가
//   아니라 "상반신 전용 레이어"에서 재생하도록 Animator Controller를 구성하세요. 스크립트는
//   animator.SetTrigger("SwingAttack") 처럼 파라미터 이름으로만 트리거를 쏘기 때문에, 그 파라미터를
//   어떤 레이어가 받아서 재생하든 코드 수정 없이 그대로 동작합니다.
//   1) Animator 창 좌측 상단에서 Layers 옆 + 버튼으로 새 레이어(예: "UpperBody")를 추가합니다.
//   2) 새 레이어를 선택하고 톱니바퀴(설정) 아이콘 클릭 → Weight를 1로, Mask에는 아래에서 만들
//      Avatar Mask를 지정합니다. Blending은 보통 Override로 둡니다.
//   3) Project 창에서 우클릭 → Create → Avatar Mask (이름 예: "UpperBodyMask") 생성 후,
//      Humanoid 바디 파트 그림에서 하반신(Hips 아래 다리/발)의 체크를 끄고 상반신(Spine, Chest,
//      양팔, 머리)만 체크된 상태로 둡니다. Root Transform/IK 관련 체크는 기본값 그대로 두면 됩니다.
//   4) UpperBody 레이어 안에 Base Layer와 별도로 SwingAttack/WaveAttack용 상태(및 그 사이
//      Any State 트랜지션, 조건: 각 Trigger)를 새로 만들어 넣습니다. (Base Layer에 있던 기존
//      상태를 그대로 복사해서 옮겨도 됩니다.)
//   5) Base Layer에는 이동/Idle 애니메이션만 남겨두면, 하반신은 계속 Idle/이동 포즈를 유지하고
//      상반신만 UpperBody 레이어의 공격 모션이 겹쳐서 재생됩니다.
//   6) 만약 원본 공격 애니메이션 클립 자체가 전신을 움직이도록 만들어져 있다면, 레이어 마스크가
//      하반신 트랙을 걸러주므로 하반신 움직임은 자동으로 무시됩니다 (클립을 다시 만들 필요 없음).
// ============================================================================

using UnityEngine;

[RequireComponent(typeof(MonsterStats))]
[RequireComponent(typeof(LootDropper))]
[RequireComponent(typeof(MonsterActivation))]
public class MiddleSlimeBoss : MonoBehaviour, IDamageable
{
    private enum State { Idle, SwingAttack, WaveAttack, Dead }
    private enum AttackType { Swing, Wave }

    [Header("참조")]
    [Tooltip("비워두면 Start()에서 태그가 Player인 오브젝트를 자동으로 찾습니다.")]
    public Transform target;
    public Animator animator;
    [Tooltip("전리품/경험치·골드 오브젝트가 튀어나오는 기준 위치입니다. 비워두면 기존처럼 이 보스의 " +
              "transform.position(+lootDropHeight)을 사용하고, 지정하면 대신 이 Transform의 위치(+ " +
              "lootDropHeight)에서 튀어나옵니다 - 미리 씬에 배치해둔 빈 오브젝트를 연결하세요. " +
              "lootDropHeight를 0으로 두면 이 지점의 위치를 그대로(추가 높이 없이) 사용합니다.")]
    public Transform lootDropPoint;
    [Tooltip("공격 판정 대상 레이어 (보통 Player 레이어)")]
    public LayerMask hitMask;
    [Tooltip("Swing/Wave 공격이 적중했을 때 재생할 VFX 이름 (Resources/VFX/ 아래 프리팹 이름과 일치해야 함). " +
              "비워두면 VFX 없이 데미지만 적용됩니다. 예: \"FX_MiddleSlime_Hit\"")]
    public string hitVfxName;

    [Header("사망 (Die)")]
    [Tooltip("체력이 0이 된 뒤 Die 애니메이션을 재생하고 이 시간(초)이 지나면 오브젝트를 파괴합니다.")]
    public float dieDelay = 2f;

    [Header("탐지")]
    [Tooltip("이 범위 안에 플레이어가 있어야 공격을 시작합니다. 범위 밖이면 공격하지 않고 계속 대기합니다.")]
    public float detectRange = 10f;

    [Header("공격 사이 대기시간 (Idle)")]
    public float idleDurationMin = 1.5f;
    public float idleDurationMax = 2.5f;

    [Header("공격 - 휘두르기 (Swing)")]
    [Tooltip("범위 표시가 플레이어를 따라다니는 시간(초)")]
    public float swingFollowDuration = 1f;
    [Tooltip("범위 표시가 고정된 뒤 실제로 내려찍기까지 걸리는 시간(초)")]
    public float swingSlamDelay = 0.5f;
    public float swingRadius = 2.5f;
    [Tooltip("휘두르기 데미지 배율(%). 최종 데미지는 MonsterStats.CalculateDamage(swingDamagePercent, " +
              "맞은 플레이어의 방어력)로 계산됩니다. 예: 100이면 보스 공격력의 100%가 데미지입니다.")]
    public float swingDamagePercent = 100f;
    [Tooltip("내려찍기로 데미지가 들어갈 때, 데미지 숫자를 맞은 대상 위로 얼마나 띄울지(미터).")]
    public float swingDamageNumberHeightOffset = 1.2f;

    [Header("공격 - 파도보내기 (Wave)")]
    [Tooltip("차징(손을 뒤로 보내는) 모션 동안의 시간(초). 이 동안 부채꼴 범위 표시가 보입니다.")]
    public float waveChargeDuration = 1f;
    [Tooltip("부채꼴 범위의 전체 각도(도)")]
    public float waveFanAngle = 90f;
    public float waveFanRadius = 8f;
    [Tooltip("파도를 몇 번 발사하는지")]
    public int waveCount = 3;
    [Tooltip("파도 발사 간격(초)")]
    public float waveFireInterval = 0.5f;
    [Tooltip("파도 하나가 원점에서 waveFanRadius까지 도달하는 데 걸리는 시간(초)")]
    public float waveTravelDuration = 1f;
    [Tooltip("파도보내기 데미지 배율(%). 최종 데미지는 MonsterStats.CalculateDamage(waveDamagePercent, " +
              "맞은 플레이어의 방어력)로 계산됩니다.")]
    public float waveDamagePercent = 75f;

    [Header("높이 조절")]
    [Tooltip("휘두르기(Swing) 범위 표시 및 내려찍기 판정 위치의 높이 오프셋. 보스 위치 기준 위(+)/아래(-)로 조절됩니다. 모델 크기나 원하는 타격 지점에 맞춰 조정하세요.")]
    public float swingHeight = 0f;
    [Tooltip("파도보내기(Wave) 범위 표시 및 파도가 생성되는 기준 높이 오프셋.")]
    public float waveHeight = 0f;
    [Tooltip("파도가 Normal Graph처럼 솟아올랐다 내려가는 최대 높이. 0으로 하면 땅에 붙어서 이동합니다.")]
    public float waveMaxHeight = 1.2f;
    [Tooltip("전리품(LootDropper)이 튀어나오기 시작하는 높이 오프셋 - 보스 위치(transform.position) " +
              "기준 위(+)로 얼마나 띄운 지점에서 팝 애니메이션이 시작될지입니다. Awake()에서 이 값을 " +
              "LootDropper.dropHeight에 그대로 넣어주므로, 다른 몬스터와 별도로 이 보스만 따로 큰 값을 " +
              "쓰고 싶을 때(모델이 커서 땅에서 튀어나오는 것처럼 보이지 않게 하고 싶을 때) 여기서만 " +
              "조절하면 됩니다 - LootDropper 컴포넌트를 따로 펼쳐볼 필요 없습니다.")]
    public float lootDropHeight = 1f;

    private State currentState;
    private AttackType lastAttack = AttackType.Wave; // 처음엔 반대 패턴(Swing)부터 나가도록 초기값을 Wave로 설정
    private float stateTimer;
    private MonsterStats stats;
    private LootDropper lootDropper;

    private CircleAreaIndicator swingIndicator;
    private bool swingLocked;
    private Vector3 swingLockedPosition;

    private FanAreaIndicator waveIndicator;
    private Vector3 waveOrigin;
    private Vector3 waveForward;
    private bool waveThrustStarted;
    private int wavesFired;
    private float waveFireTimer;

    private static readonly int SwingAttackParam = Animator.StringToHash("SwingAttack");
    private static readonly int WaveAttackParam = Animator.StringToHash("WaveAttack");
    private static readonly int DieParam = Animator.StringToHash("Die");

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        stats = GetComponent<MonsterStats>();
        lootDropper = GetComponent<LootDropper>();

        // 전리품 드롭 높이를 이 스크립트의 lootDropHeight 하나로 관리합니다 - LootDropper 컴포넌트를
        // 따로 펼쳐서 Drop Height를 맞출 필요 없이, 다른 높이 조절 필드들처럼 여기서 바로 조절하세요.
        lootDropper.dropHeight = lootDropHeight;
    }

    private void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }

        EnterIdle();
    }

    private void Update()
    {
        // 사망 상태에서는 그 어떤 로직도 실행하지 않습니다. Die 트리거와 파괴 예약은 Die()에서
        // 이미 처리했습니다.
        if (currentState == State.Dead) return;

        switch (currentState)
        {
            case State.Idle:
                UpdateIdleState();
                break;
            case State.SwingAttack:
                UpdateSwingAttack();
                break;
            case State.WaveAttack:
                UpdateWaveAttack();
                break;
        }
    }

    // ------------------------------------------------------------------
    // Idle - 다음 공격 결정
    // ------------------------------------------------------------------

    private void EnterIdle()
    {
        currentState = State.Idle;
        stateTimer = Random.Range(idleDurationMin, idleDurationMax);
    }

    private void UpdateIdleState()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer > 0f) return;
        if (target == null) return; // 타겟이 없으면 계속 대기
        if (!IsTargetInDetectRange()) return; // 탐지범위 밖이면 공격하지 않고 계속 대기

        // 직전과 다른 패턴이 나오도록 기본은 번갈아 나가게 했습니다. 완전 무작위를 원하면
        // 아래 두 줄을 (AttackType)Random.Range(0, 2) 로 바꾸세요.
        AttackType next = (lastAttack == AttackType.Swing) ? AttackType.Wave : AttackType.Swing;
        lastAttack = next;

        if (next == AttackType.Swing) EnterSwingAttack();
        else EnterWaveAttack();
    }

    // ------------------------------------------------------------------
    // 휘두르기 (Swing)
    // ------------------------------------------------------------------

    private void EnterSwingAttack()
    {
        currentState = State.SwingAttack;
        stateTimer = 0f;
        swingLocked = false;

        GameObject go = new GameObject("SwingIndicator");
        swingIndicator = go.AddComponent<CircleAreaIndicator>();
        swingIndicator.SetRadius(swingRadius);
        swingIndicator.PlaceAt(GroundPosition(target.position));
        swingIndicator.Follow(target);

        if (animator != null) animator.SetTrigger(SwingAttackParam);
    }

    private void UpdateSwingAttack()
    {
        stateTimer += Time.deltaTime;

        if (!swingLocked)
        {
            if (stateTimer >= swingFollowDuration)
            {
                swingLocked = true;
                swingIndicator.Lock();
                swingLockedPosition = swingIndicator.transform.position;
            }
            return;
        }

        if (stateTimer >= swingFollowDuration + swingSlamDelay)
        {
            PerformSwingSlam();
        }
    }

    private void PerformSwingSlam()
    {
        Collider[] hits = Physics.OverlapSphere(swingLockedPosition, swingRadius, hitMask);
        foreach (Collider col in hits)
        {
            IDamageable damageable = col.GetComponentInParent<IDamageable>();
            if (damageable == null) continue;

            PlayerStats playerStats = col.GetComponentInParent<PlayerStats>();
            float targetDefense = playerStats != null ? playerStats.TotalDefense : 0f;

            DamageResult result = stats.CalculateDamage(swingDamagePercent, targetDefense);
            damageable.TakeDamage(result.damage);

            Vector3 hitPoint = col.ClosestPoint(swingLockedPosition);

            if (!string.IsNullOrEmpty(hitVfxName))
            {
                VFXManager.Instance.Play(hitVfxName, hitPoint, transform.rotation);
            }

            Vector3 numberPosition = hitPoint + Vector3.up * swingDamageNumberHeightOffset;
            DamageNumberManager.Instance.Show(result.damage, numberPosition, false, DamageNumberTeam.Player);
        }

        DestroySwingIndicator();
        EnterIdle();
    }

    private void DestroySwingIndicator()
    {
        if (swingIndicator != null) Destroy(swingIndicator.gameObject);
        swingIndicator = null;
    }

    // ------------------------------------------------------------------
    // 파도보내기 (Wave)
    // ------------------------------------------------------------------

    private void EnterWaveAttack()
    {
        currentState = State.WaveAttack;
        stateTimer = 0f;
        waveThrustStarted = false;
        wavesFired = 0;

        waveForward = Flatten(target.position - transform.position);
        if (waveForward.sqrMagnitude < 0.0001f) waveForward = transform.forward;
        waveForward.Normalize();

        waveOrigin = transform.position + Vector3.up * waveHeight;

        GameObject go = new GameObject("WaveIndicator");
        go.transform.position = waveOrigin;
        go.transform.rotation = Quaternion.LookRotation(waveForward);
        waveIndicator = go.AddComponent<FanAreaIndicator>();
        waveIndicator.Build(waveFanAngle * 0.5f, waveFanRadius);

        if (animator != null) animator.SetTrigger(WaveAttackParam);
    }

    private void UpdateWaveAttack()
    {
        stateTimer += Time.deltaTime;

        if (stateTimer < waveChargeDuration) return;

        if (!waveThrustStarted)
        {
            waveThrustStarted = true;
            waveFireTimer = 0f; // 손을 뻗는 순간 첫 파도가 바로 나가도록
        }

        waveFireTimer -= Time.deltaTime;
        if (wavesFired < waveCount && waveFireTimer <= 0f)
        {
            FireWave();
            wavesFired++;
            waveFireTimer = waveFireInterval;
        }

        if (wavesFired >= waveCount)
        {
            DestroyWaveIndicator();
            EnterIdle();
        }
    }

    private void FireWave()
    {
        GameObject go = new GameObject("ShockwaveWave");
        ShockwaveWave wave = go.AddComponent<ShockwaveWave>();
        wave.damagePercent = waveDamagePercent;
        wave.sourceStats = stats;
        wave.hitMask = hitMask;
        wave.maxHeight = waveMaxHeight;
        wave.hitVfxName = hitVfxName;
        wave.Launch(waveOrigin, waveForward, waveFanAngle * 0.5f, waveFanRadius, waveTravelDuration);
    }

    private void DestroyWaveIndicator()
    {
        if (waveIndicator != null) Destroy(waveIndicator.gameObject);
        waveIndicator = null;
    }

    // ------------------------------------------------------------------
    // 피격
    // ------------------------------------------------------------------

    /// <summary>플레이어의 공격 등에서 호출하세요. MiddleSlime은 Hit(피격) 모션이 없으므로 애니메이터
    /// 트리거 없이 데미지만 적용합니다. 진행 중이던 공격(상태/범위 표시)도 끊지 않습니다. 다만 체력이
    /// 0이 되면 즉시 Die()로 넘어가 사망 처리합니다. 이미 죽은 상태면 무시합니다.</summary>
    public void TakeDamage(float amount)
    {
        if (currentState == State.Dead) return;

        stats.TakeDamage(amount);

        if (stats.CurrentHP <= 0f)
        {
            Die();
        }
    }

    /// <summary>체력이 0이 되면 호출됩니다. 진행 중이던 Swing/Wave 범위 표시를 즉시 정리하고 Die
    /// 애니메이션을 재생한 뒤, dieDelay초 후 오브젝트를 파괴합니다.</summary>
    private void Die()
    {
        currentState = State.Dead;

        DestroySwingIndicator();
        DestroyWaveIndicator();
        DisableColliders(); // 죽은 직후 콜라이더를 꺼서, dieDelay 동안 시체가 남아있는 사이 또 공격 판정에
                            // 맞아 데미지/이펙트/데미지 숫자가 중복으로 발생하지 않게 합니다.

        // 일반 몬스터는 "드롭할 전리품 없음"이 정상적인 설정이라 LootDropper가 조용히 넘어가지만,
        // 보스는 거의 항상 전리품이 있어야 하므로 Loot Table을 깜빡 연결 안 한 경우를 바로 알 수 있도록
        // 여기서만 따로 경고를 띄웁니다.
        if (lootDropper.lootTable == null)
        {
            Debug.LogWarning("[MiddleSlimeBoss] LootDropper의 Loot Table이 비어있어 전리품이 드롭되지 않습니다. " +
                              "이 오브젝트의 LootDropper 컴포넌트에서 Loot Table 필드에 애셋을 연결해주세요.", this);
        }

        // lootDropPoint를 지정해뒀으면 그 위치를, 아니면 기존처럼 이 보스의 위치를 기준으로 흩뿌립니다.
        Vector3 lootOrigin = lootDropPoint != null ? lootDropPoint.position : transform.position;
        lootDropper.DropLoot(lootOrigin); // 지정한(또는 죽은) 위치를 기준으로 전리품을 흩뿌립니다 (보스 전용 Loot Table을 연결해두면 됩니다).
        lootDropper.DropRewards(lootOrigin); // 경험치/골드 오브젝트를 흩뿌립니다 (자동으로 플레이어에게 흡수됩니다).

        if (animator != null) animator.SetTrigger(DieParam);

        Destroy(gameObject, dieDelay);
    }

    /// <summary>자신(과 자식 오브젝트) 위의 모든 Collider를 꺼서 더 이상 공격 판정에 걸리지 않도록 합니다.</summary>
    private void DisableColliders()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }
    }

    // ------------------------------------------------------------------
    // 유틸
    // ------------------------------------------------------------------

    /// <summary>target이 detectRange 안에 있는지 (수평 거리 기준) 확인합니다.</summary>
    private bool IsTargetInDetectRange()
    {
        if (target == null) return false;
        float dist = Vector3.Distance(Flatten(transform.position), Flatten(target.position));
        return dist <= detectRange;
    }

    private static Vector3 Flatten(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    private Vector3 GroundPosition(Vector3 worldPos)
    {
        worldPos.y = transform.position.y + swingHeight; // 보스 바닥 높이 + swingHeight 오프셋.
        return worldPos;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, swingRadius);
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, waveFanRadius);
    }
}