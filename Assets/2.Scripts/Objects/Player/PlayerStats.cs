// ============================================================================
// PlayerStats.cs
// ----------------------------------------------------------------------------
// 플레이어의 전투 스탯 시스템입니다 (레벨, HP, MP, 공격력, 방어력, 치명타 확률/피해량).
// 원신처럼 "기초 수치"와 "그 기초 수치에 합산되는 %"를 분리해서, 최종 값은 이 컴포넌트가
// 매번 계산해서 내어줍니다 - 즉 bonus 값(장비/버프 등으로 나중에 바뀔 값)만 바꿔주면 총 스탯이
// 자동으로 다시 계산됩니다. 특정 시점의 값을 캐싱해서 들고 있지 않습니다.
//
// [계산 공식]
//   총 HP    = (기초 HP + hpGrowthPerLevel × (레벨 - 1)) × (100% + 추가 HP%)
//   총 공격력 = (기초 공격력 + attackGrowthPerLevel × (레벨 - 1) + 기초 무기 공격력) × (100% + 추가 공격력%)
//   총 방어력 = (기초 방어력 + defenseGrowthPerLevel × (레벨 - 1)) × (100% + 추가 방어력%)
//   총 치명타 확률/피해량 = 기초값 + 추가값 (이 둘은 배율이 아니라 그대로 더해집니다)
//   총 MP는 별도 %가산 공식이 없어서 baseMP를 그대로 최대치로 사용합니다.
//
// [레벨업 스탯 성장]
//   레벨이 1 오를 때마다 HP +30, 공격력 +2, 방어력 +2가 "기초 수치"에 누적으로 더해집니다
//   (그 뒤 추가%가 다시 곱해지므로, 장비/버프로 늘어난 추가 공격력% 등이 있으면 레벨업 효과도 함께
//   증폭됩니다). 레벨 1이면 성장치가 0이라 기초 스탯 그대로입니다. 최대 레벨은 maxLevel(기본 50)로
//   제한되며, level 값은 인스펙터에서 바꾸거나 LevelUp()/SetLevel()을 호출해도 항상 1~maxLevel
//   범위로 자동 클램프됩니다. 몬스터는 이 레벨업 성장 대상이 아닙니다 - MonsterStats는 그대로입니다.
//
// [데미지 공식] - CalculateDamage() 참고
//   데미지 = (가해자의 총 공격력 - 대상의 방어력) × (기본 공격/스킬의 데미지%)
//   여기에 치명타가 뜨면 × (100% + 총 치명타 피해량%)를 추가로 곱합니다.
//   치명타 여부는 총 치명타 확률(%)을 굴려서 판정합니다.
//   지금은 몬스터 쪽에 방어력 스탯이 없어서 targetDefense를 안 넘기면 0으로 계산됩니다 -
//   나중에 몬스터에도 방어력이 생기면 그 값을 그대로 넘겨주시면 방어력 감산이 적용됩니다.
//   마지막으로 damageVariancePercent(기본 1%)만큼 무작위 편차를 한 번 더 곱해서, 완전히 같은
//   조건이어도 매번 데미지가 살짝씩 달라지도록 했습니다 (예: 100 데미지 → 99~101 사이에서 무작위).
//
// [필살기 에너지]
//   기본 공격/스킬이 적을 맞혔을 때 충전되는 것(각 AttackHitbox의 energyOnHit 값만큼, AttackHitbox.cs
//   참고)에 더해, 주인공 패시브로 시간에 따라 서서히 자연 회복도 됩니다(energyRegenPerSecond, HP/MP와
//   같은 방식 - Update()에서 매 프레임 Regenerate() 호출). 광역 공격으로 여러 대상을 동시에 맞혀도
//   판정 하나당 한 번만 충전됩니다. maxEnergy(기본 100)를 넘지 않으며, 필살기를 사용하면
//   PlayerController가 SpendEnergy()를 호출해 소모합니다. 최대치까지 채워야만(기본 100) 필살기를 쓸 수 있습니다.
//
// [경험치/레벨업 - 원신 스타일 한계돌파]
//   레벨업에 필요한 경험치(expToNextLevel)는 더 이상 매 레벨 일정 배율(예: 1.15배)로 늘어나지
//   않고, 레벨 구간(Tier)마다 다른 시작값+증가폭을 쓰는 계단식 공식입니다 (ExpRequiredForLevel() 참고):
//     - 1~19레벨: 1레벨 기준 50, 레벨마다 +20씩 증가 (19레벨 = 50 + 20×18 = 410)
//     - 20~39레벨: 20레벨 기준 1000, 레벨마다 +500씩 증가
//     - 40레벨 이상: 40레벨 기준 50000, 레벨마다 +25000씩 증가
//   또한 breakthroughLevels(기본 {20, 40})에 들어있는 레벨에 도달하면, 경험치가 충분히 쌓여도
//   한계돌파(Breakthrough())를 하기 전까지는 그 이상 레벨업할 수 없습니다 - 경험치는 그 레벨의
//   expToNextLevel에서 멈추고 더 쌓이지 않습니다(원신의 돌파 레벨 상한과 동일한 개념).
//   한계돌파에 필요한 재료/골드 조건 확인 및 차감은 이 스크립트의 책임이 아닙니다 - 나중에 만들
//   캐릭터 창 UI가 그 조건을 먼저 확인/차감한 뒤 Breakthrough()를 호출해주는 구조입니다. 이 스크립트는
//   "지금 돌파 가능한 레벨에 있는지"(IsAwaitingBreakthrough)만 알고 있고, Breakthrough()가 성공하면
//   HP/공격력/방어력에 보너스(%)를 적용하고 막혀있던 레벨업을 이어서 진행합니다.
//
// [주의] baseMP는 요청하신 기본 스탯 목록에 값이 없어서 임시로 100을 넣어뒀습니다.
// 원하는 값으로 인스펙터에서 바꿔주세요.
//
// [씬 준비]
//   Player 오브젝트(PlayerController가 붙어있는 그 오브젝트)에 이 스크립트를 추가하세요.
//   AttackHitbox/PlayerSkillProjectile이 부모 방향으로 이 컴포넌트를 자동으로 찾아
//   CalculateDamage()를 호출합니다.
//
// [피격/사망 모션]
//   TakeDamage()가 호출되면 HP를 깎은 뒤, 그 결과로 죽었으면 같은 오브젝트의
//   PlayerController.Die()를, 아직 살아있으면 PlayerController.TakeHit()을 호출해서 Hit/Die
//   애니메이터 트리거를 쏴줍니다. PlayerController에 hitStunDuration(기본 0.4초)만큼 조작이
//   잠기는 것까지는 구현돼 있지만, 사망 후 부활/리스폰/게임오버 UI 같은 처리는 아직 없습니다.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>CalculateDamage()의 결과값입니다. 치명타 여부를 같이 담아서, 나중에 HUD에서 크리티컬
/// 데미지 폰트/색을 다르게 표시하는 등의 용도로 바로 쓸 수 있게 했습니다.</summary>
public readonly struct DamageResult
{
    public readonly float damage;
    public readonly bool isCrit;

    public DamageResult(float damage, bool isCrit)
    {
        this.damage = damage;
        this.isCrit = isCrit;
    }
}

public class PlayerStats : MonoBehaviour, IDamageable
{
    /// <summary>씬에 Player 하나에만 붙는 컴포넌트라, 다른 스크립트(RewardOrb 등)에서 여기로 바로 접근합니다.</summary>
    public static PlayerStats Instance { get; private set; }

    [Header("레벨")]
    [Tooltip("현재 레벨입니다. 인스펙터에서 직접 바꾸거나 LevelUp()/SetLevel()로 바꿀 수 있으며, 항상 " +
              "1~maxLevel 범위로 자동 클램프됩니다. 레벨이 오를수록 아래 성장치만큼 기초 HP/공격력/방어력이 늘어납니다.")]
    public int level = 1;
    [Tooltip("최대 레벨입니다. level은 이 값을 넘지 않도록 자동으로 제한됩니다.")]
    public int maxLevel = 50;

    [Header("레벨업 성장치 (레벨이 1 오를 때마다 기초 스탯에 누적으로 더해지는 값)")]
    public float hpGrowthPerLevel = 30f;
    public float attackGrowthPerLevel = 2f;
    public float defenseGrowthPerLevel = 2f;

    [Header("경험치")]
    [Tooltip("현재 레벨에서 쌓인 경험치입니다. AddExperience()로 늘어나며, 필요치를 넘으면 자동으로 레벨업합니다 " +
              "(단, 한계돌파가 필요한 레벨이라면 필요치에서 멈추고 더 쌓이지 않습니다 - IsAwaitingBreakthrough 참고).")]
    public int currentExp = 0;
    [Tooltip("다음 레벨로 올라가는 데 필요한 경험치입니다. Awake()와 레벨업/한계돌파 시점마다 " +
              "ExpRequiredForLevel(level)로 자동 재계산되므로, 인스펙터에서 직접 바꿔도 다음 재계산 때 덮어써집니다.")]
    public int expToNextLevel = 50;

    [Header("경험치 - 레벨 구간(Tier)별 공식")]
    [Tooltip("1레벨의 기본 필요 경험치. 1~19레벨 구간은 이 값에서 시작해서 레벨마다 expTier1GrowthPerLevel씩 늘어납니다.")]
    public int expTier1BaseAt1 = 50;
    [Tooltip("1~19레벨 구간에서 레벨마다 늘어나는 필요 경험치.")]
    public int expTier1GrowthPerLevel = 20;
    [Tooltip("breakthroughLevels[0](기본 20레벨)의 기본 필요 경험치. 그 이후 구간은 이 값에서 시작해서 " +
              "레벨마다 expTier2GrowthPerLevel씩 늘어납니다.")]
    public int expTier2BaseAt20 = 1000;
    [Tooltip("20~39레벨 구간에서 레벨마다 늘어나는 필요 경험치.")]
    public int expTier2GrowthPerLevel = 500;
    [Tooltip("breakthroughLevels[1](기본 40레벨)의 기본 필요 경험치. 그 이후(40레벨~)는 이 값에서 시작해서 " +
              "레벨마다 expTier3GrowthPerLevel씩 늘어납니다.")]
    public int expTier3BaseAt40 = 50000;
    [Tooltip("40레벨 이후 구간에서 레벨마다 늘어나는 필요 경험치.")]
    public int expTier3GrowthPerLevel = 25000;

    [Header("한계돌파 (레벨 상한 돌파 - 원신 스타일)")]
    [Tooltip("이 레벨들에 도달하면, 한계돌파(Breakthrough())를 하기 전까지 경험치가 충분해도 더 이상 " +
              "레벨업할 수 없습니다. 기본값은 원신처럼 20/40레벨입니다. 이 배열의 값(오름차순이어야 함)은 " +
              "동시에 경험치 Tier 구간의 경계로도 쓰입니다(ExpRequiredForLevel 참고) - [0]=20이면 " +
              "1~19레벨은 Tier1, 20레벨부터는 Tier2 공식을 씁니다.")]
    public int[] breakthroughLevels = { 20, 40 };
    [Tooltip("한계돌파 1회 성공할 때마다 bonusHPPercent에 누적으로 더해지는 값(%, 기초 HP 기준). " +
              "예: 50이면 돌파 1회당 기초 HP의 50%만큼 총 HP가 늘어납니다.")]
    public float breakthroughHPBonusPercent = 50f;
    [Tooltip("한계돌파 1회 성공할 때마다 bonusAttackPercent에 누적으로 더해지는 값(%, 기초 공격력 기준).")]
    public float breakthroughAttackBonusPercent = 40f;
    [Tooltip("한계돌파 1회 성공할 때마다 bonusDefensePercent에 누적으로 더해지는 값(%, 기초 방어력 기준).")]
    public float breakthroughDefenseBonusPercent = 40f;

    // 이미 한계돌파를 마친 레벨의 집합입니다(예: {20}이면 20레벨 돌파는 끝났고 40레벨 돌파는 아직).
    // 세이브/로드 시스템이 생기면 이 목록도 함께 저장/복원해야 재시작 후에도 돌파 상태가 유지됩니다.
    private readonly HashSet<int> completedBreakthroughLevels = new HashSet<int>();

    [Header("기초 스탯")]
    public float baseHP = 1000f;
    public float baseAttackPower = 100f;
    public float baseWeaponAttackPower = 50f;
    public float baseDefense = 50f;
    [Tooltip("%")]
    public float baseCritRate = 10f;
    [Tooltip("%. 100이면 치명타 시 데미지가 2배(100% + 100%)가 됩니다.")]
    public float baseCritDamage = 100f;
    [Tooltip("기본 스탯 목록에 MP 값이 없어서 임시로 100을 넣어뒀습니다. 원하는 값으로 바꿔주세요. " +
              "지금은 별도 %가산 공식이 없어 이 값이 곧 최대 MP입니다.")]
    public float baseMP = 100f;

    [Header("추가 스탯 (%) - 장비/버프 등으로 늘어나는 보너스")]
    [Tooltip("HP/공격력/방어력에 곱해지는 보너스(%)입니다. 예: 20을 넣으면 해당 기초 수치의 +20%가 합산됩니다.")]
    public float bonusHPPercent = 0f;
    public float bonusAttackPercent = 0f;
    public float bonusDefensePercent = 0f;
    [Tooltip("치명타 확률/피해량 보너스는 배율이 아니라 기초값에 그대로 더해집니다. " +
              "예: 기초 치명타 확률 10 + 이 값 5 = 15%.")]
    public float bonusCritRate = 0f;
    public float bonusCritDamage = 0f;

    [Header("데미지 랜덤 편차")]
    [Tooltip("CalculateDamage()가 계산한 최종 데미지에 마지막으로 적용하는 무작위 편차(%). 예: 1이면 " +
              "완전히 같은 조건(같은 공격력/방어력/치명타 여부)이어도 최종 데미지가 ±1% 범위에서 매번 " +
              "조금씩 다르게 나옵니다. 0으로 두면 편차 없이 항상 같은 값입니다.")]
    public float damageVariancePercent = 1f;

    [Header("자원 자연 회복")]
    public float hpRegenPerSecond = 5f;
    public float mpRegenPerSecond = 10f;

    [Header("필살기 에너지")]
    [Tooltip("필살기 사용에 필요한 에너지의 최대치입니다. 기본 공격/스킬이 적을 맞혔을 때(AttackHitbox.energyOnHit) " +
              "충전되는 것과 별개로, 아래 energyRegenPerSecond만큼 시간에 따라서도 서서히 자연 회복됩니다.")]
    public float maxEnergy = 100f;
    [Tooltip("주인공 패시브 - 필살기 에너지 자연 회복량입니다(초당). HP/MP 자연 회복(hpRegenPerSecond/" +
              "mpRegenPerSecond)과 동일한 방식으로 매 프레임 서서히 채워집니다. 기획 스펙 기준 기본값은 " +
              "2/5초(=초당 0.4)입니다. 0으로 두면 자연 회복 없이 전투 충전(AttackHitbox.energyOnHit)만으로 채워집니다.")]
    public float energyRegenPerSecond = 0.4f;

    [Header("스킬 강화 (SkillInfo 트리 - 4개 강화 노드)")]
    [Tooltip("'패시브 강화'가 해제된 상태에서 기본 공격(AttackHitbox, isBasicAttack = true)이 적중할 때마다 " +
              "우클릭 스킬(파이어볼)의 재사용 대기시간을 이만큼(초) 앞당깁니다. 기획 스펙: 0.2초.")]
    public float passiveSkillCooldownReductionOnHit = 0.2f;

    // 강화 노드 4개(기본공격강화/패시브강화/스킬강화/필살기강화)를 해제했는지 여부입니다. HP/MP처럼 세이브가
    // 필요한 값이지만, 지금은 다른 진행 상태(레벨/한계돌파 등)와 마찬가지로 메모리에만 들고 있습니다 -
    // 나중에 세이브/로드 시스템을 만들 때 같이 저장해주시면 됩니다.
    private bool hasBasicAttackUpgrade;
    private bool hasPassiveUpgrade;
    private bool hasSkillUpgrade;
    private bool hasUltUpgrade;

    private float currentHP;
    private float currentMP;
    private float currentEnergy;

    // TakeDamage()가 Hit/Die 모션 재생을 요청할 대상입니다. 같은 오브젝트에 있는
    // PlayerController를 자동으로 찾습니다 - 없어도(예: 테스트용으로 스탯만 붙여둔 경우) 에러 없이
    // 데미지 계산/HP 반영은 그대로 동작하고, 모션 재생만 건너뜁니다.
    private PlayerController controller;

    // ------------------------------------------------------------------
    // 계산된 총 스탯 - 캐싱하지 않고 매번 bonus 값(과 레벨)을 반영해서 계산합니다.
    // ------------------------------------------------------------------

    /// <summary>레벨 성장치가 반영된 기초 HP/공격력/방어력입니다. 레벨 1이면 성장치가 0이라 base 값과 같습니다.</summary>
    private int LevelSteps => level - 1;
    private float LevelAdjustedBaseHP => baseHP + hpGrowthPerLevel * LevelSteps;
    private float LevelAdjustedBaseAttackPower => baseAttackPower + attackGrowthPerLevel * LevelSteps;
    private float LevelAdjustedBaseDefense => baseDefense + defenseGrowthPerLevel * LevelSteps;

    public float TotalHP => LevelAdjustedBaseHP * (1f + bonusHPPercent / 100f);
    public float TotalAttackPower => (LevelAdjustedBaseAttackPower + baseWeaponAttackPower) * (1f + bonusAttackPercent / 100f);
    public float TotalDefense => LevelAdjustedBaseDefense * (1f + bonusDefensePercent / 100f);
    public float TotalCritRate => baseCritRate + bonusCritRate;
    public float TotalCritDamage => baseCritDamage + bonusCritDamage;
    public float TotalMP => baseMP;
    public bool IsMaxLevel => level >= maxLevel;

    // ------------------------------------------------------------------
    // 현재 HP/MP - 자연 회복되고, 피해를 입으면 줄어듭니다.
    // ------------------------------------------------------------------

    public float CurrentHP => currentHP;
    public float MaxHP => TotalHP;
    public float CurrentMP => currentMP;
    public float MaxMP => TotalMP;
    public float CurrentEnergy => currentEnergy;
    public float MaxEnergy => maxEnergy;
    public bool IsDead => currentHP <= 0f;

    // SkillInfo 트리의 4개 강화 노드가 해제됐는지 여부입니다. AttackHitbox(기본공격강화/패시브강화),
    // PlayerSkillProjectile(스킬강화), PlayerController(필살기강화)가 각자 필요한 곳에서 이 값을 확인합니다.
    public bool HasBasicAttackUpgrade => hasBasicAttackUpgrade;
    public bool HasPassiveUpgrade => hasPassiveUpgrade;
    public bool HasSkillUpgrade => hasSkillUpgrade;
    public bool HasUltUpgrade => hasUltUpgrade;

    /// <summary>인스펙터에서 값을 바꿀 때마다 에디터가 호출합니다. level이 실수로 1~maxLevel 범위를
    /// 벗어나게 입력되어도 즉시 클램프해서 잘못된 값이 그대로 남아있지 않게 하고, expToNextLevel도
    /// 그 레벨에 맞는 값으로 다시 계산해서 인스펙터에서 바로 확인할 수 있게 합니다.</summary>
    private void OnValidate()
    {
        level = Mathf.Clamp(level, 1, Mathf.Max(1, maxLevel));
        expToNextLevel = ExpRequiredForLevel(level);
    }

    private void Awake()
    {
        Instance = this;

        level = Mathf.Clamp(level, 1, Mathf.Max(1, maxLevel));
        expToNextLevel = ExpRequiredForLevel(level); // 인스펙터에서 level을 25 등으로 미리 설정해뒀어도 그 레벨 구간에 맞는 값으로 시작합니다.
        currentHP = TotalHP;
        currentMP = TotalMP;
        currentEnergy = 0f; // 필살기 에너지는 전투로만 충전되므로 0에서 시작합니다.
        controller = GetComponent<PlayerController>();
    }

    /// <summary>경험치를 amount만큼 얻습니다. 필요 경험치(expToNextLevel)를 넘기면 자동으로
    /// 레벨업하고(한 번에 여러 레벨이 오를 수도 있음), 남은 경험치는 다음 레벨로 이월됩니다.
    /// 레벨업할 때마다 expToNextLevel이 ExpRequiredForLevel(level)로 다시 계산됩니다. 단, 지금 레벨이
    /// 한계돌파가 필요한 레벨(breakthroughLevels)이라면 경험치가 충분해도 거기서 멈춥니다 - currentExp를
    /// expToNextLevel로 고정해두고(넘친 만큼 버리지 않고 유지) 더 진행하지 않습니다. 이미 최대 레벨이면
    /// 더 쌓을 곳이 없으므로 그냥 무시합니다.</summary>
    public void AddExperience(int amount)
    {
        if (IsMaxLevel) return;

        currentExp += amount;

        while (!IsMaxLevel && currentExp >= expToNextLevel)
        {
            if (IsAwaitingBreakthrough)
            {
                currentExp = expToNextLevel; // 한계돌파 전까지는 여기서 더 늘지 않습니다.
                break;
            }

            currentExp -= expToNextLevel;
            LevelUp();
            expToNextLevel = ExpRequiredForLevel(level);
        }

        if (IsMaxLevel) currentExp = 0; // 최대 레벨에 도달하면 더 이상 쌓을 필요가 없으니 정리합니다.
    }

    /// <summary>레벨을 amount만큼 올립니다(기본 1레벨씩). maxLevel을 넘지 않습니다. 한계돌파 제한을
    /// 거치지 않는 저수준 함수입니다 - 디버그/치트용으로 직접 호출하는 경우가 아니라면 AddExperience()나
    /// Breakthrough()를 통해서 레벨이 오르게 하세요. HP/MP 현재값을 자동으로 채워주지는 않습니다 -
    /// 최대치가 늘어난 만큼의 차이는 자연 회복(hpRegenPerSecond/mpRegenPerSecond)으로 서서히 채워집니다.
    /// 레벨업 즉시 풀피/풀MP로 채우고 싶다면 이 함수 호출 뒤에 Heal(MaxHP) 등을 직접 호출하세요.</summary>
    public void LevelUp(int amount = 1)
    {
        level = Mathf.Clamp(level + amount, 1, maxLevel);
    }

    /// <summary>레벨을 특정 값으로 직접 지정합니다 (퀘스트 보상, 디버그 등). maxLevel을 넘지 않습니다.
    /// expToNextLevel도 그 레벨에 맞게 다시 계산합니다. 한계돌파 완료 여부는 건드리지 않으므로, 돌파 레벨
    /// 이후로 직접 이동시키는 경우 필요하다면 Breakthrough()도 별도로 호출해주세요.</summary>
    public void SetLevel(int newLevel)
    {
        level = Mathf.Clamp(newLevel, 1, maxLevel);
        expToNextLevel = ExpRequiredForLevel(level);
    }

    // ------------------------------------------------------------------
    // 한계돌파 (레벨 상한 돌파)
    // ------------------------------------------------------------------

    /// <summary>지금 레벨이 한계돌파가 필요한 레벨(breakthroughLevels)이면서, 아직 그 레벨의 돌파를
    /// 마치지 못한 상태인지 여부입니다. true인 동안에는 경험치가 충분해도 AddExperience()가 레벨업을
    /// 진행하지 않습니다. 캐릭터 창 UI에서 "돌파 필요" 표시를 띄우는 조건으로 그대로 쓰면 됩니다.</summary>
    public bool IsAwaitingBreakthrough => IsBreakthroughLevel(level) && !completedBreakthroughLevels.Contains(level);

    /// <summary>특정 레벨(breakthroughLevels에 들어있는 값 중 하나)의 한계돌파를 이미 마쳤는지 여부입니다.
    /// 캐릭터 창 UI가 "다음으로 돌파해야 할 단계"를 찾을 때 씁니다(UICharacterInfo.GetPendingBreakthroughIndex 참고).</summary>
    public bool HasCompletedBreakthrough(int lvl) => completedBreakthroughLevels.Contains(lvl);

    private bool IsBreakthroughLevel(int lvl)
    {
        for (int i = 0; i < breakthroughLevels.Length; i++)
        {
            if (breakthroughLevels[i] == lvl) return true;
        }
        return false;
    }

    /// <summary>지금 레벨 구간(Tier)에 맞는, 다음 레벨로 가는 데 필요한 경험치를 계산합니다.
    /// breakthroughLevels[0](기본 20)과 [1](기본 40)을 구간 경계로 사용합니다.</summary>
    private int ExpRequiredForLevel(int lvl)
    {
        int tier2Start = breakthroughLevels.Length > 0 ? breakthroughLevels[0] : 20;
        int tier3Start = breakthroughLevels.Length > 1 ? breakthroughLevels[1] : 40;

        if (lvl < tier2Start)
        {
            return expTier1BaseAt1 + expTier1GrowthPerLevel * (lvl - 1);
        }
        if (lvl < tier3Start)
        {
            return expTier2BaseAt20 + expTier2GrowthPerLevel * (lvl - tier2Start);
        }
        return expTier3BaseAt40 + expTier3GrowthPerLevel * (lvl - tier3Start);
    }

    /// <summary>한계돌파를 실행합니다. 재료/골드가 충분한지 확인하고 차감하는 건 이 함수의 역할이 아닙니다 -
    /// 나중에 만들 캐릭터 창 UI가 그 조건을 먼저 확인/차감한 뒤에만 이 함수를 호출해주는 구조를 가정합니다.
    /// 지금이 정말 돌파 가능한 시점(IsAwaitingBreakthrough)이 아니면 아무 것도 하지 않고 false를 반환합니다.
    /// 성공하면 HP/공격력/방어력에 각각 breakthroughHPBonusPercent/AttackBonusPercent/DefenseBonusPercent%
    /// 만큼 보너스를 누적하고, 막혀있던 레벨업을 밀린 경험치만큼 이어서 진행한 뒤 true를 반환합니다.</summary>
    public bool Breakthrough()
    {
        if (!IsAwaitingBreakthrough) return false;

        completedBreakthroughLevels.Add(level);

        bonusHPPercent += breakthroughHPBonusPercent;
        bonusAttackPercent += breakthroughAttackBonusPercent;
        bonusDefensePercent += breakthroughDefenseBonusPercent;

        // 돌파하는 동안 쌓여있던 경험치가 있었다면, 막혀있던 만큼 이어서 레벨업합니다.
        // (다음 한계돌파 레벨을 또 만나면 IsAwaitingBreakthrough가 다시 true가 되어 여기서 멈춥니다.)
        while (!IsMaxLevel && currentExp >= expToNextLevel && !IsAwaitingBreakthrough)
        {
            currentExp -= expToNextLevel;
            LevelUp();
            expToNextLevel = ExpRequiredForLevel(level);
        }

        if (IsMaxLevel) currentExp = 0;

        return true;
    }

    // ------------------------------------------------------------------
    // 스킬 강화 (SkillInfo 트리) - UICharacterInfo.ClickSkillUpgradeButton()이 재료/골드 소모에 성공한
    // 직후 selectedSkillNode.data.skillId에 맞춰 이 중 하나를 호출합니다(UICharacterInfo.cs 참고).
    // 한계돌파와 달리 되돌릴 일이 없어서 각각 한 번 켜면 끝인 단순한 플래그입니다.
    // ------------------------------------------------------------------

    /// <summary>기본공격강화: 공격속도 +30%, 데미지 +30%. 실제 적용은 PlayerController(애니메이터 속도)와
    /// AttackHitbox(isBasicAttack인 히트박스의 데미지)에서 이 값을 확인해서 처리합니다.</summary>
    public void UnlockBasicAttackUpgrade() => hasBasicAttackUpgrade = true;

    /// <summary>패시브강화: 기본 공격 적중 시 스킬 쿨타임 -0.2초. 실제 적용은 AttackHitbox가 이 값을
    /// 확인해서 PlayerController.ReduceSkillCooldown()을 호출하는 방식으로 처리합니다.</summary>
    public void UnlockPassiveUpgrade() => hasPassiveUpgrade = true;

    /// <summary>스킬강화: 파이어볼 크기/범위 +50%(범위), 데미지 +30%. 실제 적용은 PlayerSkillProjectile이
    /// 발사하는 순간 이 값을 확인해서 처리합니다.</summary>
    public void UnlockSkillUpgrade() => hasSkillUpgrade = true;

    /// <summary>필살기강화: 내려찍기 이후 0.5초 뒤 2차 폭발(마법 데미지 300%). 실제 적용은 PlayerController가
    /// OnUltSlamImpact() Animation Event에서 이 값을 확인해서 처리합니다.</summary>
    public void UnlockUltUpgrade() => hasUltUpgrade = true;

    private void Update()
    {
        Regenerate(ref currentHP, TotalHP, hpRegenPerSecond);
        Regenerate(ref currentMP, TotalMP, mpRegenPerSecond);
        Regenerate(ref currentEnergy, maxEnergy, energyRegenPerSecond);
    }

    /// <summary>초당 perSecond만큼 max까지 서서히 채웁니다. max가 (디버프 등으로) 줄어들어 현재값을
    /// 넘어서게 된 경우에도 다음 프레임에 바로 max로 클램프됩니다.</summary>
    private static void Regenerate(ref float current, float max, float perSecond)
    {
        current = Mathf.Min(max, current + perSecond * Time.deltaTime);
    }

    /// <summary>IDamageable 구현. 몬스터의 공격 등 외부에서 호출하면 현재 HP를 깎습니다 (0 밑으로 내려가지 않음).
    /// 이미 사망한 상태(HP 0)면 무시합니다. 구르는 동안의 무적은 여기서 확인하지 않습니다 - PlayerController가
    /// 구르는 동안 플레이어 오브젝트의 레이어 자체를 바꿔서(EnterInvincible()) 몬스터 공격의 Hit Mask에 아예
    /// 걸리지 않게 하므로, 무적 상태에서는 이 TakeDamage() 자체가 호출되지 않습니다(히트 VFX/데미지 숫자도
    /// 같이 안 뜹니다). HP를 깎은 뒤, 그 결과로 사망했으면 PlayerController.Die()를, 아직 살아있으면
    /// PlayerController.TakeHit()을 호출해서 Die/Hit 모션이 재생되도록 합니다.</summary>
    public void TakeDamage(float amount)
    {
        if (currentHP <= 0f) return;

        currentHP = Mathf.Max(0f, currentHP - amount);

        if (currentHP <= 0f)
        {
            if (controller != null) controller.Die();
        }
        else
        {
            if (controller != null) controller.TakeHit();
        }
    }

    /// <summary>포션/힐 스킬 등에서 호출할 회복 함수입니다. MaxHP를 넘지 않습니다.</summary>
    public void Heal(float amount)
    {
        currentHP = Mathf.Min(TotalHP, currentHP + amount);
    }

    /// <summary>필살기 에너지를 amount만큼 충전합니다. AttackHitbox가 기본 공격/스킬 적중 시 호출합니다
    /// (판정 하나당 한 번만 - 여러 대상을 동시에 맞혀도 중복 충전되지 않습니다). maxEnergy를 넘지 않습니다.</summary>
    public void AddEnergy(float amount)
    {
        currentEnergy = Mathf.Clamp(currentEnergy + amount, 0f, maxEnergy);
    }

    /// <summary>마나를 amount만큼 소모합니다. 스킬/필살기를 사용하는 순간 PlayerController가 호출합니다 -
    /// 충분한지(CurrentMP >= cost) 확인은 호출하는 쪽에서 먼저 하고, 여기서는 그냥 깎기만 합니다
    /// (0 밑으로는 내려가지 않습니다).</summary>
    public void SpendMana(float amount)
    {
        currentMP = Mathf.Max(0f, currentMP - amount);
    }

    /// <summary>필살기 에너지를 amount만큼 소모합니다. 필살기를 사용하는 순간 PlayerController가 호출합니다.</summary>
    public void SpendEnergy(float amount)
    {
        currentEnergy = Mathf.Max(0f, currentEnergy - amount);
    }

    /// <summary>기본 공격/스킬 등 플레이어가 가하는 데미지를 계산합니다.
    /// 데미지 = (총 공격력 - targetDefense) × (damagePercent / 100), 치명타면 × (100% + 총 치명타 피해량%).
    /// 치명타는 총 치명타 확률(%)로 판정합니다. targetDefense를 생략하면 0으로 계산됩니다
    /// (몬스터에 방어력 스탯이 생기면 그 값을 넘겨주세요).</summary>
    public DamageResult CalculateDamage(float damagePercent, float targetDefense = 0f)
    {
        float rawDamage = Mathf.Max(0f, TotalAttackPower - targetDefense) * (damagePercent / 100f);

        bool isCrit = Random.Range(0f, 100f) < TotalCritRate;
        float finalDamage = isCrit ? rawDamage * (1f + TotalCritDamage / 100f) : rawDamage;

        finalDamage = ApplyDamageVariance(finalDamage);

        return new DamageResult(finalDamage, isCrit);
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