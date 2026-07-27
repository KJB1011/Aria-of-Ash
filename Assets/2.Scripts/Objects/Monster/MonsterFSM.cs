// ============================================================================
// MonsterFSM.cs
// ----------------------------------------------------------------------------
// 근접/원거리 공격을 가진 몬스터 공용 FSM 베이스 클래스.
// Idle(스폰 지점 배회) → Trace(탐색) → Chase(추격) / BodyAttack(근접) / SplashAttack(원거리) / Hit / Return(복귀) / Die(사망)
// 이동은 NavMeshAgent 기반입니다.
//
// [사용 방법]
//   이 클래스를 직접 붙이지 말고, 몬스터별로 상속해서 사용하세요.
//   예) public class SlimeFSM : MonsterFSM { ... }
//   기본 동작(상태 전이, 타이머, 배회/복귀, 조준 등)은 이 클래스가 전부 처리하고,
//   몬스터마다 달라지는 부분(공격 판정, 투사체 생성 등)만 아래 protected virtual 훅을
//   자식 클래스에서 override 하면 됩니다.
//     - OnBodyAttackTrigger()   : 근접 공격 애니메이션 트리거 직후 호출 (데미지 판정 등)
//     - OnSplashAttackTrigger() : 원거리 공격 애니메이션 트리거 직후 호출 (투사체 생성 등)
//     - OnHitTrigger()          : 피격 애니메이션 트리거 직후 호출 (피격 이펙트 등)
//     - OnDieTrigger()          : 사망 애니메이션 트리거 직후 호출 (드롭 아이템, 사망 이펙트 등)
//   근접/원거리 공격이 아예 없는 몬스터는 hasMeleeAttack / hasRangedAttack을 false로 두면
//   해당 사거리 체크 자체를 건너뜁니다.
//
// [씬 준비]
//   1) Window > AI > Navigation 에서 바닥에 NavMesh를 미리 Bake 해두세요.
//   2) 몬스터 오브젝트에 이 클래스를 상속한 스크립트(예: SlimeFSM)를 붙입니다.
//      NavMeshAgent는 자동으로 같이 추가됩니다. 오브젝트가 처음 배치된 위치가 스폰 지점이 됩니다.
//   3) Target을 비워두면 Start()에서 태그가 "Player"인 오브젝트를 자동으로 찾습니다.
//   4) Animator 파라미터: IsMove(Bool), BodyAttack(Trigger), SplashAttack(Trigger), Hit(Trigger), Die(Trigger)
//      * IDLE/WALK는 IsMove Bool로, 나머지는 Trigger로 전환하도록 Animator Controller를 구성하세요.
//
// [FSM 흐름 요약]
//   Idle (스폰 지점 배회)
//     ├ 적이 detectRange 안에 들어오면 → Trace(탐색)
//     └ 평소엔 스폰 지점 기준 wanderBoxSize 크기의 상자 범위 안을 랜덤하게 돌아다님
//   Trace (탐색 - 진입 즉시 조건에 따라 갈라지는 "판단" 상태)
//     ├ meleeRange 안이면(hasMeleeAttack) → BodyAttack
//     ├ rangedRange 안이고 원거리 쿨타임이 끝났으면(hasRangedAttack) → SplashAttack
//     └ 그 외(사거리 밖, detectRange 안) → Chase
//     * 전투에 처음 진입하는 순간(=Idle→Trace) 원거리 공격은 강제로 쿨타임(rangedCooldown)에 들어갑니다.
//   Chase (추격) → 매 프레임 위 조건을 재검사, 적이 detectRange를 벗어나면 → Return
//   BodyAttack (근접) → 진입 즉시 타겟 쪽으로 정면 스냅(SplashAttack과 동일), 공격 후 meleePostDelay
//     후딜레이, 끝나면 판단 로직 재실행
//   SplashAttack (원거리) → splashAttackStateDuration 재생, 끝나면 판단 로직 재실행 (조준은 aimTurnSpeed로 계속 보정)
//   Hit (피격) → TakeHit() 호출 시 무조건 진입, hitStunDuration 후 판단 로직 재실행
//   Return (복귀) → returnMoveSpeed(빠른 속도)로 스폰 지점까지 이동, 도착하면 → Idle
//     * 복귀 도중 적이 다시 detectRange 안에 들어오면 전투를 재개합니다(Trace로 전환).
//   Die (사망) → TakeDamage()로 체력이 0이 되면 무조건 진입. Die 트리거를 쏘고 이동/충돌 등을 모두 멈춘 뒤
//     dieDelay초 후 오브젝트를 파괴합니다. 이 상태에 들어가면 더 이상 어떤 로직도 실행되지 않습니다
//     (Update() 맨 위에서 곧바로 리턴). 진입하는 즉시 자신의 모든 Collider를 꺼서(DisableColliders()),
//     dieDelay 동안 시체가 씬에 남아있는 사이에도 더 이상 공격 판정에 걸리지 않습니다.
//
// [리쉬(leash) - 스폰에서 너무 멀어졌을 때]
//   상태와 무관하게(Return/Hit/Die 제외) 매 프레임 스폰 지점과의 거리를 검사해서
//   maxLeashDistance를 넘으면 즉시 Return으로 전환합니다.
//
// [최적화 - 아주 멀리 있을 때 오브젝트 자체를 비활성화]
//   detectRange(전투 감지)와는 완전히 별개로, [RequireComponent]로 자동으로 붙는 MonsterActivation이
//   MonsterActivationManager에 등록되어 플레이어와의 거리가 activationRange(훨씬 넓은 값, 기본 50m)를
//   넘으면 이 몬스터 오브젝트 자체가 SetActive(false)로 꺼집니다 - 이 스크립트의 Update()를 포함해
//   전부 멈추므로 필드에 몬스터가 많을 때 성능에 도움이 됩니다. 자세한 내용은
//   MonsterActivationManager.cs를 참고하세요.
//
// [체력 - MonsterStats]
//   체력(HP)은 이제 이 스크립트가 아니라 같이 붙는 MonsterStats 컴포넌트가 갖고 있습니다
//   ([RequireComponent]로 자동 추가됩니다). Max Health를 조절하려면 MonsterStats의 Base HP를
//   바꾸세요. TakeDamage()는 그대로 이 스크립트에서 받아서(IDamageable), MonsterStats에는
//   수치 반영만 위임하고 Hit/Die 상태 전환은 여기서 그대로 판단합니다.
//
// [근접 공격 판정 - MonsterAttackArea]
//   Player와 완전히 동일한 구조를 씁니다. 몬스터 아래 "MonsterAttackArea"(태그: MonsterAttackArea)
//   오브젝트를 만들고, 그 아래에 BodyAttack 등 모션 이름을 딴 자식 오브젝트(BoxCollider +
//   MonsterAttackHitbox)로 판정 범위를 만들어두면, 근접 공격 애니메이션 클립에 건 Animation
//   Event(OnHitboxOpen/OnHitboxClose)가 그 이름의 히트박스만 열고 닫아줍니다. 데미지/VFX/데미지
//   숫자는 각 MonsterAttackHitbox가 자체적으로 처리하므로 이 FSM 스크립트는 관여하지 않습니다.
//   attackArea 필드는 비워두면 Awake()에서 자식의 MonsterAttackAreaController를 자동으로 찾고,
//   Hit/Die 등으로 BodyAttack이 중간에 끊기면 안전장치로 자동으로 CloseAllHitboxes()를 호출해서
//   판정이 열린 채로 남아있지 않게 합니다. 자세한 설정 방법은 MonsterAttackAreaController.cs와
//   MonsterAttackHitbox.cs, (Animator가 다른 오브젝트에 있다면) MonsterAnimationEventRelay.cs를
//   참고하세요.
// ============================================================================

using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(MonsterStats))]
[RequireComponent(typeof(LootDropper))]
[RequireComponent(typeof(MonsterActivation))]
public abstract class MonsterFSM : MonoBehaviour, IDamageable
{
    public enum State
    {
        Idle,
        Trace,
        Chase,
        BodyAttack,
        SplashAttack,
        Hit,
        Return,
        Die
    }

    [Header("참조")]
    [Tooltip("비워두면 Start()에서 태그가 Player인 오브젝트를 자동으로 찾습니다.")]
    public Transform target;
    public Animator animator;
    [Tooltip("비워두면 Awake()에서 하위(자식)에 있는 MonsterAttackAreaController를 자동으로 찾습니다. " +
              "BodyAttack이 Hit/Die 등으로 강제 종료될 때 열려있는 근접 공격 판정을 확실히 닫아주는 데 사용합니다.")]
    public MonsterAttackAreaController attackArea;

    [Header("보유 공격 종류")]
    [Tooltip("근접 공격이 없는 몬스터라면 꺼두세요. (meleeRange 체크 자체를 건너뜁니다)")]
    public bool hasMeleeAttack = true;
    [Tooltip("원거리 공격이 없는 몬스터라면 꺼두세요. (rangedRange 체크 자체를 건너뜁니다)")]
    public bool hasRangedAttack = true;

    [Header("탐지 / 사거리")]
    public float detectRange = 8f;
    public float meleeRange = 1.5f;
    public float rangedRange = 6f;

    [Header("이동")]
    public float moveSpeed = 2.5f;

    [Header("스폰 지점 배회 (Idle)")]
    [Tooltip("스폰 지점을 중심으로 배회할 정사각형 범위의 한 변 길이. 기본 2 = 2x2 범위")]
    public float wanderBoxSize = 2f;
    [Tooltip("배회 중 한 지점에 도착해서 다음 지점으로 출발하기 전까지 대기하는 시간 범위(초)")]
    public float wanderWaitMin = 1.5f;
    public float wanderWaitMax = 3.5f;

    [Header("복귀 (Return)")]
    [Tooltip("타겟을 놓쳤을 때, 혹은 스폰 지점에서 너무 멀어졌을 때 스폰 지점으로 돌아오는 속도. 평소 이동속도보다 빠르게 설정하세요.")]
    public float returnMoveSpeed = 6f;
    [Tooltip("스폰 지점으로부터 이 거리 이상 벗어나면(전투 중이든 배회 중이든) 강제로 복귀합니다.")]
    public float maxLeashDistance = 10f;

    [Header("공격 - 근접 (BodyAttack)")]
    [Tooltip("공격 1회 사용 후, 다시 공격할 수 있을 때까지의 후딜레이(초)")]
    public float meleePostDelay = 1.5f;

    [Header("공격 - 원거리 (SplashAttack)")]
    [Tooltip("원거리 공격 재사용 대기시간(초). 전투 시작(Idle→Trace) 시점에 강제로 이 쿨타임에 들어갑니다.")]
    public float rangedCooldown = 8f;
    [Tooltip("SplashAttack 모션 재생 중 다른 행동을 못하는 시간(초). 재사용 대기시간(rangedCooldown)과는 별개입니다.")]
    public float splashAttackStateDuration = 1f;
    [Tooltip("SplashAttack 도중 플레이어를 계속 바라보도록 보정하는 회전 속도(도/초). 곡사 투사체 조준 정확도에 영향을 줍니다.")]
    public float aimTurnSpeed = 720f;

    [Header("피격 (Hit)")]
    [Tooltip("Hit 상태에서 다시 행동을 재개하기까지 걸리는 시간(초)")]
    public float hitStunDuration = 0.5f;

    [Header("사망 (Die)")]
    [Tooltip("체력이 0이 된 뒤 Die 애니메이션을 재생하고 이 시간(초)이 지나면 오브젝트를 파괴합니다.")]
    public float dieDelay = 2f;

    public State CurrentState { get; protected set; }

    protected NavMeshAgent agent;
    protected MonsterStats stats;
    protected LootDropper lootDropper;
    protected Vector3 spawnPosition;

    protected float meleeDelayTimer;     // 근접 공격 재사용 락(전역으로 계속 감소)
    protected float rangedCooldownTimer; // 원거리 공격 재사용 락(전역으로 계속 감소)
    protected float splashAttackTimer;   // SplashAttack 상태 자체의 지속 시간
    protected float hitTimer;            // Hit 상태 자체의 지속 시간
    protected bool combatStarted;        // Idle -> Trace 최초 진입 여부 (원거리 강제 쿨타임용)

    protected bool wanderMoving;         // 배회 중 현재 이동 중인지(false면 대기 중)
    protected float wanderTimer;         // 배회 대기 시간 카운트다운

    protected static readonly int IsMoveParam = Animator.StringToHash("IsMove");
    protected static readonly int BodyAttackParam = Animator.StringToHash("BodyAttack");
    protected static readonly int SplashAttackParam = Animator.StringToHash("SplashAttack");
    protected static readonly int HitParam = Animator.StringToHash("Hit");
    protected static readonly int DieParam = Animator.StringToHash("Die");

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        stats = GetComponent<MonsterStats>();
        lootDropper = GetComponent<LootDropper>();

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        if (attackArea == null)
        {
            attackArea = GetComponentInChildren<MonsterAttackAreaController>(true);
        }
    }

    protected virtual void Start()
    {
        spawnPosition = transform.position;

        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
        }

        // 스폰 위치가 NavMesh 위에 없으면 SetDestination이 계속 실패해서 "아무것도 안 하고 멈춰있는"
        // 것처럼 보입니다. 콘솔에 바로 원인이 드러나도록 시작 시점에 한 번 확인합니다.
        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning($"[{name}] NavMeshAgent가 NavMesh 위에 있지 않습니다. " +
                              "스폰 위치가 베이크된 NavMesh에서 너무 멀리 떨어져 있거나, " +
                              "이 몬스터의 NavMeshAgent Radius/Agent Type이 베이크 설정과 맞지 않는지 확인하세요.", this);
        }

        agent.speed = moveSpeed;
        ChangeState(State.Idle);
    }

    protected virtual void Update()
    {
        // 사망 상태에서는 그 어떤 로직도 실행하지 않습니다 (리쉬 체크, 이동, 판단 로직 전부 정지).
        // Die 트리거와 파괴 예약은 ChangeState(State.Die)에서 이미 처리했습니다.
        if (CurrentState == State.Die) return;

        TickGlobalTimers();

        // 스폰 지점에서 너무 멀어지면 강제로 복귀합니다.
        // 단, 공격/피격/이미 복귀 중인 상태는 제외합니다. 그렇지 않으면 공격 애니메이션(정지 상태)
        // 재생 도중에도 리쉬 체크가 끼어들어 갑자기 Return으로 끊고 움직이기 시작하는 버그가 생깁니다.
        bool isActionLocked = CurrentState == State.Return
            || CurrentState == State.Hit
            || CurrentState == State.BodyAttack
            || CurrentState == State.SplashAttack;

        if (!isActionLocked)
        {
            float distFromSpawn = Vector3.Distance(transform.position, spawnPosition);
            if (distFromSpawn > maxLeashDistance)
            {
                ChangeState(State.Return);
            }
        }

        switch (CurrentState)
        {
            case State.Idle:
                UpdateIdle();
                break;
            case State.Trace:
                // Trace는 ChangeState에서 진입 즉시 다음 상태로 넘어가므로 보통 여기 도달하지 않습니다.
                EvaluateCombatState();
                break;
            case State.Chase:
                EvaluateCombatState();
                break;
            case State.BodyAttack:
                UpdateBodyAttack();
                break;
            case State.SplashAttack:
                UpdateSplashAttack();
                break;
            case State.Hit:
                UpdateHit();
                break;
            case State.Return:
                UpdateReturn();
                break;
        }
    }

    /// <summary>
    /// agent.isStopped = true만으로는 감속(Acceleration)이 덜 끝난 상태의 관성이나 다른 에이전트와의
    /// Obstacle Avoidance 때문에 몇 프레임 더 미끄러질 수 있습니다. velocity까지 같이 0으로 만들어서
    /// 공격/피격/대기 중에 확실히 그 자리에 멈추도록 합니다.
    /// </summary>
    protected void StopAgent()
    {
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
    }

    protected virtual void TickGlobalTimers()
    {
        if (meleeDelayTimer > 0f) meleeDelayTimer -= Time.deltaTime;
        if (rangedCooldownTimer > 0f) rangedCooldownTimer -= Time.deltaTime;
    }

    // ------------------------------------------------------------------
    // 상태별 Update
    // ------------------------------------------------------------------

    protected virtual void UpdateIdle()
    {
        if (target != null)
        {
            float distance = Vector3.Distance(transform.position, target.position);
            if (distance <= detectRange)
            {
                ChangeState(State.Trace);
                return;
            }
        }

        UpdateWander();
    }

    /// <summary>스폰 지점 주변 작은 상자 범위를 랜덤하게 배회합니다.</summary>
    protected virtual void UpdateWander()
    {
        if (wanderMoving)
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                wanderMoving = false;
                wanderTimer = Random.Range(wanderWaitMin, wanderWaitMax);
                StopAgent();
                if (animator != null) animator.SetBool(IsMoveParam, false);
            }
        }
        else
        {
            wanderTimer -= Time.deltaTime;
            if (wanderTimer <= 0f)
            {
                Vector3 point = GetRandomWanderPoint();
                agent.speed = moveSpeed;
                agent.isStopped = false;
                agent.SetDestination(point);
                wanderMoving = true;
                if (animator != null) animator.SetBool(IsMoveParam, true);
            }
        }
    }

    protected virtual Vector3 GetRandomWanderPoint()
    {
        float half = wanderBoxSize * 0.5f;
        Vector3 randomOffset = new Vector3(Random.Range(-half, half), 0f, Random.Range(-half, half));
        Vector3 point = spawnPosition + randomOffset;

        if (NavMesh.SamplePosition(point, out NavMeshHit hit, half + 0.5f, NavMesh.AllAreas))
        {
            return hit.position;
        }
        return spawnPosition;
    }

    protected virtual void UpdateBodyAttack()
    {
        if (meleeDelayTimer <= 0f)
        {
            EvaluateCombatState();
        }
    }

    protected virtual void UpdateSplashAttack()
    {
        // 곡사 투사체를 던지는 동안 플레이어가 움직여도 계속 조준하도록 매 프레임 보정합니다.
        if (target != null)
        {
            FaceTarget(target.position, aimTurnSpeed);
        }

        splashAttackTimer -= Time.deltaTime;
        if (splashAttackTimer <= 0f)
        {
            EvaluateCombatState();
        }
    }

    /// <summary>lookPosition 방향으로 수평 회전만 적용합니다. turnSpeedDegPerSec가 0 이하면 즉시 스냅합니다.</summary>
    protected void FaceTarget(Vector3 lookPosition, float turnSpeedDegPerSec)
    {
        Vector3 direction = lookPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = turnSpeedDegPerSec <= 0f
            ? targetRotation
            : Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeedDegPerSec * Time.deltaTime);
    }

    protected virtual void UpdateHit()
    {
        hitTimer -= Time.deltaTime;
        if (hitTimer <= 0f)
        {
            EvaluateCombatState();
        }
    }

    protected virtual void UpdateReturn()
    {
        // 복귀 도중 타겟이 다시 탐지되면 전투를 재개합니다.
        if (target != null)
        {
            float distance = Vector3.Distance(transform.position, target.position);
            if (distance <= detectRange)
            {
                ChangeState(State.Trace);
                return;
            }
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            ChangeState(State.Idle);
        }
    }

    /// <summary>
    /// Trace 진입 시, 그리고 Chase 중 매 프레임, 그리고 각 공격/피격이 끝난 직후 호출되는
    /// 중앙 판단 로직입니다. 거리와 쿨타임을 보고 다음 행동(공격/추격/복귀)을 결정합니다.
    /// </summary>
    protected virtual void EvaluateCombatState()
    {
        if (target == null)
        {
            ChangeState(State.Return);
            return;
        }

        float distance = Vector3.Distance(transform.position, target.position);

        if (distance > detectRange)
        {
            ChangeState(State.Return);
            return;
        }

        if (hasMeleeAttack && distance <= meleeRange && meleeDelayTimer <= 0f)
        {
            ChangeState(State.BodyAttack);
            return;
        }

        if (hasRangedAttack && distance <= rangedRange && rangedCooldownTimer <= 0f)
        {
            ChangeState(State.SplashAttack);
            return;
        }

        // 공격 조건을 못 채우면 추격 상태를 유지/진입합니다.
        if (CurrentState != State.Chase)
        {
            ChangeState(State.Chase);
        }

        // 이미 근접 사거리 안인데 후딜레이나 쿨타임 때문에 공격을 못 쓰는 경우엔
        // 제자리에서 대기하고, 그 외에는 계속 쫓아갑니다.
        bool shouldMove = !(hasMeleeAttack && distance <= meleeRange);
        agent.speed = moveSpeed;
        if (shouldMove)
        {
            agent.isStopped = false;
            agent.SetDestination(target.position);
        }
        else
        {
            StopAgent();
        }
        if (animator != null)
        {
            animator.SetBool(IsMoveParam, shouldMove);
        }
    }

    // ------------------------------------------------------------------
    // 상태 전환
    // ------------------------------------------------------------------

    [Header("디버그")]
    [Tooltip("켜두면 상태가 바뀔 때마다 콘솔에 로그를 남깁니다. '왜 이 행동을 하는지' 추적할 때 켜보세요.")]
    public bool debugLogStateChanges = false;

    protected virtual void ChangeState(State newState)
    {
        if (debugLogStateChanges && newState != State.Trace)
        {
            Debug.Log($"[{name}] {CurrentState} → {newState} (target={(target != null ? target.name : "null")})", this);
        }

        // BodyAttack 도중 다른 상태로 강제 전환되면(피격 스턴, 사망 등), Animation Event로 닫혔어야 할
        // 히트박스가 못 닫힌 채 남아있을 수 있습니다 - 안전장치로 확실히 닫아줍니다.
        if (CurrentState == State.BodyAttack && newState != State.BodyAttack && attackArea != null)
        {
            attackArea.CloseAllHitboxes();
        }

        CurrentState = newState;

        switch (newState)
        {
            case State.Idle:
                agent.speed = moveSpeed;
                wanderMoving = false;
                wanderTimer = Random.Range(wanderWaitMin, wanderWaitMax);
                // 완전히 비전투 상태로 돌아왔으니, 다음에 다시 전투가 시작될 때 원거리 공격이
                // 또 강제로 쿨타임에 들어가도록 리셋합니다. (그렇지 않으면 이 몬스터가 살아있는 동안
                // 딱 한 번, 최초 조우 때만 강제 쿨타임이 걸리고 그 다음부터는 적용되지 않습니다)
                combatStarted = false;
                StopAgent();
                if (animator != null) animator.SetBool(IsMoveParam, false);
                break;

            case State.Trace:
                // 전투에 처음 진입하는 순간(=Idle에서 벗어나는 순간) 원거리 공격을 강제로 쿨타임에 넣습니다.
                if (!combatStarted)
                {
                    combatStarted = true;
                    rangedCooldownTimer = rangedCooldown;
                }
                EvaluateCombatState();
                break;

            case State.Chase:
                agent.speed = moveSpeed;
                agent.isStopped = false;
                if (animator != null) animator.SetBool(IsMoveParam, true);
                break;

            case State.BodyAttack:
                StopAgent();
                meleeDelayTimer = meleePostDelay;
                // SplashAttack과 마찬가지로 공격을 시작하는 순간 즉시 타겟 쪽으로 몸을 정면으로 돌립니다 -
                // 근접 판정 범위가 좁은 몬스터일수록 이 스냅이 없으면 타겟이 살짝 옆에 있을 때 히트박스가
                // 빗나가기 쉽습니다.
                if (target != null)
                {
                    FaceTarget(target.position, 0f);
                }
                if (animator != null)
                {
                    animator.SetBool(IsMoveParam, false);
                    animator.SetTrigger(BodyAttackParam);
                }
                OnBodyAttackTrigger();
                break;

            case State.SplashAttack:
                StopAgent();
                rangedCooldownTimer = rangedCooldown;
                splashAttackTimer = splashAttackStateDuration;
                // 공격을 시작하는 순간 즉시 타겟 쪽으로 몸을 돌립니다. (애니메이션 트리거와 동시에 정확한 방향을 보고 있어야 함)
                if (target != null)
                {
                    FaceTarget(target.position, 0f);
                }
                if (animator != null)
                {
                    animator.SetBool(IsMoveParam, false);
                    animator.SetTrigger(SplashAttackParam);
                }
                OnSplashAttackTrigger();
                break;

            case State.Hit:
                StopAgent();
                hitTimer = hitStunDuration;
                if (animator != null)
                {
                    animator.SetTrigger(HitParam);
                }
                OnHitTrigger();
                break;

            case State.Return:
                agent.speed = returnMoveSpeed;
                agent.isStopped = false;
                agent.SetDestination(spawnPosition);
                if (animator != null) animator.SetBool(IsMoveParam, true);
                break;

            case State.Die:
                StopAgent();
                agent.enabled = false; // 죽은 채로 더 이상 길찾기/이동을 하지 않도록 완전히 꺼둡니다.
                DisableColliders(); // 죽은 직후부터는 콜라이더를 꺼서, dieDelay 동안 시체가 남아있는 사이에
                                    // 또 공격 판정에 맞아 데미지/이펙트/데미지 숫자가 중복으로 발생하지 않게 합니다.
                lootDropper.DropLoot(); // 죽은 위치를 기준으로 전리품을 흩뿌립니다 (Loot Table이 비어있으면 아무것도 드롭하지 않습니다).
                lootDropper.DropRewards(); // 경험치/골드 오브젝트를 흩뿌립니다 (자동으로 플레이어에게 흡수됩니다).
                QuestManager.Instance?.ReportKill(stats.monsterId); // 진행 중인 퀘스트 중 이 몬스터를 목표로 하는 Kill 목표가 있으면 카운트를 올립니다. QuestManager가 없는 테스트 씬에서도 안전합니다.
                if (animator != null)
                {
                    animator.SetBool(IsMoveParam, false);
                    animator.SetTrigger(DieParam);
                }
                OnDieTrigger();
                Destroy(gameObject, dieDelay);
                break;
        }
    }

    // ------------------------------------------------------------------
    // 몬스터별로 달라지는 부분 - 자식 클래스에서 override 하세요.
    // ------------------------------------------------------------------

    /// <summary>근접 공격 애니메이션 트리거 직후 호출됩니다. 데미지 판정, 이펙트 등을 여기서 처리하세요.</summary>
    protected virtual void OnBodyAttackTrigger() { }

    /// <summary>원거리 공격 애니메이션 트리거 직후 호출됩니다. 투사체 생성 등을 여기서 처리하세요.</summary>
    protected virtual void OnSplashAttackTrigger() { }

    /// <summary>피격 애니메이션 트리거 직후 호출됩니다. 피격 이펙트/사운드 등을 여기서 처리하세요.</summary>
    protected virtual void OnHitTrigger() { }

    /// <summary>사망 애니메이션 트리거 직후 호출됩니다. 아이템 드롭, 사망 이펙트/사운드 등을 여기서 처리하세요.</summary>
    protected virtual void OnDieTrigger() { }

    // ------------------------------------------------------------------
    // 외부(데미지 시스템 등)에서 호출하는 API
    // ------------------------------------------------------------------

    /// <summary>플레이어의 공격 등 외부 이벤트에서 호출하세요. 진행 중이던 행동을 끊고 Hit 상태로 강제 전환합니다.</summary>
    public void TakeHit()
    {
        ChangeState(State.Hit);
    }

    /// <summary>IDamageable 구현. AttackHitbox/FireballProjectile 등 플레이어 공격 판정이 맞았을 때 호출합니다.
    /// 실제 체력 반영은 MonsterStats.TakeDamage()에 위임하고, 그 결과를 보고 체력이 남아있으면 TakeHit()으로
    /// Hit 상태(피격 모션)에, 0이 되면 Die()로 사망 상태에 진입시킵니다. 이미 죽은 상태(체력 0)면 무시합니다.</summary>
    public virtual void TakeDamage(float amount)
    {
        if (stats.CurrentHP <= 0f) return;

        stats.TakeDamage(amount);

        if (stats.CurrentHP <= 0f)
        {
            Die();
        }
        else
        {
            TakeHit();
        }
    }

    /// <summary>체력이 0이 되면 호출됩니다. Die 상태로 전환해 Die 애니메이션을 재생하고, dieDelay초 후
    /// 오브젝트를 파괴합니다. 필요하면 자식 클래스에서 override해서 추가 연출을 넣을 수도 있습니다
    /// (직접 override하는 대신 OnDieTrigger()를 쓰는 걸 권장합니다).</summary>
    protected virtual void Die()
    {
        ChangeState(State.Die);
    }

    /// <summary>자신(과 자식 오브젝트) 위의 모든 Collider를 꺼서 더 이상 공격 판정(AttackHitbox의
    /// OnTriggerEnter, FireballProjectile의 OverlapSphere 등)에 걸리지 않도록 합니다. TakeDamage() 쪽에도
    /// 이미 체력이 0이면 무시하는 가드가 있지만, 그것만으로는 시체가 dieDelay 동안 씬에 남아있는 사이
    /// 여전히 맞았다는 판정 자체(히트 이펙트, 데미지 숫자 HUD 등)는 막지 못해서 콜라이더를 직접 꺼줍니다.</summary>
    protected void DisableColliders()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Vector3 spawnGizmoPos = Application.isPlaying ? spawnPosition : transform.position;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeRange);
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, rangedRange);

        // 배회 범위(초록 상자)와 리쉬 범위(파란 원)는 스폰 지점 기준입니다.
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(spawnGizmoPos, new Vector3(wanderBoxSize, 0.1f, wanderBoxSize));
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(spawnGizmoPos, maxLeashDistance);
    }
}