// ============================================================================
// AttackHitbox.cs
// ----------------------------------------------------------------------------
// AttackArea(태그: AttackArea) 아래, Attack1/Attack2/Attack3/Skill/UltSkill 등
// 각 모션 이름을 딴 오브젝트에 붙이는 컴포넌트입니다. 그 오브젝트에는 BoxCollider가
// 하나 있어야 하고(자동으로 Is Trigger로 맞춰줍니다), 평소에는 꺼져있다가
// AttackAreaController가 이름으로 찾아서 Open()/Close()를 호출해줄 때만 켜집니다.
// 인스펙터에서 직접 켜고 끌 필요는 없습니다 - 항상 꺼진 상태로 시작합니다.
//
// [중요] OnTriggerEnter가 실제로 호출되려면, 이 오브젝트 쪽이나 상대(적) 쪽 중
// 적어도 한 곳에는 Rigidbody가 있어야 하는 게 유니티 물리 규칙입니다. Player는
// CharacterController를 쓰고 있어서 기본적으로 Rigidbody가 없을 텐데, 이 경우 Player
// 루트(혹은 AttackArea 오브젝트)에 Rigidbody를 추가하고 Is Kinematic을 체크해두세요.
// 그러면 이 히트박스들을 포함해 하위의 모든 Collider가 하나의 물리 바디로 묶여서
// 트리거 판정이 정상적으로 발생합니다 (Is Kinematic이라 CharacterController의 이동에는
// 영향을 주지 않습니다).
//
// [데미지 계산]
// 고정 데미지 값을 넣는 대신, Player 오브젝트에 있는 PlayerStats.CalculateDamage()를 호출해서
// 최종 데미지를 계산합니다 - (PlayerStats의 총 공격력 - 맞은 대상의 총 방어력) × (이 오브젝트의
// damagePercent%)를 기본으로, 치명타 확률/피해량까지 반영됩니다. 대상 방어력은 맞은 콜라이더에서
// MonsterStats를 찾아서 가져오고, 없으면(방어력 스탯이 없는 대상이면) 0으로 계산합니다.
// Attack1/Attack2/Attack3/Skill/UltSkill 등 모션별로 이 damagePercent만 다르게 설정해주시면 됩니다
// (원신 등에서 "스킬 데미지 123% 공격력"이라고 표기하는 것과 같은 방식입니다).
//
// [히트 VFX]
// hitVfxName에 이름을 넣어두면, 실제로 데미지가 들어가는 순간 VFXManager.Instance.Play()로
// 그 이름의 이펙트를 재생합니다(Assets/Resources/VFX/ 아래에 그 이름의 프리팹이 있어야 합니다).
// 재생 위치는 맞은 대상 콜라이더의 바운딩 박스 중심(GetHitPosition() 참고)이라 항상 몬스터의 몸통
// 근처에서 안정적으로 재생되고, 회전은 이 히트박스 오브젝트의 회전을 그대로 씁니다. 위치 계산은
// TakeDamage()를 호출하기 "전에" 미리 끝내둡니다 - 이 타격이 상대를 죽이는 마지막 타격이면
// TakeDamage() 안에서 곧바로 Die()가 실행되어 상대의 Collider를 꺼버리는데(MonsterFSM.DisableColliders),
// 그 뒤에 위치를 계산하면 어긋나기 때문입니다. 비워두면 VFX 없이 데미지만 들어갑니다.
// Attack1/Attack2/Attack3마다 다른 이펙트를 쓰고 싶다면 각 오브젝트에서 이 값만 다르게 설정하면
// 됩니다.
// (참고 - 시행착오: 처음엔 Collider.ClosestPoint(이 히트박스 위치)를 썼는데 오목한 Mesh Collider에는
// 지원되지 않아 입력값을 그대로 돌려주는 문제가, 그 다음 ClosestPointOnBounds로 바꾸니 근접 공격처럼
// 히트박스가 몬스터의 XZ 범위 안까지 파고들면 그 축은 클램프 안 되고 Y만 클램프되어 "플레이어
// 발밑"에서 나오는 문제가 있었습니다 - 그래서 지금은 아예 이 히트박스의 위치를 계산에 쓰지 않고
// bounds.center만 사용합니다.)
//
// [데미지 숫자 HUD]
// 데미지가 들어가는 순간 DamageNumberManager.Instance.Show()로 맞은 지점 위에 데미지 숫자를
// 띄웁니다(오브젝트 풀링 사용, DamageNumberManager.cs 참고). 치명타면 CalculateDamage가 돌려준
// isCrit 값을 그대로 넘겨서 더 크고 다른 색으로 표시됩니다. 항상 몬스터가 맞은 것으로
// (DamageNumberTeam.Enemy) 표시됩니다 - 이 컴포넌트는 플레이어의 공격 판정이기 때문입니다.
//
// [에너지 충전]
// energyOnHit에 넣은 값만큼 PlayerStats.AddEnergy()를 호출해서 필살기 에너지를 충전합니다.
// Open()된 뒤 처음으로 대상을 맞힌 순간에만 한 번 충전되고(energyChargedThisOpen), 광역 판정으로
// 여러 대상을 동시에 맞혀도 중복 충전되지 않습니다. 기본 공격 1타/2타=5, 3타=10, 스킬=30처럼
// Attack1/Attack2/Attack3/Skill 오브젝트마다 이 값을 다르게 설정하세요. 필살기(UltSkill)는
// 에너지를 오히려 소모해서 쓰는 쪽이라 0으로 두면 됩니다(소모는 PlayerController가 필살기를
// 쓰는 순간 PlayerStats.SpendEnergy()로 따로 처리합니다).
//
// [기본공격강화 / 패시브강화 - SkillInfo 트리]
// isBasicAttack을 켜두면(Attack1/Attack2/Attack3 오브젝트에서만 켜세요 - Skill/UltSkill은 꺼둔 채로
// 두세요) 이 히트박스가 "기본 공격"으로 취급됩니다. PlayerStats.HasBasicAttackUpgrade가 켜져있으면
// 이 히트박스의 데미지가 +30%(damagePercent × 1.3) 적용되고, PlayerStats.HasPassiveUpgrade가
// 켜져있으면 적중할 때마다 PlayerController.ReduceSkillCooldown()을 호출해서 우클릭 스킬의 쿨타임을
// PlayerStats.passiveSkillCooldownReductionOnHit(기본 0.2초)만큼 앞당깁니다. 공격속도 +30%(기본공격강화)는
// 여기가 아니라 PlayerController.StartAttackHit()/EndAttackMotion()에서 animator.speed로 처리합니다.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class AttackHitbox : MonoBehaviour
{
    [Tooltip("적으로 판정할 레이어. 몬스터들이 속한 레이어를 지정하세요.")]
    public LayerMask enemyMask;
    [Tooltip("이 모션의 데미지 배율(%). 최종 데미지는 PlayerStats.CalculateDamage(damagePercent)가 계산합니다 " +
              "(총 공격력 × damagePercent%, 치명타 확률/피해량 반영). 예: 80을 넣으면 공격력의 80%가 기본 데미지입니다.")]
    public float damagePercent = 100f;
    [Tooltip("데미지가 들어가는 순간 재생할 VFX 이름 (Resources/VFX/ 아래 프리팹 이름과 일치해야 함). " +
              "비워두면 VFX 없이 데미지만 적용됩니다. 예: \"FX_Player_Slash\"")]
    public string hitVfxName;
    [Tooltip("데미지 숫자가 뜨는 위치를 맞은 지점에서 위로 얼마나 띄울지(미터). 발밑이 아니라 " +
              "몸통 근처에서 뜨도록 하기 위한 값입니다.")]
    public float damageNumberHeightOffset = 0.8f;
    [Tooltip("이 판정이 적중했을 때 충전되는 필살기 에너지량입니다. 기본 공격 1타/2타=5, 3타=10, 스킬=30처럼 " +
              "모션별로 다르게 설정하세요. Open()된 뒤 여러 대상을 동시에 맞혀도(광역 공격 등) 한 번만 " +
              "충전됩니다. 필살기(UltSkill)처럼 에너지를 소모해서 쓰는 모션은 0으로 두세요.")]
    public int energyOnHit = 0;
    [Tooltip("이 히트박스가 '기본 공격'(Attack1/Attack2/Attack3)인지 여부입니다. Skill/UltSkill 오브젝트에서는 " +
              "꺼둔 채로 두세요. 켜두면 SkillInfo의 기본공격강화(데미지 +30%)/패시브강화(적중 시 스킬 쿨타임 " +
              "-0.2초) 효과가 이 히트박스에 적용됩니다.")]
    public bool isBasicAttack = false;

    private BoxCollider boxCollider;
    private PlayerStats playerStats;
    private PlayerController playerController;
    private readonly HashSet<Collider> alreadyHit = new HashSet<Collider>();
    private bool energyChargedThisOpen;

    private void Awake()
    {
        EnsureCollider();

        playerStats = GetComponentInParent<PlayerStats>();
        if (playerStats == null)
        {
            Debug.LogWarning($"[AttackHitbox] {name}: 부모 쪽에서 PlayerStats를 찾지 못했습니다. Player 오브젝트에 " +
                              "PlayerStats를 추가해주세요. 지금 상태로는 이 판정의 데미지가 항상 0으로 들어갑니다.", this);
        }

        // 패시브강화(적중 시 스킬 쿨타임 감소)를 위해서만 필요합니다 - isBasicAttack이 꺼져있는(Skill/UltSkill)
        // 히트박스에서는 못 찾아도 경고 없이 조용히 넘어갑니다(애초에 쓰지 않으므로).
        playerController = GetComponentInParent<PlayerController>();
        if (playerController == null && isBasicAttack)
        {
            Debug.LogWarning($"[AttackHitbox] {name}: 부모 쪽에서 PlayerController를 찾지 못했습니다. " +
                              "패시브강화(적중 시 스킬 쿨타임 감소)가 동작하지 않습니다.", this);
        }
    }

    // AttackAreaController.Start()가 이제 모든 Awake()가 끝난 뒤에 실행되도록 고쳐놨지만,
    // 혹시라도 다른 스크립트가 더 이르게(예: 다른 Awake()에서) 이 컴포넌트의 Open()/Close()를
    // 호출하는 경우까지 대비해 boxCollider가 비어있으면 그 자리에서 바로 채워주는
    // 이중 안전장치입니다. 정상적인 흐름에서는 Awake()에서 이미 채워져 있어 실제로는
    // 아무 일도 하지 않습니다.
    private void EnsureCollider()
    {
        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider>();
            boxCollider.isTrigger = true;
            boxCollider.enabled = false;
        }
    }

    /// <summary>이 판정을 켭니다. 이전에 맞은 대상 기록도 초기화해서, 새로 열릴 때마다 다시 맞을 수 있게 합니다.</summary>
    public void Open()
    {
        EnsureCollider();
        alreadyHit.Clear();
        energyChargedThisOpen = false;
        boxCollider.enabled = true;
    }

    public void Close()
    {
        EnsureCollider();
        boxCollider.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & enemyMask.value) == 0) return;
        if (!alreadyHit.Add(other)) return; // 이번 판정 구간에서 이미 맞춘 대상이면 무시합니다.

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable == null) return;

        // 맞는 순간의 스탯을 기준으로 계산해서, 버프/디버프 등으로 공격력/방어력이 바뀌어도 항상 최신 값이
        // 반영됩니다. 치명타 확률/피해량도 CalculateDamage 안에서 함께 굴려서 반영됩니다.
        MonsterStats targetStats = other.GetComponentInParent<MonsterStats>();
        float targetDefense = targetStats != null ? targetStats.TotalDefense : 0f;

        // 기본공격강화(SkillInfo)가 해제되어 있고 이 히트박스가 기본 공격이면 데미지 +30%를 적용합니다.
        float effectiveDamagePercent = damagePercent;
        if (isBasicAttack && playerStats != null && playerStats.HasBasicAttackUpgrade)
        {
            effectiveDamagePercent *= 1.3f;
        }

        // 맞은 지점은 데미지를 넣기 "전에" 미리 계산해둡니다 - 이번 타격이 상대를 죽이는 마지막 타격이면
        // TakeDamage() 안에서 곧바로 Die()가 실행되어 상대의 모든 Collider를 꺼버리는데(MonsterFSM.DisableColliders),
        // 그 뒤에 계산하면 콜라이더가 이미 꺼진 상태라 위치 계산이 어긋나서 이펙트/데미지 숫자가 몬스터가 아니라
        // 공격자(플레이어) 발밑 근처에서 나오는 것처럼 보이는 문제가 있었습니다.
        Vector3 hitPosition = GetHitPosition(other);

        DamageResult result = playerStats != null ? playerStats.CalculateDamage(effectiveDamagePercent, targetDefense) : default;
        damageable.TakeDamage(result.damage);

        ChargeEnergyOnce();
        ReduceSkillCooldownIfPassiveUpgraded();

        PlayHitVfx(hitPosition);
        ShowDamageNumber(hitPosition, result.damage, result.isCrit);
    }

    /// <summary>패시브강화(SkillInfo)가 해제되어 있고 이 히트박스가 기본 공격이면, 적중할 때마다 우클릭
    /// 스킬(파이어볼)의 쿨타임을 PlayerStats.passiveSkillCooldownReductionOnHit만큼 앞당깁니다. 한 번의
    /// 판정 구간에서 여러 대상을 동시에 맞혀도(광역 판정 등) 대상 수만큼 중복 적용됩니다 - 에너지 충전과
    /// 달리 "적중당" 감소이므로 의도한 동작입니다.</summary>
    private void ReduceSkillCooldownIfPassiveUpgraded()
    {
        if (!isBasicAttack || playerStats == null || playerController == null) return;
        if (!playerStats.HasPassiveUpgrade) return;

        playerController.ReduceSkillCooldown(playerStats.passiveSkillCooldownReductionOnHit);
    }

    /// <summary>이 판정이 열린 뒤(Open()) 처음으로 대상을 맞혔을 때만 필살기 에너지를 충전합니다.
    /// 한 번의 판정 구간에서 여러 대상을 동시에 맞혀도(광역 공격 등) 에너지는 한 번만 들어갑니다.</summary>
    private void ChargeEnergyOnce()
    {
        if (energyChargedThisOpen) return;
        if (energyOnHit <= 0 || playerStats == null) return;

        playerStats.AddEnergy(energyOnHit);
        energyChargedThisOpen = true;
    }

    private void PlayHitVfx(Vector3 hitPosition)
    {
        if (string.IsNullOrEmpty(hitVfxName)) return;

        // 회전은 히트박스 자신의 회전(각 Attack 모션마다 스윙 방향에 맞춰 설정해둔 값)을 그대로 사용합니다.
        VFXManager.Instance.Play(hitVfxName, hitPosition, transform.rotation);
    }

    private void ShowDamageNumber(Vector3 hitPosition, float damage, bool isCrit)
    {
        Vector3 position = hitPosition + Vector3.up * damageNumberHeightOffset;
        DamageNumberManager.Instance.Show(damage, position, isCrit, DamageNumberTeam.Enemy);
    }

    /// <summary>맞은 대상(hitCollider) 위의 재생 위치를 계산합니다.
    /// [시행착오] 처음엔 Collider.ClosestPoint(이 히트박스의 위치)를 썼는데, 오목한(Non-Convex) Mesh
    /// Collider에는 지원되지 않아 입력값(플레이어 쪽 위치)을 그대로 돌려줘버리는 문제가 있었습니다.
    /// 그래서 ClosestPointOnBounds로 바꿨는데, 이번엔 근접 공격처럼 히트박스가 몬스터의 가로/세로(XZ)
    /// 범위 안까지 파고드는 경우 그 축은 클램프되지 않고 입력값(플레이어 쪽 XZ) 그대로 남아버리고
    /// 세로(Y)만 몬스터의 바운딩 박스 높이 범위로 클램프되어서, 결과적으로 "플레이어 발밑"(수평은
    /// 플레이어, 수직만 몬스터의 바닥 높이)에서 이펙트가 나오는 문제가 있었습니다. 그래서 아예 이
    /// 히트박스의 위치는 계산에 쓰지 않고, 맞은 대상 콜라이더의 바운딩 박스 중심(bounds.center)을
    /// 그대로 씁니다 - 항상 몬스터의 몸통 한가운데 근처에서 안정적으로 나옵니다.</summary>
    private Vector3 GetHitPosition(Collider hitCollider)
    {
        return hitCollider.bounds.center;
    }
}