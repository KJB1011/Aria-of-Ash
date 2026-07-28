// ============================================================================
// MonsterAttackHitbox.cs
// ----------------------------------------------------------------------------
// 몬스터의 근접 공격 판정용 히트박스. Player의 AttackHitbox와 완전히 동일한 구조입니다 -
// MonsterAttackArea(태그: MonsterAttackArea) 아래, 그 몬스터의 각 근접 모션 이름을 딴
// 자식 오브젝트(예: "BodyAttack")에 붙입니다. 그 오브젝트에는 BoxCollider가 하나 있어야
// 하고(자동으로 Is Trigger로 맞춰줍니다), 평소에는 꺼져있다가 MonsterAttackAreaController가
// 이름으로 찾아서 Open()/Close()를 호출해줄 때만 켜집니다. 인스펙터에서 직접 켜고 끌 필요는
// 없습니다 - 항상 꺼진 상태로 시작합니다.
//
// [데미지 계산 - Player의 AttackHitbox와 동일한 방식]
// 고정 데미지 대신, 부모 쪽(몬스터 루트)에서 자동으로 찾은 MonsterStats.CalculateDamage()를
// 호출해서 최종 데미지를 계산합니다 - (몬스터의 총 공격력 - 맞은 플레이어의 총 방어력) ×
// (이 히트박스의 damagePercent%)입니다. Attack1/Attack2/BodyAttack 등 모션별로 이 damagePercent만
// 다르게 설정하면 됩니다(원신 등에서 "공격력의 80%" 식으로 표기하는 것과 같은 방식). 몬스터는
// 치명타 스탯이 없어서 항상 고정 데미지입니다(DamageResult.isCrit는 항상 false). 맞는 대상은 항상
// 플레이어이므로 데미지 숫자는 항상 DamageNumberTeam.Player로 표시됩니다.
//
// [중요] OnTriggerEnter가 실제로 호출되려면, 이 오브젝트 쪽이나 상대(플레이어) 쪽 중 적어도
// 한 곳에는 Rigidbody가 있어야 하는 게 유니티 물리 규칙입니다. 플레이어 쪽에는 이미 자신의
// AttackHitbox가 동작하도록 Player 루트에 Kinematic Rigidbody를 추가해뒀을 테니, 몬스터 쪽에서
// 따로 Rigidbody를 추가하지 않아도 그걸로 충분합니다.
//
// [히트 VFX / 데미지 숫자]
// hitVfxName에 이름을 넣어두면, 실제로 데미지가 들어가는 순간 VFXManager.Instance.Play()로
// 그 이름의 이펙트를 재생합니다(Assets/Resources/VFX/ 아래에 그 이름의 프리팹이 있어야 합니다).
// 재생 위치는 맞은 플레이어 콜라이더에서 이 히트박스와 가장 가까운 지점입니다. 비워두면 VFX
// 없이 데미지만 들어갑니다. 데미지가 들어가는 순간 DamageNumberManager.Instance.Show()로 맞은
// 지점 위에 데미지 숫자도 띄웁니다.
//
// [히트 SFX - 몬스터별로 설정]
// 이 몬스터의 공격이 플레이어에게 적중하는 순간, 부모 쪽 MonsterStats.attackHitSfxName(Resources/SFX/
// 아래 클립 이름)이 채워져 있으면 SoundManager.Instance.PlaySFX()로 같은 위치에서 재생합니다 - 몬스터
// 종류마다 이 값을 다르게 설정하면 각자 다른 공격 타격음을 낼 수 있습니다. 비워두면 소리 없이 데미지만
// 들어갑니다.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class MonsterAttackHitbox : MonoBehaviour
{
    [Tooltip("이 판정의 대상 레이어. 보통 Player 레이어를 지정하세요.")]
    public LayerMask hitMask;
    [Tooltip("이 판정의 데미지 배율(%). 최종 데미지는 MonsterStats.CalculateDamage(damagePercent)가 계산합니다 " +
              "(몬스터의 총 공격력 × damagePercent%, 맞은 플레이어의 총 방어력만큼 감산). 예: 80을 넣으면 " +
              "공격력의 80%가 데미지입니다.")]
    public float damagePercent = 100f;
    [Tooltip("데미지가 들어가는 순간 재생할 VFX 이름 (Resources/VFX/ 아래 프리팹 이름과 일치해야 함). " +
              "비워두면 VFX 없이 데미지만 적용됩니다. 예: \"FX_SmallSlime_Hit\"")]
    public string hitVfxName;
    [Tooltip("데미지 숫자가 뜨는 위치를 맞은 지점에서 위로 얼마나 띄울지(미터).")]
    public float damageNumberHeightOffset = 0.8f;

    private BoxCollider boxCollider;
    private MonsterStats monsterStats;
    private readonly HashSet<Collider> alreadyHit = new HashSet<Collider>();

    private void Awake()
    {
        EnsureCollider();

        monsterStats = GetComponentInParent<MonsterStats>();
        if (monsterStats == null)
        {
            Debug.LogWarning($"[MonsterAttackHitbox] {name}: 부모 쪽에서 MonsterStats를 찾지 못했습니다. 몬스터 " +
                              "오브젝트에 MonsterStats가 있는지 확인해주세요. 지금 상태로는 이 판정의 데미지가 " +
                              "항상 0으로 들어갑니다.", this);
        }
    }

    // MonsterAttackAreaController.Start()가 모든 Awake()가 끝난 뒤에 등록하지만, 혹시라도 다른
    // 스크립트가 더 이르게 Open()/Close()를 호출하는 경우까지 대비한 이중 안전장치입니다.
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
        boxCollider.enabled = true;
    }

    public void Close()
    {
        EnsureCollider();
        boxCollider.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & hitMask.value) == 0) return;
        if (!alreadyHit.Add(other)) return; // 이번 판정 구간에서 이미 맞춘 대상이면 무시합니다.

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable == null) return;

        // 맞는 순간의 스탯을 기준으로 계산해서, 버프/디버프 등으로 공격력/방어력이 바뀌어도 항상
        // 최신 값이 반영됩니다.
        PlayerStats playerStats = other.GetComponentInParent<PlayerStats>();
        float targetDefense = playerStats != null ? playerStats.TotalDefense : 0f;

        DamageResult result = monsterStats != null ? monsterStats.CalculateDamage(damagePercent, targetDefense) : default;
        damageable.TakeDamage(result.damage);

        PlayHitVfx(other);
        PlayHitSfx(other);
        ShowDamageNumber(other, result.damage);
    }

    private void PlayHitVfx(Collider hitCollider)
    {
        if (string.IsNullOrEmpty(hitVfxName)) return;

        Vector3 vfxPosition = hitCollider.ClosestPoint(transform.position);
        VFXManager.Instance.Play(hitVfxName, vfxPosition, transform.rotation);
    }

    /// <summary>부모 쪽 MonsterStats.attackHitSfxName(이 몬스터가 플레이어를 맞혔을 때 낼 소리)이
    /// 설정되어 있으면 재생합니다. 몬스터마다 이 값을 다르게 설정하면 각자 다른 공격 타격음을 낼 수
    /// 있습니다.</summary>
    private void PlayHitSfx(Collider hitCollider)
    {
        if (monsterStats == null || string.IsNullOrEmpty(monsterStats.attackHitSfxName)) return;

        Vector3 sfxPosition = hitCollider.ClosestPoint(transform.position);
        SoundManager.Instance.PlaySFX(monsterStats.attackHitSfxName, sfxPosition);
    }

    private void ShowDamageNumber(Collider hitCollider, float damage)
    {
        Vector3 position = hitCollider.ClosestPoint(transform.position) + Vector3.up * damageNumberHeightOffset;
        DamageNumberManager.Instance.Show(damage, position, false, DamageNumberTeam.Player);
    }
}