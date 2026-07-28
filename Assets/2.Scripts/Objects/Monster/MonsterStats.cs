// ============================================================================
// MonsterStats.cs
// ----------------------------------------------------------------------------
// 몬스터의 전투 스탯(HP, 공격력, 방어력)을 담는 컴포넌트입니다. PlayerStats와 같은 방식
// (기초 수치 × (100% + 추가%))으로 계산하지만, 몬스터는 무기 공격력이나 치명타 스탯이 없어서
// 그 부분만 뺐습니다.
//
//   총 HP    = 기초 HP × (100% + 추가 HP%)
//   총 공격력 = 기초 공격력 × (100% + 추가 공격력%)
//   총 방어력 = 기초 방어력 × (100% + 추가 방어력%)
//
// CalculateDamage()로 계산하는 데미지는 마지막에 damageVariancePercent(기본 1%)만큼 무작위 편차가
// 한 번 더 곱해집니다 (PlayerStats와 동일한 방식) - 완전히 같은 조건이어도 매번 살짝씩 달라집니다.
//
// MonsterFSM/MiddleSlimeBoss가 이 컴포넌트를 필수로 요구합니다([RequireComponent]) - 기존에
// 각자 갖고 있던 maxHealth/currentHealth 필드를 이 컴포넌트로 옮겼습니다. 실제 Hit/Die 상태
// 전환 같은 "행동" 로직은 그대로 MonsterFSM/MiddleSlimeBoss가 담당하고, 이 컴포넌트는 순수하게
// 수치만 담당합니다 (그래서 IDamageable은 여기서 구현하지 않았습니다 - TakeDamage를 받는 진입점은
// 여전히 MonsterFSM/MiddleSlimeBoss이고, 거기서 이 컴포넌트의 TakeDamage(amount)를 호출해
// 수치만 반영한 뒤 Hit/Die 여부를 자기가 판단합니다).
//
// [씬 준비]
//   몬스터 오브젝트에 MonsterFSM을 상속한 스크립트(SlimeFSM 등)나 MiddleSlimeBoss를 붙이면
//   이 컴포넌트가 자동으로 같이 추가됩니다. Base HP/Attack/Defense 값을 인스펙터에서 몬스터별로
//   설정해주세요. (기존에 MonsterFSM/MiddleSlimeBoss 인스펙터에 있던 Max Health 값을 쓰고
//   계셨다면, 그 값을 이 컴포넌트의 Base HP로 옮겨서 다시 입력해주세요 - 필드 위치만 바뀌었을 뿐
//   의미는 같습니다.)
// ============================================================================

using UnityEngine;

public class MonsterStats : MonoBehaviour
{
    [Header("표시 정보")]
    [Tooltip("체력바 등 UI에 표시할 이름입니다. 예: \"슬라임\", \"우드골렘\"")]
    public string displayName = "Monster";
    [Tooltip("체력바 등 UI에 표시할 레벨입니다(예: 보스 체력바의 \"Lv. 25\"). PlayerStats.level과 달리 " +
              "순수하게 표시용 숫자일 뿐, HP/공격력/방어력 계산에는 전혀 영향을 주지 않습니다 - 몬스터는 " +
              "레벨에 따른 스탯 성장 대상이 아닙니다(성장이 필요하면 baseHP 등 기초 스탯 자체를 몬스터마다 " +
              "다르게 설정하세요).")]
    public int level = 1;

    [Header("퀘스트 식별")]
    [Tooltip("퀘스트 Kill 목표(QuestData.Objective.targetMonsterId)가 이 몬스터를 구분하는 데 쓰는 고유 " +
              "ID입니다. LootItemData.itemId와 같은 개념입니다. 예: \"slime\". 비워두면 이 몬스터는 어떤 " +
              "Kill 목표에도 걸리지 않습니다.")]
    public string monsterId;

    [Header("기초 스탯")]
    public float baseHP = 100f;
    public float baseAttackPower = 20f;
    public float baseDefense = 10f;

    [Header("추가 스탯 (%) - 버프/디버프 등으로 늘어나는 보너스")]
    [Tooltip("HP/공격력/방어력에 곱해지는 보너스(%)입니다. 예: 20을 넣으면 해당 기초 수치의 +20%가 합산됩니다.")]
    public float bonusHPPercent = 0f;
    public float bonusAttackPercent = 0f;
    public float bonusDefensePercent = 0f;

    [Header("피격 사운드 (이 몬스터가 [플레이어에게] 맞았을 때)")]
    [Tooltip("플레이어의 공격에 맞았을 때 재생할 타격 효과음 이름입니다(Resources/SFX/ 아래 클립 이름과 " +
              "일치해야 함). 몬스터 종류마다 서로 다른 타격음(예: 슬라임은 질척한 소리, 골렘은 둔탁한 " +
              "소리)을 내고 싶을 때 여기서 설정하세요. AttackHitbox.hitSfxName(공격 모션 쪽에 설정해둔 " +
              "타격음)과 둘 다 채워져 있으면 이 몬스터 쪽 값이 우선하고, 이게 비어있을 때만 " +
              "AttackHitbox.hitSfxName이 대신 재생됩니다. 둘 다 비어있으면 타격음 없이 데미지만 들어갑니다.")]
    public string hitSfxName;

    [Header("공격 명중 사운드 (이 몬스터가 [플레이어를] 맞혔을 때)")]
    [Tooltip("반대 방향입니다 - 위 hitSfxName은 '이 몬스터가 맞았을 때'이고, 이건 '이 몬스터의 공격이 " +
              "플레이어에게 적중했을 때' 재생할 효과음입니다(Resources/SFX/ 아래 클립 이름과 일치해야 함). " +
              "몬스터 종류마다 다른 공격 타격음(예: 슬라임의 물컹한 타격, 골렘의 묵직한 타격)을 내고 싶을 " +
              "때 여기서 설정하세요. MonsterAttackHitbox를 쓰는 일반 몬스터와 MiddleSlimeBoss의 Swing/Wave " +
              "공격 모두 이 값을 재생합니다. 비워두면 타격음 없이 데미지만 들어갑니다.")]
    public string attackHitSfxName;

    [Header("데미지 랜덤 편차")]
    [Tooltip("CalculateDamage()가 계산한 최종 데미지에 마지막으로 적용하는 무작위 편차(%). PlayerStats와 " +
              "동일한 방식입니다 - 예: 1이면 최종 데미지가 ±1% 범위에서 매번 조금씩 다르게 나옵니다.")]
    public float damageVariancePercent = 1f;

    [Header("처치 보상")]
    [Tooltip("이 몬스터를 처치하면 지급되는 총 경험치입니다. LootDropper.DropRewards()가 이 값을 " +
              "경험치 오브젝트 여러 개로 나눠서 드롭합니다.")]
    public int expReward = 10;
    [Tooltip("이 몬스터를 처치하면 지급되는 총 골드입니다. LootDropper.DropRewards()가 이 값을 " +
              "골드 오브젝트 여러 개로 나눠서 드롭합니다.")]
    public int goldReward = 5;

    private float currentHP;

    // ------------------------------------------------------------------
    // 계산된 총 스탯 - 캐싱하지 않고 매번 bonus 값을 반영해서 계산합니다.
    // ------------------------------------------------------------------

    public float TotalHP => baseHP * (1f + bonusHPPercent / 100f);
    public float TotalAttackPower => baseAttackPower * (1f + bonusAttackPercent / 100f);
    public float TotalDefense => baseDefense * (1f + bonusDefensePercent / 100f);

    public float CurrentHP => currentHP;
    public float MaxHP => TotalHP;

    private void Awake()
    {
        currentHP = TotalHP;
    }

    /// <summary>순수하게 수치만 깎습니다 (0 밑으로 내려가지 않음). Hit/Die 상태 전환 여부는
    /// 이 컴포넌트를 갖고 있는 MonsterFSM/MiddleSlimeBoss가 반환된 CurrentHP를 보고 직접 판단합니다.</summary>
    public void TakeDamage(float amount)
    {
        currentHP = Mathf.Max(0f, currentHP - amount);
    }

    /// <summary>회복 스킬 등에서 쓸 수 있는 함수입니다 (몬스터의 자가 회복, 힐러 몬스터 등). MaxHP를 넘지 않습니다.</summary>
    public void Heal(float amount)
    {
        currentHP = Mathf.Min(TotalHP, currentHP + amount);
    }

    /// <summary>이 몬스터가 플레이어 등에게 가하는 데미지를 계산합니다. 몬스터는 치명타 스탯이 없어서
    /// 항상 고정 데미지입니다(DamageResult.isCrit는 항상 false).
    /// 데미지 = (총 공격력 - targetDefense) × (damagePercent / 100). targetDefense를 생략하면
    /// 0으로 계산됩니다.</summary>
    public DamageResult CalculateDamage(float damagePercent, float targetDefense = 0f)
    {
        float damage = Mathf.Max(0f, TotalAttackPower - targetDefense) * (damagePercent / 100f);
        damage = ApplyDamageVariance(damage);
        return new DamageResult(damage, false);
    }

    /// <summary>최종 데미지에 ±damageVariancePercent% 범위의 무작위 편차를 곱해줍니다. damageVariancePercent가
    /// 0 이하면 편차 없이 원래 값을 그대로 돌려줍니다.</summary>
    private float ApplyDamageVariance(float damage)
    {
        if (damageVariancePercent <= 0f) return damage;

        float variance = damageVariancePercent / 100f;
        float multiplier = 1f + Random.Range(-variance, variance);
        return damage * multiplier;
    }
}