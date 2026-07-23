// ============================================================================
// SkillTreeData.cs
// ----------------------------------------------------------------------------
// SkillInfo(스킬 트리) 노드 하나의 "종류"를 정의하는 ScriptableObject입니다. LootItemData와 같은
// 방식입니다 - 씬에 배치된 SkillTreeNode(버튼)가 아니라, "이 스킬 노드가 뭔지"에 대한 데이터만
// 담습니다. 패시브/기본공격/스킬/필살기 4개 + 강화 버전 4개, 총 8개를 이 애셋으로 하나씩 만들어서
// 씬의 8개 SkillTreeNode에 각각 연결하세요.
//
// [애셋 만들기]
//   Project 창에서 우클릭 → Create → Character > Skill Tree Data 로 새 스킬 애셋을 만드세요.
//   예) "Skill_Passive", "Skill_Passive_Upgrade", "Skill_BasicAttack", "Skill_BasicAttack_Upgrade" 등.
//
// [강화 4종 - Skill Id를 꼭 정확히 맞춰주세요]
//   기본공격강화/패시브강화/스킬강화/필살기강화, 이 4개 "강화" 노드(원본 4개 말고)의 Skill Id는
//   UICharacterInfo.ApplySkillUpgradeEffect()가 문자열 그대로 비교해서 PlayerStats에 실제 효과를 켭니다 -
//   반드시 아래 문자열과 정확히(대소문자까지) 똑같이 입력하세요:
//     "basic_attack_upgrade" - 공격속도 +30%, 데미지 +30%
//     "passive_upgrade"      - 기본 공격 적중 시 스킬 쿨타임 -0.2초
//     "skill_upgrade"        - 파이어볼 크기 0.3→0.5, 범위 +50%, 데미지 +30%
//     "ult_upgrade"          - 내려찍기 0.5초 뒤 2차 폭발(마법 데미지 300%)
//   패시브/기본공격/스킬/필살기 "원본" 4개(처음부터 해제되어 있는 것)는 이 매칭 대상이 아니므로
//   Skill Id를 비워두거나 아무 값이나 넣어도 상관없습니다.
// ============================================================================

using UnityEngine;

[CreateAssetMenu(fileName = "Skill_New", menuName = "Character/Skill Tree Data")]
public class SkillTreeData : ScriptableObject
{
    [Header("식별")]
    [Tooltip("코드/세이브 데이터에서 이 스킬 노드를 구분할 고유 ID입니다. 강화 4종 노드는 반드시 " +
              "\"basic_attack_upgrade\" / \"passive_upgrade\" / \"skill_upgrade\" / \"ult_upgrade\" 중 " +
              "하나와 정확히 똑같이 입력하세요(UICharacterInfo.ApplySkillUpgradeEffect() 참고) - 그래야 " +
              "해제하는 순간 실제 효과가 켜집니다.")]
    public string skillId;

    [Header("표시 정보")]
    public string displayName;
    [Tooltip("정보 패널의 타입 텍스트로 그대로 표시됩니다. 예: \"패시브\", \"기본 공격\", \"스킬\", " +
              "\"필살기\", \"패시브 강화\" 등.")]
    public string skillType;
    [TextArea(3, 6)]
    public string description;
    public Sprite icon;

    [Header("해제 조건 (UNLOCK 버튼)")]
    [Tooltip("이 스킬 노드를 해제하는 데 필요한 재료 1. 비워두면(null) 재료1 없이도 조건을 만족한 것으로 칩니다.")]
    public LootItemData material1;
    public int material1Amount;
    [Tooltip("이 스킬 노드를 해제하는 데 필요한 재료 2. 비워두면(null) 재료2 없이도 조건을 만족한 것으로 칩니다.")]
    public LootItemData material2;
    public int material2Amount;
    public int requiredGold;
}