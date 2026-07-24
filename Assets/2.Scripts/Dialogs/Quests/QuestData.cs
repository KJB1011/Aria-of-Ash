// ============================================================================
// QuestData.cs
// ----------------------------------------------------------------------------
// 퀘스트 하나를 정의하는 ScriptableObject입니다. TalkScript와 같은 방식으로 애셋으로 만들어두고,
// 대화 선택지(TalkScript.Choice.questToGrant)나 다른 트리거에서 QuestManager.Instance.AddQuest(data)로
// 넘겨주면 실제 진행 상황(QuestProgress)이 만들어집니다.
//
// [진행 상황은 이 애셋에 저장하지 않습니다]
//   MonsterStats가 baseHP(고정값)와 currentHP(런타임 값)를 분리해서 갖고 있는 것과 같은 이유로,
//   이 QuestData는 순수하게 "정의"만 담고 실제 진행도(목표별 카운트, 완료 여부)는 QuestManager가
//   런타임에 만드는 QuestProgress가 따로 들고 있습니다(QuestManager.cs 참고) - 같은 QuestData를
//   여러 번 참조해도 항상 같은 목표/보상 정의를 재사용할 수 있고, 애셋 자체는 플레이 중에 바뀌지
//   않습니다.
//
// [목표(Objective) - Kill / Collect / TalkToNpc / CompleteQuest]
//   Kill: targetMonsterId와 같은 monsterId를 가진 몬스터가 죽을 때마다(MonsterFSM.ChangeState(State.Die) →
//   QuestManager.ReportKill(monsterId)) 카운트가 1씩 오릅니다.
//   Collect: targetItem을 지금 몇 개 갖고 있는지(PlayerInventory.GetItemCount - 누적 획득이 아니라
//   "지금 보유한 개수" 기준입니다. 아이템을 쓰거나 버리면 진행도도 같이 줄어듭니다)를
//   PlayerInventory.OnInventoryChanged가 발생할 때마다 다시 계산합니다(QuestManager.cs 참고).
//   TalkToNpc: targetNpcId와 같은 npcId를 가진 NPC와 상호작용(F키)할 때마다(NPCTalker.Interact() →
//   QuestManager.ReportTalkToNpc(npcId)) 카운트가 1씩 오릅니다 - Kill과 완전히 같은 방식이고,
//   그 NPC의 대화 내용/분기와는 무관하게 상호작용 자체만으로 카운트됩니다(대화가 끝까지 진행됐는지는
//   확인하지 않습니다). targetCount는 보통 1이면 충분합니다.
//   CompleteQuest: targetQuest로 지정한 다른 퀘스트가 완료되는 순간(QuestManager.CompleteQuest() 내부에서
//   자동으로 확인 - 따로 Report 함수를 호출할 필요 없습니다) 카운트가 1씩 오릅니다. "여러 개의 작은
//   퀘스트를 다 깨야 완료되는 큰 퀘스트"를 만들 때 씁니다 - 예를 들어 "마을의 신뢰를 쌓자"라는 퀘스트에
//   targetQuest가 각각 "청년의 부탁"/"상인의 부탁"/"촌장의 부탁"인 CompleteQuest 목표 3개를 넣어두면,
//   그 세 퀘스트를 전부 완료해야 "마을의 신뢰를 쌓자"도 완료됩니다(objectives 배열은 원래 전부 채워야
//   완료되므로, 이 셋을 각각 별도 Objective로 나눠 넣으면 됩니다). targetCount는 보통 1이면 충분합니다.
//   [주의] targetQuest에 이 퀘스트 자기 자신을 넣지 마세요 - 자기 자신은 활성 상태에서는 아직 완료된 게
//   아니므로 절대 카운트되지 않아 목표를 영원히 채울 수 없습니다.
//
// [완료 방식 - requiresTurnIn]
//   false(기본값): 목표를 다 채우는 즉시 자동으로 완료 처리되고 보상도 바로 지급됩니다(별도 보고
//   없음). true: 목표를 다 채워도 자동 완료되지 않고 "완료 보고 대기(ReadyToTurnIn)" 상태로 남아있다가,
//   TalkScript.Choice.questToTurnIn으로 연결해둔 NPC 대화 선택지를 골라야 그 시점에 완료/보상 지급이
//   이뤄집니다(QuestManager.TurnInQuest 참고) - 원신/전형적인 RPG의 "NPC에게 돌아가 보고하기" 퀘스트를
//   만들 때 켜세요.
//
// [선행조건 - prerequisiteQuests]
//   여기 넣은 퀘스트들을 전부 완료(completedQuests에 있음)해야만 이 퀘스트를 받을 수 있습니다.
//   QuestManager.AddQuest()가 자동으로 확인해서, 선행조건을 못 채웠으면 조용히(경고만 남기고) 지급을
//   거부합니다. NPC 대화에서 이 퀘스트를 아예 제안하지 않게 하고 싶다면(선택지 자체를 안 보여주거나
//   다른 대사로 넘어가고 싶다면) QuestManager.CanStartQuest(data)를 미리 물어보고 분기하세요. 비워두면
//   선행조건 없이 언제든 받을 수 있습니다(기존 퀘스트와 동일하게 동작).
//
// [애셋 만들기]
//   Project 창에서 우클릭 → Create → Quest > Quest Data 로 새 퀘스트 애셋을 만드세요.
// ============================================================================

using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Quest_New", menuName = "Quest/Quest Data")]
public class QuestData : ScriptableObject
{
    public enum ObjectiveType
    {
        Kill,
        Collect,
        TalkToNpc,
        CompleteQuest
    }

    [Serializable]
    public class Objective
    {
        [Tooltip("이 목표의 종류입니다. Kill이면 Target Monster Id를, Collect면 Target Item을, " +
                  "TalkToNpc면 Target Npc Id를, CompleteQuest면 Target Quest를 사용합니다.")]
        public ObjectiveType type;

        [Tooltip("[Kill 전용] 처치해야 할 몬스터의 monsterId입니다(MonsterStats.monsterId와 같은 값이어야 합니다).")]
        public string targetMonsterId;

        [Tooltip("[Collect 전용] 모아야 할 아이템입니다.")]
        public LootItemData targetItem;

        [Tooltip("[TalkToNpc 전용] 말을 걸어야 할 NPC의 npcId입니다(NPCTalker.npcId와 같은 값이어야 " +
                  "합니다). 그 NPC와 상호작용(F키)하기만 하면 카운트되고, 대화 내용/선택지와는 무관합니다.")]
        public string targetNpcId;

        [Tooltip("[CompleteQuest 전용] 완료돼야 할 다른 퀘스트입니다 - 이 퀘스트가 완료되는 순간 자동으로 " +
                  "카운트됩니다(따로 코드에서 호출할 함수 없음). 자기 자신을 넣지 마세요(파일 상단 주석 참고).")]
        public QuestData targetQuest;

        [Tooltip("목표 수량입니다. TalkToNpc/CompleteQuest는 보통 1이면 충분합니다.")]
        public int targetCount = 1;

        [Tooltip("UIQuest/UIIngameQuest에 표시할 이 목표의 설명입니다(진행 수량 \"(0/5)\"는 UI가 자동으로 " +
                  "붙여주므로 여기엔 그 앞부분만 적으면 됩니다). 예: \"슬라임 처치\"")]
        public string description;
    }

    [Serializable]
    public class RewardItem
    {
        public LootItemData item;
        public int amount = 1;
    }

    [Header("식별 / 표시")]
    [Tooltip("코드에서 퀘스트를 구분할 고유 ID입니다. 예: \"quest_slime_hunt\"")]
    public string questId;
    public string questName;
    [TextArea(2, 5)]
    public string description;

    [Header("목표 (여러 개면 전부 채워야 완료됩니다)")]
    public Objective[] objectives = new Objective[0];

    [Header("완료 방식")]
    [Tooltip("켜두면 목표를 다 채워도 자동으로 완료되지 않고, NPC 대화 선택지(Choice.questToTurnIn)로 " +
              "보고해야 그때 완료/보상 지급됩니다. 꺼두면(기본값) 목표 달성 즉시 자동으로 완료/보상 " +
              "지급됩니다.")]
    public bool requiresTurnIn = false;

    [Header("선행조건 (비워두면 언제든 받을 수 있음)")]
    [Tooltip("여기 넣은 퀘스트들을 전부 완료해야만 이 퀘스트를 받을 수 있습니다. QuestManager.AddQuest()가 " +
              "자동으로 확인합니다. 자세한 설명은 파일 상단 주석 참고.")]
    public QuestData[] prerequisiteQuests = new QuestData[0];

    [Header("보상 - 완료되는 시점(자동 완료 또는 보고 완료)에 자동 지급됩니다(QuestManager.GrantRewards 참고)")]
    public int rewardExp = 0;
    public int rewardGold = 0;
    public RewardItem[] rewardItems = new RewardItem[0];
}