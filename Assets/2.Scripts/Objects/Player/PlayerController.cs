using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("참조")]
    public Transform cameraTransform;
    public Animator animator;
    [Tooltip("비워두면 Awake()에서 같은 오브젝트의 PlayerTargeting을 자동으로 찾습니다. 기본 공격/스킬을 " +
              "시작할 때 이 컴포넌트가 감지한 가장 가까운 적 쪽으로 캐릭터를 스냅 회전시킵니다.")]
    public PlayerTargeting targeting;
    [Tooltip("비워두면 Awake()에서 하위(자식)에 있는 AttackAreaController를 자동으로 찾습니다. " +
              "콤보가 대시/스킬/안전장치로 강제 종료될 때 열려있는 공격 판정을 확실히 닫아주는 데 사용합니다.")]
    public AttackAreaController attackArea;
    [Tooltip("비워두면 Awake()에서 같은 오브젝트의 PlayerStats를 자동으로 찾습니다. HP가 바뀔 때마다 " +
              "uiIngame.SetHPBar()를 호출하는 데 이 컴포넌트의 CurrentHP/MaxHP를 읽어옵니다.")]
    public PlayerStats playerStats;
    [Tooltip("HP 바 등 인게임 UI를 담당하는 스크립트입니다. 비워두면 Awake()에서 씬에 있는 UIIngame을 " +
              "자동으로 찾습니다(보통 UI는 Player의 자식이 아니라 Canvas 쪽에 있어서 하위 검색 대신 " +
              "씬 전체에서 찾습니다). 매 프레임 HP 비율(0~1)로 uiIngame.SetHPBar(rate)를 호출해줍니다.")]
    public UIIngame uiIngame;
    [Tooltip("휘두르기(슬래시 궤적 등) VFX가 재생될 위치. 보통 무기(칼날) 쪽에 빈 자식 오브젝트를 만들어 " +
              "연결하세요. 비워두면 이 오브젝트(Player 루트) 자신의 위치/회전을 사용합니다. OnAttackSwingVfx " +
              "Animation Event에서 사용됩니다.")]
    public Transform weaponVfxPoint;
    [Tooltip("타격 하나에 VFX를 2개 넣고 싶을 때(예: 3타에 보조 이펙트 추가) 두 번째 이펙트가 재생될 위치입니다. " +
              "비워두면 이 오브젝트(Player 루트) 자신의 위치/회전을 사용합니다 - weaponVfxPoint와 같은 곳에서 " +
              "내고 싶다면 여기에도 같은 Transform을 연결하세요. OnAttackSwingVfx2 Animation Event에서 사용됩니다.")]
    public Transform weaponVfxPoint2;

    [Header("휘두르기 VFX 회전 보정")]
    [Tooltip("weaponVfxPoint의 회전에 추가로 곱해줄 보정값(오일러 각, 도)입니다. VFX 프리팹 자체의 기본 방향과 " +
              "실제 모션의 스윙 방향이 안 맞을 때, 코드를 건드리지 않고 이 값만 조절해서 맞추는 용도입니다. " +
              "1타/2타/3타가 서로 다른 방향으로 휘두른다면 세 값을 각각 다르게 맞추면 됩니다. " +
              "Play 모드 중에 값을 바꾸면 즉시 반영되어 바로 확인할 수 있지만, Play 모드를 끄면 변경한 값이 " +
              "초기화되니 마음에 드는 값을 찾으면 정지하기 전에 따로 적어두거나 컴포넌트를 우클릭 → " +
              "Copy Component 해뒀다가 정지 후 Paste Component Values로 붙여넣으세요.")]
    public Vector3 attack1SwingVfxRotationOffset;
    public Vector3 attack2SwingVfxRotationOffset;
    public Vector3 attack3SwingVfxRotationOffset;

    [Header("휘두르기 VFX 위치 보정")]
    [Tooltip("weaponVfxPoint 기준 로컬 오프셋(미터)입니다. weaponVfxPoint의 현재 회전 방향을 기준으로 " +
              "이동하므로(월드 X/Y/Z가 아니라 그 지점이 바라보는 방향 기준), 예를 들어 Z를 늘리면 무기가 " +
              "가리키는 방향으로, X를 늘리면 그 지점 기준 오른쪽으로 이동합니다. 회전 보정과 마찬가지로 " +
              "1타/2타/3타를 각각 다르게 맞출 수 있고, Play 모드 중 값을 바꾸면 바로 확인할 수 있습니다.")]
    public Vector3 attack1SwingVfxPositionOffset;
    public Vector3 attack2SwingVfxPositionOffset;
    public Vector3 attack3SwingVfxPositionOffset;

    [Header("휘두르기 VFX 2 (보조 이펙트) 회전 보정")]
    [Tooltip("weaponVfxPoint2 기준 회전 보정값입니다. 지금은 3타에만 두 번째 VFX를 쓸 계획이라면 " +
              "attack3SwingVfx2RotationOffset만 조절하고 나머지 둘은 그냥 0으로 둬도 됩니다 - 1타/2타 클립에는 " +
              "OnAttackSwingVfx2 이벤트 자체를 추가하지 않으면 아예 호출되지 않으니 값이 있어도 영향 없습니다.")]
    public Vector3 attack1SwingVfx2RotationOffset;
    public Vector3 attack2SwingVfx2RotationOffset;
    public Vector3 attack3SwingVfx2RotationOffset;

    [Header("휘두르기 VFX 2 (보조 이펙트) 위치 보정")]
    [Tooltip("weaponVfxPoint2 기준 위치 보정값입니다. 사용법은 위 회전 보정과 동일합니다.")]
    public Vector3 attack1SwingVfx2PositionOffset;
    public Vector3 attack2SwingVfx2PositionOffset;
    public Vector3 attack3SwingVfx2PositionOffset;

    [Header("필살기(UltSkill) VFX")]
    [Tooltip("필살기에 넣을 VFX 2개 중 첫 번째가 재생될 기준 위치/회전입니다. 비워두면 이 오브젝트(Player 루트) " +
              "자신의 위치/회전을 사용합니다. Animation Event에서 OnUltSkillVfx1(String)을 호출하면서 문자열 " +
              "파라미터에 Resources/VFX 아래의 이펙트 이름을 넣으세요. 두 VFX가 서로 다른 곳(예: 손 vs 바닥)에서 " +
              "나온다면 이 지점을 아예 다른 위치에 붙은 빈 자식 오브젝트로 연결하세요.")]
    public Transform ultSkillVfxPoint1;
    [Tooltip("ultSkillVfxPoint1 기준 위치 보정(로컬 방향 기준 미터)과 회전 보정(오일러 각, 도)입니다. " +
              "휘두르기 VFX 보정과 완전히 같은 방식이니, Play 모드에서 값을 바꿔가며 맞추고 정지 전에 저장해두세요.")]
    public Vector3 ultSkillVfx1PositionOffset;
    public Vector3 ultSkillVfx1RotationOffset;

    [Space]
    [Tooltip("필살기에 넣을 VFX 2개 중 두 번째가 재생될 기준 위치/회전입니다. 비워두면 이 오브젝트(Player 루트) " +
              "자신의 위치/회전을 사용합니다. Animation Event에서 OnUltSkillVfx2(String)을 호출하세요.")]
    public Transform ultSkillVfxPoint2;
    [Tooltip("ultSkillVfxPoint2 기준 위치/회전 보정입니다. ultSkillVfx1과 동일한 방식입니다.")]
    public Vector3 ultSkillVfx2PositionOffset;
    public Vector3 ultSkillVfx2RotationOffset;

    [Header("필살기 차지 이펙트 (칼에 기 모으기)")]
    [Tooltip("필살기를 시전하는 순간부터 재생되어 무기에 붙은 채로 계속 따라다니는 차지 VFX(Resources/VFX/ " +
              "아래 프리팹 이름)입니다. ultSkillVfxPoint1/2(한 프레임만 재생되는 일반 VFX)와 달리 " +
              "VFXManager.PlayAttached()로 재생해서 무기가 움직이는 동안에도 계속 따라붙습니다 - 보통 Loop " +
              "파티클로 만드세요. OnUltSlamImpact() Animation Event가 호출되는 순간(실제로 내려찍는 프레임) " +
              "자동으로 정리되고, 그 전에 피격/사망 등으로 필살기가 중간에 끊겨도 안전하게 정리됩니다. " +
              "비워두면 재생하지 않습니다.")]
    public string ultChargeVfxName = "FX_Player_UltCharge";
    [Tooltip("차지 VFX가 붙을 기준 위치입니다. 비워두면 weaponVfxPoint(무기 쪽에 연결해둔 지점)를 쓰고, " +
              "그것도 비어있으면 이 오브젝트(Player 루트) 자신을 씁니다.")]
    public Transform ultChargeVfxPoint;
    [Tooltip("ultChargeVfxPoint(또는 weaponVfxPoint) 기준 로컬 위치 보정(미터)입니다.")]
    public Vector3 ultChargeVfxPositionOffset;
    [Tooltip("ultChargeVfxPoint(또는 weaponVfxPoint) 기준 로컬 회전 보정(오일러 각, 도)입니다.")]
    public Vector3 ultChargeVfxRotationOffset;

    [Header("이동")]
    public float moveSpeed = 5f;
    public float rotationSmoothTime = 0.1f;

    [Header("걷기 (이벤트/컷씬 전용 - IsWalk)")]
    [Tooltip("평소 이동(WASD, IsMove)과는 별개로, 컷씬 등 이벤트에서 CutsceneMove()로 자동으로 걸을 때만 " +
              "사용하는 속도입니다. 이때는 IsMove가 아니라 IsWalk 애니메이터 bool을 켜서 걷는 모션을 " +
              "재생합니다 - 지금은 실제 플레이어 조작(이동)용이 아니라 이벤트 전용이므로, 애니메이션의 " +
              "걷기 사이클과 자연스럽게 맞도록 이 값을 인스펙터에서 직접 조절하세요.")]
    public float cutsceneWalkSpeed = 2f;

    [Header("대시")]
    public float dashSpeed = 6f;
    public float dashDuration = 0.9f;
    public float dashCooldown = 1f;
    [Tooltip("켜두면 구르는 동안 플레이어 오브젝트를 invincibleLayerName 레이어로 임시로 바꿔서 무적이 됩니다.")]
    public bool dashGrantsInvincibility = true;
    [Tooltip("구르는 동안 플레이어를 임시로 옮길 레이어 이름입니다. 이 레이어를 Edit > Project Settings > " +
              "Tags and Layers에서 미리 만들어두세요. 몬스터들의 Hit Mask(MonsterAttackHitbox/ProjectileBase/ " +
              "MiddleSlimeBoss 등)는 원래 \"Player\" 레이어만 체크되어 있을 테니 이 새 레이어는 자동으로 " +
              "빠져있어 손댈 필요가 없습니다 - 다만 Edit > Project Settings > Physics의 Layer Collision " +
              "Matrix에서는 이 레이어가 기존 Player 레이어와 똑같이 땅/벽 등과 충돌하도록 체크해두세요 " +
              "(안 그러면 구르는 동안 바닥을 뚫고 떨어질 수 있습니다). CharacterController는 그대로 " +
              "활성화된 채라 구르는 동안에도 이동은 정상적으로 계속됩니다 - 레이어만 바뀌어서 몬스터 공격의 " +
              "Hit Mask에 안 걸릴 뿐입니다.")]
    public string invincibleLayerName = "PlayerInvincible";
    [Tooltip("대시 진행도(0=시작 ~ 1=끝)에 따른 속도 배율 곡선. 기본값은 처음엔 빠르게 튀어나갔다가 " +
              "끝으로 갈수록 느려지도록(구르기 느낌) 되어 있습니다. 세로축(값)은 dashSpeed에 곱해지는 배율입니다.")]
    public AnimationCurve dashSpeedCurve = new AnimationCurve(
        new Keyframe(0f, 1f, 0f, -1.2f),
        new Keyframe(1f, 0.25f, -1.2f, 0f)
    );

    [Header("중력")]
    public float gravity = -20f;
    [Tooltip("바닥에 붙어있을 때 아래로 살짝 눌러주는 힘 (미끄러짐 방지)")]
    public float groundedStickForce = -2f;

    [Header("방향 전환 애니메이션")]
    [Tooltip("직전 이동 방향과 새 이동 방향의 각도 차이가 이 값(도) 이상이면 ChangeDirection 트리거를 발동합니다.")]
    public float directionChangeAngleThreshold = 120f;

    [Header("스킬")]
    [Tooltip("우클릭으로 사용하는 일반 스킬의 모션 재생 시간(초). 이 시간 동안 이동/회전/대시가 모두 잠깁니다. 실제 Skill 애니메이션 클립 길이에 맞춰 조정하세요.")]
    public float skillDuration = 1f;
    [Tooltip("Q로 사용하는 필살기(UltSkill)의 모션 재생 시간(초). 이 시간 동안 이동/회전/대시가 모두 잠깁니다. 실제 UltSkill 애니메이션 클립 길이에 맞춰 조정하세요.")]
    public float ultSkillDuration = 2f;

    [Header("쿨타임")]
    [Tooltip("스킬(우클릭) 재사용 대기시간(초). 스킬을 쓰는 순간부터(모션 재생과 별개로 바로) 카운트가 시작됩니다.")]
    public float skillCooldown = 8f;
    [Tooltip("필살기(Q) 재사용 대기시간(초). 필살기를 쓰는 순간부터(모션 재생과 별개로 바로) 카운트가 시작됩니다.")]
    public float ultCooldown = 20f;

    [Header("마나/에너지 소모")]
    [Tooltip("스킬(우클릭) 사용에 필요한 마나입니다. 쿨타임이 다 됐어도 마나가 부족하면 사용할 수 없습니다.")]
    public float skillManaCost = 30f;
    [Tooltip("필살기(Q) 사용에 필요한 마나입니다.")]
    public float ultManaCost = 80f;
    [Tooltip("필살기(Q) 사용에 필요한 에너지입니다. 에너지 최대치가 100이라 사실상 에너지가 가득 찼을 때만 " +
              "사용할 수 있습니다. 에너지는 PlayerStats.CurrentEnergy이며, 기본 공격/스킬이 적을 맞히면 " +
              "AttackHitbox.energyOnHit만큼 충전됩니다.")]
    public float ultEnergyCost = 100f;
    [Tooltip("켜두면 필살기(UltSkill) 모션이 재생되는 동안 대시와 똑같은 방식으로 무적이 됩니다 - " +
              "EnterInvincible()/ExitInvincible()을 그대로 재사용해서 플레이어 오브젝트를 invincibleLayerName " +
              "레이어로 임시로 바꿉니다(위 '대시' 항목의 invincibleLayerName/레이어 세팅과 완전히 공유합니다 - " +
              "따로 만들 필요 없습니다). 일반 스킬(우클릭 파이어볼)에는 적용되지 않고, 오직 필살기(Q)에만 적용됩니다.")]
    public bool ultSkillGrantsInvincibility = true;
    [Tooltip("필살기 모션/입력잠김(ultSkillDuration)이 끝난 뒤에도 무적만 이 시간(초)만큼 더 유지합니다 - " +
              "착지/회복 구간에 바로 피격당하는 걸 막는 여유 시간입니다. 애니메이션과 입력잠김 길이는 " +
              "그대로 두고 무적만 더 길게 하고 싶을 때 이 값을 올리세요. 0이면 예전처럼 ultSkillDuration이 " +
              "끝나는 즉시 무적도 같이 풀립니다. 이 여유 시간 중에 피격(TakeHit)/사망(Die)/대화 시작 등으로 " +
              "강제 종료되면 무적도 그 즉시 풀립니다(여유 시간을 끝까지 기다리지 않습니다).")]
    public float ultInvincibilityExtraDuration = 0f;

    [Header("필살기 게이지 UI")]
    [Tooltip("_ultCooldownImage가 실제 에너지 값을 얼마 만에 따라잡을지(초). 예를 들어 기본 공격 1타로 " +
              "에너지가 5(=5%) 충전되면 실제 값은 그 프레임에 바로 바뀌지만, 게이지는 순간이동하듯 툭 튀는 " +
              "대신 이 시간(기본 0.4초 = 2/5초) 동안 부드럽게 그 값까지 채워지는 것처럼 보이게 합니다. " +
              "0 이하로 두면 예전처럼 매 프레임 실제 값을 그대로 반영합니다(순간 이동).")]
    public float ultGaugeFillDuration = 0.4f;

    [Header("필살기강화 (SkillInfo에서 해제 시)")]
    [Tooltip("SkillInfo의 '필살기강화'가 해제되어 있으면, OnUltSlamImpact() Animation Event가 호출된 뒤 " +
              "이만큼의 시간(초)이 지나 2차 폭발이 터집니다. 기획 스펙: 내려찍고 0.5초 뒤.")]
    public float ultSecondExplosionDelay = 0.5f;
    [Tooltip("2차 폭발의 데미지 배율(%). 최종 데미지는 playerStats.CalculateDamage(이 값, 대상 방어력)로 " +
              "계산됩니다(기본 공격/파이어볼과 같은 공식 - 마법 데미지라고 해서 별도 스탯을 쓰지는 않습니다). " +
              "기획 스펙: 100 × 3 = 300%.")]
    public float ultSecondExplosionDamagePercent = 300f;
    [Tooltip("2차 폭발이 데미지를 주는 반경(미터). ultSecondExplosionPoint(비워두면 캐릭터 위치)를 중심으로 " +
              "이 범위 안의 모든 적이 맞습니다.")]
    public float ultSecondExplosionRadius = 3f;
    [Tooltip("2차 폭발이 대상으로 삼을 레이어. AttackHitbox의 enemyMask와 동일하게(몬스터가 속한 레이어) " +
              "맞춰주세요.")]
    public LayerMask ultSecondExplosionMask;
    [Tooltip("2차 폭발이 터지는 중심 위치. 비워두면 폭발이 실제로 터지는 순간의 캐릭터(Player) 위치를 " +
              "사용합니다.")]
    public Transform ultSecondExplosionPoint;
    [Tooltip("2차 폭발이 터질 때 재생할 VFX 이름 (Resources/VFX/ 아래 프리팹 이름과 일치해야 함). " +
              "비워두면 VFX 없이 데미지만 적용됩니다.")]
    public string ultSecondExplosionVfxName;

    [Header("기본공격강화 (SkillInfo에서 해제 시)")]
    [Tooltip("SkillInfo의 '기본공격강화'가 해제되어 있으면 기본 공격(Attack1/Attack2/Attack3) 모션이 재생되는 " +
              "동안 animator.speed를 이 배율로 올립니다 - 애니메이션 자체가 빨라지니 Animation Event로 걸려있는 " +
              "콤보 판정 타이밍(OnAttackComboWindowOpen 등)도 함께 빨라집니다. 기획 스펙: 공격속도 +30%. " +
              "데미지 +30%는 여기가 아니라 AttackHitbox(isBasicAttack인 히트박스)에서 처리합니다.")]
    public float basicAttackUpgradeSpeedMultiplier = 1.3f;

    [Header("기본 공격 콤보")]
    [Tooltip("좌클릭 기본 공격 (1타→2타→3타→1타...로 반복되는 루프형 콤보). 다음 타로 넘어가는 시점은 각 Attack " +
              "클립에 건 Animation Event(OnAttackComboWindowOpen)가 결정합니다. 콤보 창이 열려있는 동안 들어온 " +
              "입력만 인식해서 다음 타로 이어지고, 창이 열리기 전에 누른 입력은 그냥 무시됩니다(선입력 버퍼링 없음). " +
              "공격 중에는 이동/회전이 잠기고, 대시/스킬/필살기 입력이 들어오면 그 즉시 캔슬됩니다(이동 입력만으로는 " +
              "캔슬되지 않습니다). 이 필드(attackHitMaxDuration)는 그 Animation Event들이 어떤 이유로든 호출되지 " +
              "않을 때를 대비한 안전장치용 최대 지속시간(초)입니다 - 실제 Attack 클립보다 넉넉하게 길게 잡아두세요.")]
    public float attackHitMaxDuration = 1.5f;

    [Header("피격 / 사망")]
    [Tooltip("Hit(피격) 모션 재생 중 이동/회전/공격/스킬/대시 등 모든 조작을 막아두는 시간(초). " +
              "실제 Hit 애니메이션 클립 길이에 맞춰 조정하세요. PlayerStats가 데미지를 받으면(TakeHit() 호출) " +
              "자동으로 이 시간만큼 조작이 잠깁니다.")]
    public float hitStunDuration = 0.4f;

    [Header("디버그")]
    [Tooltip("콘솔에 기본 공격 콤보의 각 이벤트(타격 시작 / 콤보 창 열림 / 입력 무시 / 모션 종료) 발생 시각을 " +
              "로그로 남깁니다. 특정 타에서만 이상하게 동작할 때, 코드 문제인지 애니메이터/클립 설정 문제인지 " +
              "구분하는 데 사용하세요.")]
    public bool debugLogCombo;

    private CharacterController controller;
    private int normalLayer;
    private int invincibleLayer = -1;
    private InputAction moveAction;
    private InputAction dashAction;
    private InputAction skillAction;
    private InputAction ultSkillAction;
    private InputAction attackAction;

    private float verticalVelocity;
    private float currentYawVelocity;
    private float currentYaw;

    private bool isDashing;
    private float dashTimer;
    private float dashCooldownTimer;
    private Vector3 dashDirection;

    // 스킬/필살기 사용 중인지 여부. 이 동안에는 이동, 회전, 대시가 모두 잠깁니다.
    private bool isUsingSkill;
    private float skillTimer;

    // 지금 재생 중인 스킬이 일반 스킬이 아니라 필살기(UltSkill)인지 여부입니다. isUsingSkill/skillTimer는
    // 일반 스킬과 필살기가 공유하지만, 필살기 카메라 연출(UltSkillEffector)은 필살기가 끝날 때만
    // 종료해줘야 해서 이 플래그로 구분합니다 - EndUltCameraIfActive() 참고.
    private bool isUsingUltSkill;

    // PlayUltChargeVfx()가 VFXManager.PlayAttached()로 재생한 차지 이펙트 인스턴스입니다. null이 아니면
    // 지금 재생 중이라는 뜻이고, EndUltChargeVfxIfActive()가 이 참조로 풀에 반납합니다.
    private GameObject ultChargeVfxInstance;

    // ultInvincibilityExtraDuration만큼 무적을 더 유지하기 위해 ExitInvincible()을 지연 호출하는
    // 코루틴입니다. null이 아니면 지금 "무적 여유 시간"이 진행 중이라는 뜻입니다 - 새 필살기를 다시
    // 쓰거나 피격/사망 등으로 강제 종료될 때 이 코루틴을 멈추고 정리합니다(EndUltInvincibilityGraceIfActive() 참고).
    private Coroutine ultInvincibilityGraceRoutine;

    // 스킬/필살기 재사용 대기시간(쿨타임) 타이머. 모션 재생 시간(skillTimer)과는 별개로,
    // 스킬을 쓰는 순간 바로 skillCooldown/ultCooldown 값으로 채워지고 0이 될 때까지 흘러갑니다.
    private float skillCooldownTimer;
    private float ultCooldownTimer;

    // 필살기 게이지(UI)에 실제로 표시되는 값입니다. PlayerStats.CurrentEnergy를 기준으로 계산된
    // 실제 비율(ultEnergyRate)을 매 프레임 그대로 쓰지 않고, ultGaugeFillDuration 동안 서서히
    // 따라잡도록(Mathf.MoveTowards) UpdateSkillCooldownUI()에서 갱신합니다 - 자세한 이유는
    // ultGaugeFillDuration 툴팁 참고.
    private float displayedUltEnergyRate01 = 1f;

    // 기본 공격 콤보 중인지 여부. 이 동안 이동/회전은 잠기며, 대시/스킬 입력이 들어오면
    // HandleAttackCombo에서 즉시 캔슬되어 다른 동작으로 넘어갈 수 있습니다.
    private bool isAttacking;
    private int comboIndex; // 0 = 콤보 없음, 1~3 = 현재 몇 타째인지
    private bool comboWindowOpen; // OnAttackComboWindowOpen 애니메이션 이벤트가 호출되면 true (다음 타로 이어질 수 있는 시점)
    private float attackHitSafetyTimer; // 애니메이션 이벤트가 호출되지 않을 경우를 대비한 안전장치용 남은 시간

    // 마지막으로 "이동 중"이었을 때의 방향. 방향 급전환 감지에 사용합니다.
    private Vector3 lastMovingDirection;

    // 피격(Hit) 경직 중인지 여부. PlayerStats.TakeDamage()가 TakeHit()을 호출하면 켜지고,
    // hitStunDuration이 지나면 자동으로 꺼집니다. 이 동안에는 이동/회전/공격/스킬/대시가 모두 잠깁니다.
    private bool isHit;
    private float hitStunTimer;

    // 사망 여부. PlayerStats.TakeDamage()로 체력이 0이 되면 Die()가 호출되어 true가 되고,
    // 이후로는 되돌리지 않습니다(부활 로직은 아직 없습니다) - Update()에서 중력 적용을 제외한
    // 모든 조작(이동/회전/공격/스킬/대시/피격)을 영구히 무시합니다.
    private bool isDead;

    /// <summary>컷씬(CutsceneManager 등)이 지금 조작을 넘겨받아 대신 움직이고 있는지 여부입니다.
    /// true인 동안 Update()는 평소 이동/전투 입력 처리를 전부 건너뜁니다 - 실제 이동은 컷씬 쪽이
    /// CutsceneMove()를 매 프레임 직접 호출해서 넣어줍니다(BeginCutsceneControl() 참고).</summary>
    public bool IsCutsceneControlled { get; private set; }

    // 문자열을 매 프레임 비교하지 않도록 애니메이터 파라미터를 미리 해시로 변환해둡니다.
    private static readonly int IsMoveParam = Animator.StringToHash("IsMove");
    private static readonly int IsWalkParam = Animator.StringToHash("IsWalk"); // 이벤트/컷씬 전용 걷기 모션 (CutsceneMove() 참고)
    private static readonly int IsDashParam = Animator.StringToHash("IsDash");
    private static readonly int ChangeDirectionParam = Animator.StringToHash("ChangeDirection");
    private static readonly int SkillParam = Animator.StringToHash("Skill");
    private static readonly int UltSkillParam = Animator.StringToHash("UltSkill");
    private static readonly int Attack1Param = Animator.StringToHash("Attack1");
    private static readonly int Attack2Param = Animator.StringToHash("Attack2");
    private static readonly int Attack3Param = Animator.StringToHash("Attack3");
    private static readonly int HitParam = Animator.StringToHash("Hit");
    private static readonly int DieParam = Animator.StringToHash("Die");

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        normalLayer = gameObject.layer;
        // invincibleLayer는 대시 무적과 필살기 무적이 함께 공유하는 레이어라, 둘 중 하나라도 켜져있으면
        // 미리 찾아둡니다(하나만 켜져있다고 해서 다른 쪽 무적이 조용히 깨지면 안 되기 때문입니다).
        if (dashGrantsInvincibility || ultSkillGrantsInvincibility)
        {
            invincibleLayer = LayerMask.NameToLayer(invincibleLayerName);
            if (invincibleLayer < 0)
            {
                Debug.LogWarning($"[PlayerController] '{invincibleLayerName}' 레이어를 찾을 수 없습니다. " +
                                  "Edit > Project Settings > Tags and Layers에서 새 레이어를 추가하고 이름을 " +
                                  "맞춰주세요. 레이어가 없으면 구르기/필살기 무적이 동작하지 않습니다(항상 데미지를 받습니다).", this);
            }
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        if (targeting == null)
        {
            targeting = GetComponent<PlayerTargeting>();
        }
        if (attackArea == null)
        {
            attackArea = GetComponentInChildren<AttackAreaController>(true);
        }
        if (playerStats == null)
        {
            playerStats = GetComponent<PlayerStats>();
        }
        if (uiIngame == null)
        {
            // UI는 보통 Player의 자식이 아니라 Canvas 쪽에 있어서, 하위 검색이 아니라 씬 전체에서 찾습니다.
            uiIngame = FindFirstObjectByType<UIIngame>();
        }

        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        dashAction = new InputAction("Dash", InputActionType.Button, "<Keyboard>/leftShift");

        skillAction = new InputAction("Skill", InputActionType.Button, "<Mouse>/rightButton");
        ultSkillAction = new InputAction("UltSkill", InputActionType.Button, "<Keyboard>/q");

        attackAction = new InputAction("Attack", InputActionType.Button, "<Mouse>/leftButton");
    }

    private void OnEnable()
    {
        moveAction.Enable();
        dashAction.Enable();
        skillAction.Enable();
        ultSkillAction.Enable();
        attackAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
        dashAction.Disable();
        skillAction.Disable();
        ultSkillAction.Disable();
        attackAction.Disable();
    }

    private void Start()
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        currentYaw = transform.eulerAngles.y;
    }

    private void Update()
    {
        // 주의: HP/MP/스킬 쿨타임 UI 갱신은 이 함수 맨 끝에서 합니다(예전엔 맨 앞에서 했었습니다) -
        // HandleSkills()가 실제로 skillCooldownTimer/ultCooldownTimer를 흘려보내고(그리고 스킬/필살기를
        // 쓴 순간 값을 다시 채우는) 곳인데, UI 갱신이 그보다 먼저 실행되면 이번 프레임에 방금 바뀐 값이
        // 아니라 "지난 프레임 끝난 시점"의 한 프레임 묵은 값을 그려버립니다. 특히 필살기를 쓰는 바로 그
        // 프레임에는 UI가 아직 쿨타임이 시작되기 전(0, 즉 빈 문자열) 값을 그리고, 실제로 20초가 채워진
        // 모습은 그 다음 프레임에야 보이게 되어서 "쿨타임 숫자가 이상하게 나온다"는 증상으로 보였습니다.
        // 그래서 HandleSkills() 등 상태를 바꾸는 로직을 전부 실행한 뒤에 그 결과를 UI에 반영하도록 순서를 바꿨습니다.
        //
        // 주의 2: 인벤토리/캐릭터정보/옵션 같은 팝업이 열려있는 동안(UICanvas.Instance.IsUIOpen)에도
        // 이 블록 전체를 건너뜁니다 - Update()는 Time.timeScale이 0이어도 계속 호출되기 때문에,
        // 이 가드가 없으면 팝업이 열려있을 때 눌렀던 클릭(attackAction.WasPressedThisFrame())이
        // 그대로 읽혀서 공격 콤보가 시작돼버립니다. Animator는 Time.timeScale에 맞춰 멈춰있으니 화면엔
        // 아무 변화가 없다가, 팝업을 닫아 Time.timeScale이 1로 돌아오는 순간 그제서야 밀려있던 공격
        // 애니메이션이 재생돼서 "UI를 닫으면 플레이어가 한 번 공격한다"는 증상으로 보였습니다.
        //
        // 주의 3: 대화 중(TalkManager.IsTalking)에는 다른 팝업들과 달리 Time.timeScale이 멈추지
        // 않습니다(TalkManager.cs 참고 - 대화 도중 애니메이션이 정상 속도로 재생되어야 하기 때문).
        // 그래서 이 블록을 건너뛰는 동안에도 Animator는 계속 정상 속도로 흘러가는데, IsMove 등
        // 파라미터가 더 이상 갱신되지 않으니 그 순간 하던 동작(이동/공격/스킬 등)이 얼어붙지 않고
        // 계속 반복 재생될 수 있습니다(예: 달리다가 대화가 시작되면 제자리에서 계속 달리는 것처럼
        // 보임). 그래서 대화 중에는 아래 else 분기에서 ForceIdleFacingTalkPartnerWhileTalking()으로
        // 매 프레임 명시적으로 Idle 상태를 강제하고 대화 상대 쪽을 바라보게 합니다.
        // 주의 4: 컷씬 중(IsCutsceneControlled)에도 이 블록을 건너뜁니다 - CutsceneManager가
        // BeginCutsceneControl()로 조작을 넘겨받아 매 프레임 CutsceneMove()를 직접 호출해서 이동을
        // 넣어주므로, 평소 입력 처리(HandleSkills 등)와 동시에 돌아가면 충돌합니다. 대화 중과 달리
        // 컷씬 중엔 별도로 "매 프레임 Idle 강제"를 해줄 필요가 없습니다 - CutsceneMove(Vector3.zero)를
        // 호출하면 그 자체로 Idle 애니메이션이 나오기 때문입니다.
        if (!isDead && !IsAnyUIOpen() && !IsCutsceneControlled)
        {
            Vector3 moveDirection = GetCameraRelativeMoveDirection();

            HandleHitStun();
            HandleSkills();
            HandleAttackCombo();
            HandleDash(moveDirection);
            UpdateAnimator(moveDirection);

            bool isActionLocked = isUsingSkill || isAttacking || isHit;

            Vector3 horizontalVelocity;
            if (isActionLocked)
            {
                // 스킬/필살기/기본 공격 콤보/피격 경직 중에는 이동도, 대시도 하지 않습니다.
                horizontalVelocity = Vector3.zero;
            }
            else if (isDashing)
            {
                horizontalVelocity = dashDirection * dashSpeed * GetDashSpeedMultiplier();
            }
            else
            {
                horizontalVelocity = moveDirection * moveSpeed;
            }

            if (!isActionLocked)
            {
                RotateTowards(isDashing ? dashDirection : moveDirection);
            }
            ApplyGravity();

            Vector3 finalVelocity = horizontalVelocity;
            finalVelocity.y = verticalVelocity;
            controller.Move(finalVelocity * Time.deltaTime);
        }
        else if (!isDead && TalkManager.Instance != null && TalkManager.Instance.IsTalking)
        {
            // 대화 중에는 Time.timeScale이 멈추지 않으므로(주의 3 참고) 매 프레임 명시적으로
            // Idle을 강제하고 대화 상대를 바라보게 합니다.
            ForceIdleFacingTalkPartnerWhileTalking();
        }
        // isDead일 때는 위 블록 전체(HandleSkills 포함)를 건너뜁니다 - 사망 후에는 어떤 조작도 받지
        // 않고 죽은 자리에 그대로 멈춰있습니다. Die()에서 콜라이더(CharacterController 포함)를 꺼뒀기
        // 때문에 더 이상 Move()를 호출하지 않습니다(비활성화된 CharacterController에 Move()를 호출하는
        // 건 안전하지 않습니다).

        UpdateHPBar(); // 살아있든 죽었든, 매 프레임 최신 HP 비율을 UI에 반영합니다 (자연 회복도 매 프레임 조금씩 바뀌기 때문입니다).
        UpdateMPBar(); // MP도 자연 회복(mpRegenPerSecond)되므로 매 프레임 최신 비율을 반영합니다.
        UpdateSkillCooldownUI(); // 스킬 쿨타임 + 필살기 에너지/쿨타임 UI(게이지 이미지 + 남은 시간 텍스트)도 이번 프레임 최신 상태로 반영합니다.
    }

    /// <summary>인벤토리/캐릭터정보/옵션 같은 UICanvas 팝업이 지금 하나라도 열려있는지 확인합니다.
    /// UICanvas.Instance가 아직 없는 씬(테스트 씬 등)에서도 안전하게 false를 돌려줍니다.</summary>
    private static bool IsAnyUIOpen()
    {
        return UICanvas.Instance != null && UICanvas.Instance.IsUIOpen;
    }

    /// <summary>대화 중(TalkManager.IsTalking) 매 프레임 호출됩니다. 진행 중이던 이동/공격/스킬/대시/
    /// 피격 상태를 전부 정리해서 애니메이터를 Idle로 되돌리고, TalkManager.CurrentAnchor(대화
    /// 상대 NPC의 기준점) 쪽을 부드럽게 바라보도록 회전시킵니다. 대화가 끝나면 항상 깨끗한 Idle
    /// 상태에서 다시 시작하도록, 남아있을 수 있는 콤보/스킬/무적 상태도 여기서 안전하게 닫아줍니다
    /// (이미 끝난 상태에서 다시 호출해도 안전한 함수들만 사용합니다 - CloseAllHitboxes/ExitInvincible).</summary>
    private void ForceIdleFacingTalkPartnerWhileTalking()
    {
        if (animator != null)
        {
            animator.SetBool(IsMoveParam, false);
            animator.SetBool(IsWalkParam, false);
            animator.SetBool(IsDashParam, false);
            animator.ResetTrigger(SkillParam);
            animator.ResetTrigger(UltSkillParam);
            animator.ResetTrigger(Attack1Param);
            animator.ResetTrigger(Attack2Param);
            animator.ResetTrigger(Attack3Param);
            animator.ResetTrigger(HitParam);
            animator.speed = 1f; // 기본공격강화 등으로 배속이 올라가 있었을 수 있으니 원래 속도로 되돌립니다.
        }

        if (attackArea != null) attackArea.CloseAllHitboxes();
        EndUltInvincibilityGraceIfActive(); // 무적 여유 시간이 진행 중이었다면 취소합니다 - 대화가 시작됐으니 즉시 풀어야 합니다.
        ExitInvincible(); // 대시/필살기 무적 중이 아니었어도 안전하게 호출할 수 있습니다.
        EndUltCameraIfActive(); // 대화 중에 필살기 연출이 남아있을 일은 거의 없겠지만, 안전장치로 여기서도 정리합니다.
        EndUltChargeVfxIfActive(); // 차지 VFX도 함께 정리합니다.

        isDashing = false;
        isUsingSkill = false;
        isAttacking = false;
        isHit = false;
        comboIndex = 0;
        comboWindowOpen = false;

        Transform anchor = TalkManager.Instance.CurrentAnchor;
        if (anchor != null)
        {
            Vector3 direction = anchor.position - transform.position;
            direction.y = 0f;
            RotateTowards(direction);
        }
    }

    /// <summary>컷씬(CutsceneManager 등 외부 연출 스크립트)이 조작을 넘겨받기 시작할 때 호출하세요.
    /// ForceIdleFacingTalkPartnerWhileTalking()과 같은 이유로, 진행 중이던 조작/전투 상태(콤보/스킬/
    /// 대시/무적/필살기 카메라·차지VFX 등)를 안전하게 정리한 뒤(인터럽트 세이프티넷) IsCutsceneControlled를
    /// 켭니다 - 그 순간부터 Update()가 평소 이동/전투 처리를 건너뛰므로, 실제 이동은 CutsceneMove()를
    /// 매 프레임 직접 호출해서 넣어줘야 합니다. 이미 죽었거나 이미 컷씬 조작 중이면 아무 것도 하지
    /// 않습니다.</summary>
    public void BeginCutsceneControl()
    {
        if (isDead || IsCutsceneControlled) return;

        if (animator != null)
        {
            animator.SetBool(IsMoveParam, false);
            animator.SetBool(IsWalkParam, false);
            animator.SetBool(IsDashParam, false);
            animator.ResetTrigger(SkillParam);
            animator.ResetTrigger(UltSkillParam);
            animator.ResetTrigger(Attack1Param);
            animator.ResetTrigger(Attack2Param);
            animator.ResetTrigger(Attack3Param);
            animator.ResetTrigger(HitParam);
            animator.speed = 1f; // 기본공격강화 등으로 배속이 올라가 있었을 수 있으니 원래 속도로 되돌립니다.
        }

        if (attackArea != null) attackArea.CloseAllHitboxes();
        EndUltInvincibilityGraceIfActive(); // 무적 여유 시간이 진행 중이었다면 취소합니다.
        ExitInvincible(); // 대시/필살기 무적 중이 아니었어도 안전하게 호출할 수 있습니다.
        EndUltCameraIfActive(); // 컷씬 도중 필살기 연출이 남아있을 일은 거의 없겠지만, 안전장치로 정리합니다.
        EndUltChargeVfxIfActive();

        isDashing = false;
        isUsingSkill = false;
        isAttacking = false;
        isHit = false;
        comboIndex = 0;
        comboWindowOpen = false;

        IsCutsceneControlled = true;
    }

    /// <summary>컷씬이 끝나 조작을 돌려줄 때 호출하세요. IsCutsceneControlled를 끄고 Animator를
    /// Idle로 되돌립니다 - 그 다음 프레임부터 Update()가 평소 이동/전투 입력을 다시 처리합니다.</summary>
    public void EndCutsceneControl()
    {
        IsCutsceneControlled = false;
        if (animator != null)
        {
            animator.SetBool(IsMoveParam, false);
            animator.SetBool(IsWalkParam, false);
        }
    }

    /// <summary>컷씬이 매 프레임 호출해서 플레이어를 worldDirection 방향으로 걷게 합니다. 정규화되어
    /// 있지 않아도 되고(방향만 취하고 실제 이동 속도는 cutsceneWalkSpeed를 사용합니다), Vector3.zero를
    /// 넘기면 제자리에 멈춰서 Idle 애니메이션으로 표시됩니다. 평소 이동(IsMove, moveSpeed)과는 별개로
    /// IsWalk bool과 cutsceneWalkSpeed를 사용합니다 - 지금은 실제 플레이어 조작용이 아니라 컷씬 등
    /// 이벤트 전용이기 때문입니다. CharacterController 이동/회전(RotateTowards/ApplyGravity)은 평소
    /// 이동과 같은 경로를 그대로 타므로 걷는 모습 자체는 자연스럽게 이어집니다.
    /// BeginCutsceneControl() ~ EndCutsceneControl() 사이에서만 호출하세요 - 그 밖의 상태에서 호출하면
    /// 평소 Update()의 이동 처리와 충돌할 수 있어 안전하게 무시합니다.</summary>
    public void CutsceneMove(Vector3 worldDirection)
    {
        if (!IsCutsceneControlled || isDead) return;

        worldDirection.y = 0f;
        bool isMoving = worldDirection.sqrMagnitude > 0.0001f;

        if (animator != null) animator.SetBool(IsWalkParam, isMoving);

        Vector3 normalizedDirection = isMoving ? worldDirection.normalized : Vector3.zero;
        if (isMoving)
        {
            RotateTowards(normalizedDirection);
        }

        ApplyGravity();

        Vector3 finalVelocity = normalizedDirection * cutsceneWalkSpeed;
        finalVelocity.y = verticalVelocity;
        controller.Move(finalVelocity * Time.deltaTime);
    }

    /// <summary>플레이어를 position/rotation으로 즉시 순간이동시킵니다. CharacterController가 켜져있는
    /// 채로 transform.position을 직접 바꾸면 다음 Move() 호출 때 그 사이 이동한 것으로 충돌 판정을
    /// 시도해 엉뚱하게 밀려나거나 걸릴 수 있으므로, 표준적인 방식대로 컨트롤러를 잠깐 꺼서 순간이동시킨
    /// 뒤 다시 켭니다. 순간이동 직후 이전 낙하 속도(verticalVelocity)가 남아있으면 새 위치에서 바닥을
    /// 뚫고 떨어지거나 튕길 수 있어 0으로 초기화하고, 회전 스무딩(currentYaw/currentYawVelocity)도
    /// 새 회전값으로 맞춰서 다음 프레임에 이전 방향에서 홱 돌아오는 것처럼 보이지 않게 합니다.
    /// 주로 컷씬(CutsceneManager)이 화면이 까맣게 가려진 동안(FadeOut ~ FadeIn 사이) 호출합니다.</summary>
    public void TeleportTo(Vector3 position, Quaternion rotation)
    {
        controller.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        controller.enabled = true;

        verticalVelocity = 0f;
        currentYaw = rotation.eulerAngles.y;
        currentYawVelocity = 0f;
    }

    /// <summary>지금 마우스 포인터가 UI 위에 있는지(=이번 클릭을 UI가 이미 처리했는지) 확인합니다.
    /// attackAction이 기본 공격에 쓰는 마우스 왼쪽 버튼은 UI 버튼 클릭에도 똑같이 쓰이기 때문에,
    /// "팝업이 열려있는 동안엔 입력을 안 읽는다"는 IsAnyUIOpen() 가드만으로는 부족한 경우가 있습니다 -
    /// 팝업을 닫는 바로 그 클릭 자체는 같은 프레임에 Time.timeScale이 이미 1로 돌아오고 팝업도
    /// 닫혀있어서(currentPopup == null) IsAnyUIOpen()이 false를 돌려주는데, 그 순간에도 여전히
    /// attackAction.WasPressedThisFrame()은 true입니다(같은 클릭이니까요). EventSystem이 이번 클릭이
    /// UI 위에서 일어났다고 기억하고 있는 걸 직접 물어보면, 스크립트 실행 순서와 상관없이 이 클릭이
    /// UI용이었는지를 정확히 판단할 수 있습니다.</summary>
    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    /// <summary>피격 경직 타이머를 흘려보내고, 다 되면 조작 잠금을 풉니다.</summary>
    private void HandleHitStun()
    {
        if (!isHit) return;

        hitStunTimer -= Time.deltaTime;
        if (hitStunTimer <= 0f)
        {
            isHit = false;
        }
    }

    /// <summary>카메라의 수평 방향을 기준으로 WASD 입력을 월드 방향으로 변환합니다.</summary>
    private Vector3 GetCameraRelativeMoveDirection()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();
        if (input.sqrMagnitude < 0.0001f || cameraTransform == null)
        {
            return Vector3.zero;
        }

        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 direction = camForward * input.y + camRight * input.x;
        return Vector3.ClampMagnitude(direction, 1f);
    }

    private void HandleDash(Vector3 moveDirection)
    {
        if (dashCooldownTimer > 0f)
        {
            dashCooldownTimer -= Time.deltaTime;
        }

        if (!isDashing && !isUsingSkill && !isAttacking && !isHit && dashAction.WasPressedThisFrame() && dashCooldownTimer <= 0f)
        {
            // 이동 입력이 있으면 그 방향으로, 없으면 캐릭터가 바라보는 방향으로 대시합니다.
            Vector3 direction = moveDirection.sqrMagnitude > 0.01f ? moveDirection.normalized : transform.forward;
            dashDirection = direction;
            isDashing = true;
            dashTimer = dashDuration;
            if (dashGrantsInvincibility) EnterInvincible();

            if (animator != null)
            {
                animator.SetTrigger(IsDashParam);
            }
        }

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f)
            {
                isDashing = false;
                dashCooldownTimer = dashCooldown;
                ExitInvincible();
            }
        }
    }

    /// <summary>플레이어 오브젝트를 invincibleLayerName 레이어로 바꿔서 무적으로 만듭니다. 대시(구르기)와
    /// 필살기(UltSkill)가 공통으로 재사용합니다 - 몬스터 공격의 Hit Mask에 이 레이어가 빠져있으면(설정
    /// 안내 참고) 트리거/OverlapSphere 자체가 플레이어를 감지하지 못하게 만듭니다. PlayerStats.TakeDamage()까지
    /// 아예 호출되지 않으므로 히트 VFX/데미지 숫자도 뜨지 않습니다. CharacterController는 그대로 활성화된
    /// 채라 이동에는 영향이 없습니다. 호출하는 쪽(HandleDash/HandleSkills)에서 각자의 설정
    /// (dashGrantsInvincibility/ultSkillGrantsInvincibility)을 먼저 확인한 뒤에만 호출해야 합니다 -
    /// 이 함수 자체는 invincibleLayer가 정상적으로 찾아졌는지만 확인합니다.</summary>
    private void EnterInvincible()
    {
        if (invincibleLayer < 0) return;
        gameObject.layer = invincibleLayer;
    }

    /// <summary>무적 상태(대시 또는 필살기)가 정상적으로 끝나거나, 피격/사망 등으로 중간에 강제로 끊길 때
    /// 호출해서 원래 레이어로 되돌립니다. 무적이 아니었을 때 호출해도(레이어가 이미 원래대로였어도)
    /// 안전합니다 - 그래서 TakeHit()/Die()처럼 "혹시 몰라서" 안전장치로 호출하는 곳에서는 지금 정말
    /// 무적 상태였는지 따지지 않고 그냥 항상 호출합니다.</summary>
    private void ExitInvincible()
    {
        if (invincibleLayer < 0) return;
        gameObject.layer = normalLayer;
    }

    /// <summary>필살기 카메라 연출(UltSkillEffector)이 지금 재생 중이면(isUsingUltSkill) 게임플레이
    /// 카메라로 되돌리고 플래그를 끕니다. 필살기 모션이 정상적으로 끝났을 때(HandleSkills)뿐 아니라
    /// 피격(TakeHit)/사망(Die)/대화 시작(ForceIdleFacingTalkPartnerWhileTalking) 등 필살기가 중간에
    /// 강제로 끊기는 모든 경로에서 호출해서, 연출이 게임플레이 카메라로 안 돌아오고 화면에 남는 일이
    /// 없게 합니다. 필살기 중이 아니었으면 아무 것도 하지 않아 어디서 호출해도 안전합니다.</summary>
    private void EndUltCameraIfActive()
    {
        if (!isUsingUltSkill) return;
        isUsingUltSkill = false;
        UltSkillEffector.Instance?.EndSequence();
    }

    /// <summary>필살기 모션(ultSkillDuration/isUsingSkill)이 끝나는 순간 HandleSkills()에서 호출합니다.
    /// ultInvincibilityExtraDuration이 0보다 크면 무적을 그만큼 더 유지하기 위해 ExitInvincible()을
    /// 지연 호출하는 코루틴을 시작하고, 0 이하면(기본값) 예전처럼 즉시 ExitInvincible()을 호출합니다.</summary>
    private void ExitInvincibleAfterUltGraceIfAny()
    {
        if (ultInvincibilityExtraDuration > 0f)
        {
            RestartUltInvincibilityGraceRoutine(ExitInvincibleAfterDelayRoutine(ultInvincibilityExtraDuration));
        }
        else
        {
            ExitInvincible();
        }
    }

    /// <summary>진행 중이던 무적 여유 시간 코루틴이 있으면 즉시 멈추고 무적을 바로 풉니다. 피격(TakeHit)/
    /// 사망(Die)/대화 시작(ForceIdleFacingTalkPartnerWhileTalking) 등으로 여유 시간 중에 강제 종료될 때,
    /// 그리고 새 필살기를 다시 쓰기 직전(EnterInvincible() 전)에 호출해서 이전 여유 시간이 새 무적을
    /// 엉뚱하게 끊어버리는 일이 없게 합니다. 여유 시간이 진행 중이 아니었으면 아무 것도 하지 않아
    /// 어디서 호출해도 안전합니다.</summary>
    private void EndUltInvincibilityGraceIfActive()
    {
        if (ultInvincibilityGraceRoutine == null) return;
        StopCoroutine(ultInvincibilityGraceRoutine);
        ultInvincibilityGraceRoutine = null;
    }

    private void RestartUltInvincibilityGraceRoutine(IEnumerator routine)
    {
        EndUltInvincibilityGraceIfActive();
        ultInvincibilityGraceRoutine = StartCoroutine(routine);
    }

    private IEnumerator ExitInvincibleAfterDelayRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        ExitInvincible();
        ultInvincibilityGraceRoutine = null;
    }

    /// <summary>필살기 시전 순간 호출됩니다(HandleSkills() 참고). ultChargeVfxName이 설정되어 있으면
    /// ultChargeVfxPoint(비워두면 weaponVfxPoint, 그것도 비어있으면 이 오브젝트 자신)에 붙여서 차지
    /// VFX를 재생합니다. Loop 파티클이라 자동 반납 시점을 계산할 수 없으므로 넉넉하게 ultSkillDuration을
    /// duration으로 직접 지정합니다 - 실제로는 그보다 먼저 OnUltSlamImpact()나
    /// EndUltChargeVfxIfActive()가 정리하므로 이 값은 안전장치일 뿐입니다.</summary>
    private void PlayUltChargeVfx()
    {
        if (string.IsNullOrEmpty(ultChargeVfxName)) return;

        Transform point = ultChargeVfxPoint != null ? ultChargeVfxPoint
            : (weaponVfxPoint != null ? weaponVfxPoint : transform);

        ultChargeVfxInstance = VFXManager.Instance.PlayAttached(
            ultChargeVfxName, point, ultChargeVfxPositionOffset,
            Quaternion.Euler(ultChargeVfxRotationOffset), ultSkillDuration);
    }

    /// <summary>재생 중인 필살기 차지 VFX가 있으면 즉시 풀로 반납합니다(OnUltSlamImpact()에서의 정상
    /// 종료뿐 아니라, 피격(TakeHit)/사망(Die)/대화 시작(ForceIdleFacingTalkPartnerWhileTalking) 등
    /// 필살기가 중간에 끊기는 모든 경로에서도 호출해서 무기에 이펙트가 계속 남아있는 일이 없게
    /// 합니다). 재생 중이 아니었으면 아무 것도 하지 않아 어디서 호출해도 안전합니다.</summary>
    private void EndUltChargeVfxIfActive()
    {
        if (ultChargeVfxInstance == null) return;
        VFXManager.Instance.ReturnToPool(ultChargeVfxName, ultChargeVfxInstance);
        ultChargeVfxInstance = null;
    }

    /// <summary>지금 대시가 시작된 지 얼마나 지났는지(0~1)를 dashSpeedCurve에 대입해 속도 배율을 구합니다.</summary>
    private float GetDashSpeedMultiplier()
    {
        float progress = 1f - Mathf.Clamp01(dashTimer / dashDuration);
        return dashSpeedCurve.Evaluate(progress);
    }

    /// <summary>우클릭(Skill)/Q(UltSkill) 입력을 처리합니다. 스킬 모션이 끝날 때까지는 새 입력을 받지 않습니다.
    /// 구르는 중에는 스킬을 사용할 수 없지만, 기본 공격 콤보 중에는 언제든 캔슬하고 스킬로 전환할 수 있습니다
    /// (반대로 스킬 중에는 HandleDash/HandleAttackCombo에서 대시/기본 공격을 막습니다).
    /// 쿨타임(skillCooldownTimer/ultCooldownTimer)은 모션 재생 시간과 별개로, 실제로 스킬을 쓴 순간
    /// 바로 시작됩니다 - 모션이 끝나야 카운트가 시작되는 게 아닙니다.
    /// 쿨타임과 별개로 마나(skillManaCost/ultManaCost)와, 필살기는 에너지(ultEnergyCost)까지 충분해야
    /// 실제로 사용됩니다 - 셋 중 하나라도 부족하면 입력은 그냥 무시됩니다(쿨타임이 남아있을 때와 동일).
    /// 사용하는 순간 PlayerStats.SpendMana()/SpendEnergy()로 즉시 소모합니다.
    /// ultSkillGrantsInvincibility가 켜져있으면 필살기 모션이 재생되는 동안(isUsingSkill이 꺼질 때까지)
    /// 대시와 같은 방식으로 무적입니다(EnterInvincible() 재사용) - 일반 스킬(우클릭)에는 적용되지 않습니다.</summary>
    private void HandleSkills()
    {
        if (skillCooldownTimer > 0f) skillCooldownTimer -= Time.deltaTime;
        if (ultCooldownTimer > 0f) ultCooldownTimer -= Time.deltaTime;

        if (isUsingSkill)
        {
            skillTimer -= Time.deltaTime;
            if (skillTimer <= 0f)
            {
                isUsingSkill = false;
                // 안전장치: 스킬용 히트박스에 대응하는 OnHitboxClose 이벤트가 어떤 이유로든
                // 호출되지 않았더라도, 스킬이 끝나는 시점에 확실히 닫아줍니다.
                if (attackArea != null) attackArea.CloseAllHitboxes();
                // 필살기(무적 적용 대상)였다면 ultInvincibilityExtraDuration만큼 무적을 더 유지할 수
                // 있게 위임하고, 일반 스킬이었다면(애초에 무적 상태가 아니었으므로) 그냥 즉시
                // ExitInvincible()을 호출하는 안전한 호출입니다(무적이 아니었어도 안전합니다).
                if (isUsingUltSkill && ultSkillGrantsInvincibility)
                {
                    ExitInvincibleAfterUltGraceIfAny();
                }
                else
                {
                    ExitInvincible();
                }
                EndUltCameraIfActive(); // 필살기였다면(isUsingUltSkill) 카메라 연출을 게임플레이 카메라로 되돌립니다. 일반 스킬이었다면 아무 일도 하지 않습니다.
                EndUltChargeVfxIfActive(); // 안전장치: OnUltSlamImpact()가 어떤 이유로든 호출되지 않았어도, 모션이 끝나는 시점에 확실히 정리합니다.
            }
            return;
        }

        if (isDashing || isHit) return; // 구르는 중이거나 피격 경직 중에는 스킬 입력을 받지 않습니다.

        bool ultReady = ultCooldownTimer <= 0f
            && playerStats.CurrentMP >= ultManaCost
            && playerStats.CurrentEnergy >= ultEnergyCost;
        if (ultSkillAction.WasPressedThisFrame() && ultReady)
        {
            CancelAttack(); // 기본 공격 콤보 중이었다면 캔슬하고 필살기로 전환합니다.
            StartSkill(UltSkillParam, ultSkillDuration);
            isUsingUltSkill = true;
            ultCooldownTimer = ultCooldown;
            playerStats.SpendMana(ultManaCost);
            playerStats.SpendEnergy(ultEnergyCost);
            if (ultSkillGrantsInvincibility)
            {
                EndUltInvincibilityGraceIfActive(); // 혹시 이전 필살기의 무적 여유 시간이 아직 안 끝났다면 취소하고, 새 무적을 깨끗하게 시작합니다.
                EnterInvincible();
            }
            // 필살기 카메라 연출(정면샷 → 뒤쪽 시점)을 시작합니다 - StartSkill() 안에서 이미
            // FaceNearestTargetIfAny()로 회전을 끝낸 뒤라 카메라 계산 시점엔 이미 올바른 방향을
            // 보고 있습니다(NPCTalker의 회전-후-카메라 순서와 같은 이유). 씬에
            // UltSkillEffector가 없어도 안전하게 아무 일도 하지 않습니다.
            UltSkillEffector.Instance?.PlayFaceShot();
            // 칼에 기를 모으는 차지 VFX도 같은 순간 시작합니다 - OnUltSlamImpact()(실제 내려찍는
            // 프레임)에서 정리됩니다.
            PlayUltChargeVfx();
            return;
        }

        bool skillReady = skillCooldownTimer <= 0f && playerStats.CurrentMP >= skillManaCost;
        if (skillAction.WasPressedThisFrame() && skillReady)
        {
            CancelAttack();
            StartSkill(SkillParam, skillDuration);
            skillCooldownTimer = skillCooldown;
            playerStats.SpendMana(skillManaCost);
        }
    }

    /// <summary>SkillInfo의 '패시브강화'가 해제되어 있을 때, 기본 공격이 적중할 때마다 AttackHitbox가
    /// 호출합니다. 우클릭 스킬(파이어볼)의 쿨타임만 앞당기고, 필살기 쿨타임(ultCooldownTimer)에는
    /// 영향을 주지 않습니다. 이미 쿨타임이 0이거나(스킬 사용 가능한 상태) 스킬을 쓴 적이 없는 상태(0)여도
    /// 0 밑으로는 내려가지 않아 안전합니다.</summary>
    public void ReduceSkillCooldown(float amount)
    {
        skillCooldownTimer = Mathf.Max(0f, skillCooldownTimer - amount);
    }

    private void StartSkill(int animatorTriggerParam, float duration)
    {
        isUsingSkill = true;
        skillTimer = duration;

        FaceNearestTargetIfAny();

        if (animator != null)
        {
            animator.SetTrigger(animatorTriggerParam);
        }
    }

    /// <summary>좌클릭 기본 공격 콤보(1타→2타→3타→1타→2타→3타...로 계속 이어지는 3타 루프)를 처리합니다.
    /// 다음 타로 이어지는 시점은 시간이 아니라 각 Attack 클립의 Animation Event(OnAttackComboWindowOpen)로
    /// 결정됩니다. 선입력 버퍼링은 없습니다 - 콤보 창이 열려있는 동안 들어온 입력만 인식해서 다음 타로
    /// 이어지고, 창이 열리기 전에 누른 입력은 그냥 무시됩니다. 어느 타에서든 콤보 창이 열려있는 동안
    /// 입력이 없으면 그 타를 끝으로 콤보가 종료됩니다. 3타 다음에 콤보 창 안에서 좌클릭하면 다시
    /// 1타부터 자연스럽게 반복됩니다.
    /// 기본 공격은 대시/스킬/필살기로만 캔슬할 수 있습니다 - 공격 중에 대시나 스킬 입력이 들어오면
    /// 그 즉시 콤보를 끊고 해당 동작을 수행합니다 (대시는 쿨타임이 남아있으면 캔슬하지 않습니다).
    /// 이동 입력만으로는 캔슬되지 않고, 콤보가 자연스럽게 끝날 때까지 이동/회전은 잠긴 채로 유지됩니다.
    /// 대시 중이거나 스킬 사용 중에는 새로 기본 공격을 시작할 수 없습니다.</summary>
    private void HandleAttackCombo()
    {
        if (isAttacking)
        {
            bool wantsToDash = dashAction.WasPressedThisFrame() && dashCooldownTimer <= 0f;
            bool wantsSkill = skillAction.WasPressedThisFrame() || ultSkillAction.WasPressedThisFrame();

            if (wantsToDash || wantsSkill)
            {
                // 대시/스킬 입력이 들어오면 언제든 콤보를 캔슬합니다.
                // 스킬/필살기는 HandleSkills에서, 대시는 HandleDash에서 이어서 처리됩니다.
                LogCombo($"{comboIndex}타 - 대시/스킬 입력으로 캔슬");
                CancelAttack();
                return;
            }

            // 안전장치: OnAttackComboWindowOpen/OnAttackMotionEnd 이벤트를 클립에 추가하는 걸 깜빡했거나
            // 어떤 이유로 호출되지 않아도, 이 시간이 지나면 강제로 콤보를 끝내서 캐릭터가 영구히 멈추지 않게 합니다.
            attackHitSafetyTimer -= Time.deltaTime;
            if (attackHitSafetyTimer <= 0f)
            {
                LogCombo($"{comboIndex}타 - 안전장치 타임아웃으로 강제 종료 (OnAttackMotionEnd 이벤트가 호출되지 않았습니다!)");
                EndAttackMotion();
                return;
            }

            // !IsPointerOverUI(): 이 클릭이 방금 UI 버튼(예: 옵션 창의 확인/나가기 버튼)을 눌러
            // 팝업을 닫은 바로 그 클릭이면, 같은 프레임에 기본 공격으로 다시 처리하지 않습니다
            // (IsAnyUIOpen 주석 참고 - Update() 상단의 팝업-열림 가드만으로는 이 클릭 자체를
            // 막을 수 없어서 여기서 한 번 더 확인합니다).
            if (attackAction.WasPressedThisFrame() && !IsPointerOverUI())
            {
                if (comboWindowOpen)
                {
                    LogCombo($"{comboIndex}타 - 콤보 창이 열려있어 즉시 다음 타로 진행");
                    AdvanceCombo();
                }
                else
                {
                    LogCombo($"{comboIndex}타 - 콤보 창이 열리기 전이라 입력 무시");
                }
            }

            return;
        }

        if (isDashing || isUsingSkill || isHit) return; // 대시/스킬/피격 경직 중에는 기본 공격을 사용할 수 없습니다.

        if (attackAction.WasPressedThisFrame() && !IsPointerOverUI())
        {
            comboIndex = 1;
            StartAttackHit(comboIndex);
        }
    }

    private void CancelAttack()
    {
        if (!isAttacking) return;
        EndAttackMotion();
    }

    /// <summary>다음 타로 넘어갑니다. 3타 다음은 다시 1타로 돌아가 콤보가 자연스럽게 반복됩니다.</summary>
    private void AdvanceCombo()
    {
        comboIndex = (comboIndex % 3) + 1; // 1→2→3→1→2→3...
        StartAttackHit(comboIndex);
    }

    private void StartAttackHit(int hitIndex)
    {
        isAttacking = true;
        comboWindowOpen = false;
        attackHitSafetyTimer = attackHitMaxDuration;

        LogCombo($"{hitIndex}타 시작 (Trigger 발동)");

        FaceNearestTargetIfAny();

        if (animator == null) return;

        // 기본공격강화(SkillInfo)가 해제되어 있으면 애니메이션 재생 속도 자체를 올립니다 - 콤보 판정
        // 타이밍이 Animation Event(OnAttackComboWindowOpen 등)로 걸려있어서 이렇게 하면 판정도 같이
        // 빨라집니다. EndAttackMotion()에서 다시 1로 되돌리는 것을 잊지 마세요(이동/대기 애니메이션에
        // 그대로 새어나가지 않도록).
        animator.speed = (playerStats != null && playerStats.HasBasicAttackUpgrade) ? basicAttackUpgradeSpeedMultiplier : 1f;

        switch (hitIndex)
        {
            case 1: animator.SetTrigger(Attack1Param); break;
            case 2: animator.SetTrigger(Attack2Param); break;
            case 3: animator.SetTrigger(Attack3Param); break;
        }
    }

    /// <summary>Animation Event 전용 콜백입니다. 각 Attack1/Attack2/Attack3 클립에서 "다음 콤보 입력을
    /// 받아도 되는 시점"(보통 타격 직후, 다음 스윙으로 이어져도 어색하지 않은 프레임)에 이 함수를 호출하도록
    /// Animation Event를 추가하세요. 이 시점부터 좌클릭을 받아들이기 시작합니다 - 그 전에 눌렀던 입력은
    /// 저장해두지 않으므로, 창이 열린 뒤에 다시 눌러야 다음 타로 이어집니다.</summary>
    public void OnAttackComboWindowOpen()
    {
        if (!isAttacking)
        {
            LogCombo("OnAttackComboWindowOpen 호출됨 - 하지만 이미 공격이 끝난 상태라 무시함 (이벤트가 너무 늦게 호출된 걸 수 있습니다)");
            return; // 안전장치 등으로 이미 콤보가 끝난 뒤 이벤트가 늦게 들어온 경우 무시합니다.
        }

        LogCombo($"{comboIndex}타 - OnAttackComboWindowOpen 호출됨");

        comboWindowOpen = true;
    }

    /// <summary>Animation Event 전용 콜백입니다. 각 Attack 클립의 끝부분(더 이상 콤보로 이어지면 안 되는 시점)에
    /// 이 함수를 호출하도록 Animation Event를 추가하세요. 다음 타로 이어지지 않았다면 공격을 종료하고
    /// 이동/회전 잠금을 풉니다.</summary>
    public void OnAttackMotionEnd()
    {
        if (!isAttacking) return;
        LogCombo($"{comboIndex}타 - OnAttackMotionEnd 호출됨 (정상 종료)");
        EndAttackMotion();
    }

    /// <summary>Animation Event 전용 콜백입니다. 히트 판정과는 별개로, 무기를 실제로 "휘두르는" 순간
    /// (예: 슬래시 궤적 VFX)에 이 함수를 호출하도록 Attack1/Attack2/Attack3 등 각 클립에 문자열
    /// 파라미터로 VFX 이름을 넣어 Animation Event를 추가하세요 (예: "FX_Player_Slash"). 적을 실제로
    /// 맞혔는지와 무관하게, 스윙 그 자체를 표현하는 이펙트라 항상 재생됩니다 - 맞았을 때만 나와야 하는
    /// 이펙트는 대신 AttackHitbox.hitVfxName을 사용하세요.
    /// 재생 위치/회전은 weaponVfxPoint를 기준으로, 지금 몇 타째인지(comboIndex)에 맞는
    /// attack1/2/3SwingVfxPositionOffset(위치)과 attack1/2/3SwingVfxRotationOffset(회전)을 각각
    /// 적용해서 계산합니다 - 모션마다 스윙 방향/위치가 달라 VFX가 어색하게 나온다면 코드가 아니라
    /// 그 여섯 필드값을 인스펙터에서 조절해서 맞추세요.</summary>
    public void OnAttackSwingVfx(string vfxName)
    {
        PlayOffsetVfx(vfxName, weaponVfxPoint, GetSwingVfxPositionOffset(), GetSwingVfxRotationOffset());
    }

    /// <summary>지금 콤보가 몇 타째인지(comboIndex)에 맞는 회전 보정값을 돌려줍니다.
    /// 콤보 중이 아닐 때(comboIndex가 1~3 범위 밖) 호출되는 경우는 없지만, 안전하게 0을 반환합니다.</summary>
    private Vector3 GetSwingVfxRotationOffset()
    {
        switch (comboIndex)
        {
            case 1: return attack1SwingVfxRotationOffset;
            case 2: return attack2SwingVfxRotationOffset;
            case 3: return attack3SwingVfxRotationOffset;
            default: return Vector3.zero;
        }
    }

    /// <summary>지금 콤보가 몇 타째인지(comboIndex)에 맞는 weaponVfxPoint 기준 로컬 위치 보정값을 돌려줍니다.</summary>
    private Vector3 GetSwingVfxPositionOffset()
    {
        switch (comboIndex)
        {
            case 1: return attack1SwingVfxPositionOffset;
            case 2: return attack2SwingVfxPositionOffset;
            case 3: return attack3SwingVfxPositionOffset;
            default: return Vector3.zero;
        }
    }

    /// <summary>Animation Event 전용 콜백입니다. 한 타격에 VFX를 2개 넣고 싶을 때(예: 3타에 보조 이펙트) 이걸
    /// 추가로 호출하세요 - OnAttackSwingVfx와 완전히 독립적인 두 번째 슬롯이라, 첫 번째 VFX와 다른 시점/위치에서
    /// 재생하고 싶을 때 문자열 파라미터(VFX 이름)로 Animation Event를 따로 추가하면 됩니다. 1타/2타 클립에는
    /// 이 이벤트를 아예 추가하지 않으면 그 타에서는 호출되지 않으니, 지금처럼 3타에만 쓰고 싶다면 Attack3
    /// 클립에만 걸어두세요. 재생 위치/회전은 weaponVfxPoint2를 기준으로 attack1/2/3SwingVfx2PositionOffset/
    /// RotationOffset을 적용해서 계산합니다.</summary>
    public void OnAttackSwingVfx2(string vfxName)
    {
        PlayOffsetVfx(vfxName, weaponVfxPoint2, GetSwingVfx2PositionOffset(), GetSwingVfx2RotationOffset());
    }

    /// <summary>지금 콤보가 몇 타째인지(comboIndex)에 맞는 두 번째 VFX용 회전 보정값을 돌려줍니다.</summary>
    private Vector3 GetSwingVfx2RotationOffset()
    {
        switch (comboIndex)
        {
            case 1: return attack1SwingVfx2RotationOffset;
            case 2: return attack2SwingVfx2RotationOffset;
            case 3: return attack3SwingVfx2RotationOffset;
            default: return Vector3.zero;
        }
    }

    /// <summary>지금 콤보가 몇 타째인지(comboIndex)에 맞는 두 번째 VFX용 weaponVfxPoint2 기준 로컬 위치 보정값을 돌려줍니다.</summary>
    private Vector3 GetSwingVfx2PositionOffset()
    {
        switch (comboIndex)
        {
            case 1: return attack1SwingVfx2PositionOffset;
            case 2: return attack2SwingVfx2PositionOffset;
            case 3: return attack3SwingVfx2PositionOffset;
            default: return Vector3.zero;
        }
    }

    /// <summary>Animation Event 전용 콜백입니다. 필살기(UltSkill) 모션 중 첫 번째 VFX가 나가야 하는 프레임에
    /// 이 함수를 호출하도록 UltSkill 클립에 문자열 파라미터(VFX 이름)로 Animation Event를 추가하세요.
    /// 재생 위치/회전은 ultSkillVfxPoint1을 기준으로 ultSkillVfx1PositionOffset/ultSkillVfx1RotationOffset을
    /// 적용해서 계산하며, 모션과 안 맞으면 코드가 아니라 그 필드들을 인스펙터에서 조절해서 맞추세요.</summary>
    public void OnUltSkillVfx1(string vfxName)
    {
        PlayOffsetVfx(vfxName, ultSkillVfxPoint1, ultSkillVfx1PositionOffset, ultSkillVfx1RotationOffset);
    }

    /// <summary>Animation Event 전용 콜백입니다. 필살기(UltSkill)의 두 번째 VFX용이며, 나머지는
    /// OnUltSkillVfx1과 동일합니다 (ultSkillVfxPoint2 / ultSkillVfx2PositionOffset / ultSkillVfx2RotationOffset 사용).</summary>
    public void OnUltSkillVfx2(string vfxName)
    {
        PlayOffsetVfx(vfxName, ultSkillVfxPoint2, ultSkillVfx2PositionOffset, ultSkillVfx2RotationOffset);
    }

    /// <summary>Animation Event 전용 콜백입니다. 필살기(UltSkill) 모션 중 실제로 "내려찍는"(타격) 프레임에
    /// 이 함수를 호출하도록 UltSkill 클립에 Animation Event를 추가하세요(파라미터 없음). SkillInfo의
    /// '필살기강화'가 해제되어 있을 때만(playerStats.HasUltUpgrade) ultSecondExplosionDelay초 뒤 2차 폭발을
    /// 예약합니다 - 해제 전이면 이벤트를 걸어두셔도 안전하게 아무 일도 하지 않습니다(나중에 강화를 해제하면
    /// 별도 작업 없이 바로 동작합니다).</summary>
    public void OnUltSlamImpact()
    {
        EndUltChargeVfxIfActive(); // 실제로 내려찍는(에너지를 방출하는) 순간이므로 차지 VFX를 여기서 정리합니다.

        if (playerStats == null || !playerStats.HasUltUpgrade) return;
        StartCoroutine(UltSecondExplosionRoutine());
    }

    /// <summary>Animation Event 전용 콜백입니다. 필살기 카메라 연출(정면샷 → 뒤로 확 멀어지는 샷 → 뒤쪽
    /// 시점) 중, "정면샷에서 뒤로 부드럽게 멀어지며 주변 풍경까지 보여줄" 프레임에 이 함수를 호출하도록
    /// UltSkill 클립에 Animation Event를 추가하세요(파라미터 없음). OnUltCameraSwitchToBack()보다 먼저,
    /// 정면샷이 잠깐 보여진 직후 프레임에 걸어두세요. UltSkillEffector.cs 헤더 주석을 참고하세요.</summary>
    public void OnUltCameraPullBack()
    {
        UltSkillEffector.Instance?.PullBack();
    }

    /// <summary>Animation Event 전용 콜백입니다. 필살기 카메라 연출 중, "뒤로 멀어진 샷에서 뒤쪽(내려찍기)
    /// 시점으로 전환할" 프레임에 이 함수를 호출하도록 UltSkill 클립에 Animation Event를
    /// 추가하세요(파라미터 없음). 보통 캐릭터가 무기를 들어올리며 등을 돌리기 시작하는, 실제
    /// 내려찍기(OnUltSlamImpact)보다 살짝 앞선 프레임에 걸어두면 "뒤쪽 시점에서 내려찍는 모습을
    /// 보여주는" 연출이 자연스럽게 이어집니다. UltSkillEffector.cs 헤더 주석을 참고하세요.</summary>
    public void OnUltCameraSwitchToBack()
    {
        UltSkillEffector.Instance?.SwitchToBackShot();
    }

    /// <summary>ultSecondExplosionDelay초 뒤, ultSecondExplosionPoint(비워두면 그 순간의 캐릭터 위치)를
    /// 중심으로 ultSecondExplosionRadius 범위 안의 모든 대상에게 ultSecondExplosionDamagePercent%의 데미지를
    /// 줍니다. FireballProjectile.Explode()/AttackHitbox와 같은 방식(Physics.OverlapSphere + CalculateDamage)입니다.</summary>
    private IEnumerator UltSecondExplosionRoutine()
    {
        yield return new WaitForSeconds(ultSecondExplosionDelay);

        Vector3 origin = ultSecondExplosionPoint != null ? ultSecondExplosionPoint.position : transform.position;
        Collider[] hits = Physics.OverlapSphere(origin, ultSecondExplosionRadius, ultSecondExplosionMask);

        foreach (Collider col in hits)
        {
            IDamageable damageable = col.GetComponentInParent<IDamageable>();
            if (damageable == null) continue;

            MonsterStats targetStats = col.GetComponentInParent<MonsterStats>();
            float targetDefense = targetStats != null ? targetStats.TotalDefense : 0f;

            DamageResult result = playerStats.CalculateDamage(ultSecondExplosionDamagePercent, targetDefense);
            damageable.TakeDamage(result.damage);

            Vector3 numberPosition = col.ClosestPoint(origin) + Vector3.up * 0.8f;
            DamageNumberManager.Instance.Show(result.damage, numberPosition, result.isCrit, DamageNumberTeam.Enemy);
        }

        if (!string.IsNullOrEmpty(ultSecondExplosionVfxName))
        {
            VFXManager.Instance.Play(ultSecondExplosionVfxName, origin);
        }
    }

    /// <summary>지정한 point(비워두면 이 오브젝트 자신)를 기준으로, point의 방향 기준 로컬 위치 보정과
    /// 회전 보정을 적용해서 VFX를 재생하는 공용 헬퍼입니다. 스윙 VFX/필살기 VFX가 전부 이 방식을 공유합니다.</summary>
    private void PlayOffsetVfx(string vfxName, Transform point, Vector3 positionOffset, Vector3 rotationOffset)
    {
        if (string.IsNullOrEmpty(vfxName)) return;

        Transform basePoint = point != null ? point : transform;
        Quaternion rotation = basePoint.rotation * Quaternion.Euler(rotationOffset);
        Vector3 position = basePoint.position + basePoint.TransformVector(positionOffset);
        VFXManager.Instance.Play(vfxName, position, rotation);
    }

    private void EndAttackMotion()
    {
        isAttacking = false;
        comboIndex = 0;
        comboWindowOpen = false;

        // 기본공격강화로 올려놨던 animator.speed를 원래대로 되돌립니다 - 안 그러면 공격이 끝난 뒤
        // 이동/대기 애니메이션까지 계속 빨라진 채로 남습니다.
        if (animator != null) animator.speed = 1f;

        // 안전장치: 대시/스킬 캔슬이나 타임아웃으로 끝나서 그 타격의 OnHitboxClose 이벤트가
        // 아예 호출되지 못한 경우에도, 열려있던 판정을 확실히 닫아줍니다.
        if (attackArea != null) attackArea.CloseAllHitboxes();
    }

    // ------------------------------------------------------------------
    // 피격 / 사망 - PlayerStats.TakeDamage()가 호출합니다.
    // ------------------------------------------------------------------

    /// <summary>PlayerStats 전용 진입점입니다. 데미지를 받았지만 아직 죽지는 않았을 때 호출하세요.
    /// 진행 중이던 공격/스킬/대시를 즉시 끊고 Hit 모션을 재생하며, hitStunDuration 동안 모든 조작을
    /// 막습니다. 이미 사망한 상태면 아무것도 하지 않습니다.</summary>
    public void TakeHit()
    {
        if (isDead) return;

        // 진행 중이던 다른 동작을 즉시 끊고 피격 상태로 전환합니다.
        if (isAttacking) EndAttackMotion();
        if (isUsingSkill)
        {
            isUsingSkill = false;
            if (attackArea != null) attackArea.CloseAllHitboxes();
        }
        isDashing = false;
        EndUltInvincibilityGraceIfActive(); // 무적 여유 시간이 진행 중이었다면 취소합니다 - 맞았으니 즉시 풀어야 합니다.
        ExitInvincible(); // 구르거나 필살기를 쓰는 중에 맞아서 여기로 들어온 거라면(정상 경로로는 거의 없겠지만) 레이어를 복구합니다.
        EndUltCameraIfActive(); // 필살기 도중 피격으로 끊긴 거라면 카메라 연출도 즉시 게임플레이 카메라로 되돌립니다.
        EndUltChargeVfxIfActive(); // 차지 VFX도 함께 정리합니다.

        isHit = true;
        hitStunTimer = hitStunDuration;

        if (animator != null) animator.SetTrigger(HitParam);
    }

    /// <summary>PlayerStats 전용 진입점입니다. 체력이 0이 되어 사망했을 때 호출하세요. Die 모션을
    /// 재생하고, 이후 모든 조작(이동/회전/공격/스킬/대시/피격)을 영구히 막습니다. 자신(과 자식
    /// 오브젝트)의 모든 Collider(CharacterController 포함)도 꺼서, 죽은 뒤에는 몬스터의 공격 판정
    /// (OverlapSphere, 투사체 트리거 등)에 더 이상 걸리지 않습니다 - 몬스터의 DisableColliders()와
    /// 같은 이유입니다. 부활/리스폰 로직은 아직 없으니 필요하면 이어서 만들어드릴 수 있습니다.
    /// 이미 사망한 상태면 아무것도 하지 않습니다.</summary>
    public void Die()
    {
        if (isDead) return;

        isDead = true;
        isHit = false;
        isAttacking = false;
        isUsingSkill = false;
        isDashing = false;
        EndUltInvincibilityGraceIfActive(); // 무적 여유 시간이 진행 중이었다면 취소합니다 - 사망했으니 즉시 풀어야 합니다.
        ExitInvincible(); // 어차피 DisableColliders()로 콜라이더 자체를 끄지만, 레이어도 깔끔하게 되돌려둡니다.
        EndUltCameraIfActive(); // 필살기 도중 사망했다면 카메라 연출도 즉시 게임플레이 카메라로 되돌립니다.
        EndUltChargeVfxIfActive(); // 차지 VFX도 함께 정리합니다.
        comboIndex = 0;
        comboWindowOpen = false;

        if (attackArea != null) attackArea.CloseAllHitboxes();

        DisableColliders();

        if (animator != null) animator.SetTrigger(DieParam);
    }

    /// <summary>자신(과 자식 오브젝트) 위의 모든 Collider를 꺼서 더 이상 물리 판정에 걸리지 않도록 합니다.
    /// CharacterController도 Collider를 상속하므로 여기 포함됩니다 - 꺼진 뒤에는 CharacterController.Move()를
    /// 더 이상 호출하지 않아야 하므로, Update()의 사망 분기에서도 Move() 호출을 하지 않습니다.</summary>
    private void DisableColliders()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }
    }

    private void LogCombo(string message)
    {
        if (!debugLogCombo) return;
        Debug.Log($"[Combo] t={Time.time:F3} {message}");
    }

    /// <summary>이동 입력 여부를 IsMove Bool 파라미터에 반영합니다. (Idle ↔ Running)
    /// 스킬/기본 공격 콤보 사용 중에는 이동 입력이 있어도 항상 false로 두어 Running 모션과 섞이지 않게 합니다.</summary>
    private void UpdateAnimator(Vector3 moveDirection)
    {
        if (animator == null) return;

        bool isMoving = !isUsingSkill && !isAttacking && !isHit && moveDirection.sqrMagnitude > 0.0001f;
        animator.SetBool(IsMoveParam, isMoving);
    }

    /// <summary>PlayerStats의 CurrentHP/MaxHP를 0~1 비율로 환산해서 UIIngame.SetHPBar()에 넘겨줍니다.
    /// playerStats나 uiIngame이 연결되어 있지 않으면(인스펙터 설정 누락 등) 그냥 넘어가지 않고
    /// NullReferenceException을 그대로 던집니다 - 누락을 조용히 숨기지 않고 콘솔에서 바로 드러나게 하기 위함입니다.</summary>
    private void UpdateHPBar()
    {
        float rate = Mathf.Clamp01(playerStats.CurrentHP / playerStats.MaxHP);
        uiIngame.SetHPBar(rate);
    }

    /// <summary>PlayerStats의 CurrentMP/MaxMP를 0~1 비율로 환산해서 UIIngame.SetMPBar()에 넘겨줍니다.</summary>
    private void UpdateMPBar()
    {
        float rate = Mathf.Clamp01(playerStats.CurrentMP / playerStats.MaxMP);
        uiIngame.SetMPBar(rate);
    }

    /// <summary>스킬 쿨타임과 필살기 에너지/쿨타임 상태를 UIIngame에 반영합니다. 스킬의 skillRate는
    /// 1(방금 씀) → 0(다 씀/사용 가능) 순서로 줄어듭니다. 필살기의 ultEnergyRate(실제 값)는 시간이 아니라
    /// PlayerStats.CurrentEnergy를 기준으로 1(에너지 0) → 0(에너지 가득 참) 순서로 줄어듭니다 -
    /// 게이지 이미지를 그 방향으로 채우면 "닳아 없어지는" 연출이 됩니다.
    /// 다만 실제 값(ultEnergyRate)을 그대로 UI에 넘기면, 기본 공격 한 번에 에너지가 5(=5%)만 올라도
    /// 게이지가 그 프레임에 바로 툭 튀어서 "충전되고 있다"는 느낌이 잘 안 듭니다 - 그래서 실제로
    /// 넘기는 값(displayedUltEnergyRate01)은 매 프레임 목표치를 향해 ultGaugeFillDuration 동안
    /// Mathf.MoveTowards로 서서히 따라잡도록 했습니다.</summary>
    private void UpdateSkillCooldownUI()
    {
        float skillRate = skillCooldown > 0f ? Mathf.Clamp01(skillCooldownTimer / skillCooldown) : 0f;
        uiIngame.SetSkillCooldown(skillRate, Mathf.Max(0f, skillCooldownTimer));

        float ultEnergyRate = playerStats.MaxEnergy > 0f
            ? 1f - Mathf.Clamp01(playerStats.CurrentEnergy / playerStats.MaxEnergy)
            : 0f;

        if (ultGaugeFillDuration > 0f)
        {
            float speed = 1f / ultGaugeFillDuration; // 0→1 전체 구간을 ultGaugeFillDuration초에 따라잡는 속도
            displayedUltEnergyRate01 = Mathf.MoveTowards(displayedUltEnergyRate01, ultEnergyRate, speed * Time.deltaTime);
        }
        else
        {
            displayedUltEnergyRate01 = ultEnergyRate; // 0 이하면 예전처럼 순간 반영
        }

        uiIngame.SetUltCooldown(displayedUltEnergyRate01, Mathf.Max(0f, ultCooldownTimer));
    }

    private void RotateTowards(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;

        float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        currentYaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref currentYawVelocity, rotationSmoothTime);
        transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);
    }

    /// <summary>PlayerTargeting이 감지한 가장 가까운 적이 있으면 그쪽을 즉시(스냅) 바라보도록 회전시킵니다.
    /// 타겟이 없으면 아무것도 하지 않고 지금 바라보던 방향 그대로 공격/스킬이 나갑니다.
    /// 기본 공격의 매 타격, 스킬/필살기 시전 시작 시점에 호출됩니다.</summary>
    private void FaceNearestTargetIfAny()
    {
        if (targeting == null) return;

        Vector3 direction = targeting.GetDirectionToTarget(transform.position);
        if (direction.sqrMagnitude < 0.0001f) return;

        currentYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        currentYawVelocity = 0f; // SmoothDamp 잔여 속도를 지워서 이후 회전이 안 튀도록 합니다.
        transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);
    }

    // 점프용
    private void ApplyGravity()
    {
        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedStickForce;
        }

        verticalVelocity += gravity * Time.deltaTime;
    }
}