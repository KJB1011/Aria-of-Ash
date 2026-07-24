// ============================================================================
// QuestManager.cs
// ----------------------------------------------------------------------------
// 퀘스트 진행을 총괄하는 매니저입니다. 씬에 하나만 있으면 되고, 다른 스크립트는
// QuestManager.Instance.AddQuest(questData)/ReportKill(monsterId)만 호출하면 됩니다. UI는
// OnQuestAdded/OnQuestProgressChanged/OnQuestCompleted 이벤트를 구독해서 그리면 됩니다 -
// PlayerInventory가 OnInventoryChanged를 발행하고 UIInventory가 구독하는 것과 완전히 같은 방식입니다.
//
// [진행 상황 - QuestProgress]
//   QuestData(애셋, 정의만 담음)와 별개로, 실제 진행도는 이 매니저가 런타임에 만드는 QuestProgress가
//   들고 있습니다(MonsterStats의 baseHP/currentHP 분리와 같은 패턴) - objectiveCounts는 QuestData.objectives와
//   같은 순서/길이의 배열입니다.
//
// [Kill 목표]
//   MonsterFSM.ChangeState(State.Die)에서 ReportKill(stats.monsterId)를 호출해줍니다. 지금 진행 중인
//   퀘스트 중 Kill 목표가 있고 targetMonsterId가 일치하면(아직 목표 수량 미만이면) 카운트를 1 올립니다.
//
// [TalkToNpc 목표]
//   NPCTalker.Interact()에서 ReportTalkToNpc(npcId)를 호출해줍니다. Kill 목표와 완전히 같은 방식으로,
//   지금 진행 중인 퀘스트 중 TalkToNpc 목표가 있고 targetNpcId가 일치하면(아직 목표 수량 미만이면)
//   카운트를 1 올립니다 - 그 NPC와 상호작용(F키)하기만 하면 카운트되고 대화 내용/분기는 확인하지
//   않습니다.
//
// [CompleteQuest 목표 - 여러 작은 퀘스트를 다 깨야 완료되는 큰 퀘스트]
//   외부에서 Report 함수를 호출할 필요가 없습니다 - CompleteQuest()가 어떤 퀘스트든 완료 처리할 때마다
//   내부에서 자동으로 CheckDependentQuestObjectives(data)를 호출해서, 지금 진행 중인 다른 퀘스트들 중
//   방금 완료된 이 퀘스트를 targetQuest로 하는 CompleteQuest 목표가 있으면 카운트를 1 올립니다(Kill/
//   TalkToNpc와 완전히 같은 증가 방식). 이 과정에서 그 상위 퀘스트까지 완료돼버리면 CompleteQuest()가
//   재귀적으로 다시 호출되는데, activeQuests.ToArray()로 스냅샷을 떠서 순회하므로(다른 Report 함수들과
//   같은 이유) 안전합니다.
//
// [Collect 목표 - 인벤토리 기반]
//   PlayerInventory.OnInventoryChanged를 구독해서(Start()에서, Awake가 아닙니다 - PlayerInventory.Instance가
//   확실히 준비된 뒤에 구독하기 위해서입니다), 인벤토리가 바뀔 때마다 진행 중인 퀘스트의 모든 Collect
//   목표를 PlayerInventory.Instance.GetItemCount(item) 기준으로 다시 계산합니다 - 누적 획득량이 아니라
//   "지금 보유한 개수" 기준이라, 아이템을 쓰거나 버리면 진행도도 자연스럽게 같이 줄어듭니다.
//
// [완료 / 보상 - 두 가지 방식]
//   QuestData.requiresTurnIn이 false(기본값)면, 모든 목표의 카운트가 각자의 targetCount 이상이 되는
//   즉시 CheckCompletion()이 CompleteQuest()를 호출해서 activeQuests에서 completedQuests로 옮기고,
//   PlayerStats.Instance.AddExperience()/PlayerCurrency.Instance.AddGold()/
//   PlayerInventory.Instance.AddItem()으로 보상을 지급한 뒤 OnQuestCompleted를 발행합니다 - 별도의
//   "완료 보고" 없이 목표 달성 즉시 자동으로 지급됩니다.
//   requiresTurnIn이 true면, 목표를 다 채워도 자동으로 완료되지 않습니다 - 대신 QuestProgress.isReadyToTurnIn을
//   true로 표시하고(퀘스트는 여전히 activeQuests에 남아있습니다) OnQuestReadyToTurnIn을 발행합니다.
//   실제 완료/보상 지급은 나중에 TurnInQuest(data)가 호출될 때(보통 NPC 대화 선택지 -
//   TalkScript.Choice.questToTurnIn → TalkManager.SelectChoice()) 비로소 일어납니다.
//
// [보상 획득 로그 - UIIngameLoot]
//   GrantRewards()가 보상을 지급할 때마다, 전리품을 줍거나(LootPickup) 몬스터가 드롭한 경험치/골드
//   오브젝트를 흡수했을 때(RewardOrb)와 완전히 같은 방식으로 화면 왼쪽 로그(UIIngameLoot)에도 하나씩
//   표시합니다 - 경험치는 Exp Reward Icon, 골드는 Gold Reward Icon 필드에 아이콘을 연결해야 표시됩니다
//   (비워두면 보상은 정상 지급되지만 로그에는 안 뜹니다 - RewardOrb 프리팹에 이미 설정해둔 아이콘과
//   같은 스프라이트를 연결하면 됩니다). 아이템 보상(rewardItems)은 LootItemData.icon을 그대로 쓰므로
//   따로 설정할 게 없습니다.
//
// [씬 준비]
//   빈 오브젝트에 이 스크립트를 붙이세요. 씬에 정확히 하나만 있어야 합니다.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>퀘스트 하나의 런타임 진행 상황입니다. QuestData(정의)는 바뀌지 않고, 이 클래스가
/// 목표별 진행 카운트와 완료/보고 대기 여부를 담습니다.</summary>
public class QuestProgress
{
    public QuestData data;
    public int[] objectiveCounts;
    public bool isCompleted;

    /// <summary>requiresTurnIn 퀘스트에서, 목표는 다 채웠지만 아직 NPC에게 보고(TurnInQuest)하지
    /// 않은 상태인지 여부입니다. requiresTurnIn이 false인 퀘스트는 이 상태를 거치지 않고 바로
    /// isCompleted로 넘어가므로 항상 false입니다.</summary>
    public bool isReadyToTurnIn;

    public QuestProgress(QuestData data)
    {
        this.data = data;
        objectiveCounts = new int[data.objectives.Length];
        isCompleted = false;
        isReadyToTurnIn = false;
    }

    /// <summary>모든 목표가 각자의 targetCount 이상 채워졌는지 여부입니다.</summary>
    public bool AllObjectivesComplete()
    {
        for (int i = 0; i < data.objectives.Length; i++)
        {
            if (objectiveCounts[i] < data.objectives[i].targetCount) return false;
        }
        return true;
    }
}

public class QuestManager : MonoBehaviour
{
    /// <summary>씬에 하나만 있는 컴포넌트라, 다른 스크립트에서 여기로 바로 접근합니다.</summary>
    public static QuestManager Instance { get; private set; }

    /// <summary>지금 진행 중인 퀘스트들입니다.</summary>
    public IReadOnlyList<QuestProgress> ActiveQuests => activeQuests;
    /// <summary>완료된 퀘스트들입니다.</summary>
    public IReadOnlyList<QuestProgress> CompletedQuests => completedQuests;

    /// <summary>새 퀘스트가 추가될 때 발생합니다 - UI가 이걸 구독해서 목록을 새로 그리면 됩니다.</summary>
    public event Action<QuestProgress> OnQuestAdded;
    /// <summary>어떤 퀘스트든 진행도가 바뀔 때(Kill/Collect 카운트 변화) 발생합니다.</summary>
    public event Action<QuestProgress> OnQuestProgressChanged;
    /// <summary>requiresTurnIn 퀘스트의 목표를 전부 채워서 "보고 대기" 상태가 됐을 때 발생합니다 - UI가
    /// 이걸 구독해서 "NPC에게 보고하세요" 같은 표시를 켤 수 있습니다. requiresTurnIn이 false인 퀘스트는
    /// 이 이벤트 없이 바로 OnQuestCompleted로 넘어갑니다.</summary>
    public event Action<QuestProgress> OnQuestReadyToTurnIn;
    /// <summary>퀘스트가 완료(보상 지급까지 끝남)됐을 때 발생합니다. requiresTurnIn이 false면 목표 달성
    /// 즉시, true면 TurnInQuest()가 호출된 시점에 발생합니다.</summary>
    public event Action<QuestProgress> OnQuestCompleted;

    [Header("보상 획득 로그 (화면 왼쪽 UIIngameLoot)")]
    [Tooltip("퀘스트 완료로 경험치를 받았을 때 화면 왼쪽 전리품 로그(UIIngameLoot)에 표시할 아이콘입니다 - " +
              "몬스터를 처치했을 때 나오는 경험치 오브젝트(RewardOrb)와 같은 아이콘을 연결하면 됩니다. " +
              "비워두면(null) 경험치는 그대로 지급되지만 로그에는 표시되지 않습니다.")]
    public Sprite expRewardIcon;
    [Tooltip("경험치 로그에 표시할 이름입니다. 예: \"경험치\" → \"경험치 x100\"으로 표시됩니다.")]
    public string expRewardDisplayName = "경험치";
    [Tooltip("퀘스트 완료로 골드를 받았을 때 화면 왼쪽 로그에 표시할 아이콘입니다. 비워두면(null) 골드는 " +
              "그대로 지급되지만 로그에는 표시되지 않습니다.")]
    public Sprite goldRewardIcon;
    [Tooltip("골드 로그에 표시할 이름입니다. 예: \"골드\" → \"골드 x50\"으로 표시됩니다.")]
    public string goldRewardDisplayName = "골드";

    private readonly List<QuestProgress> activeQuests = new List<QuestProgress>();
    private readonly List<QuestProgress> completedQuests = new List<QuestProgress>();

    private bool subscribedToInventory;

    private void Awake()
    {
        Instance = this;
    }

    // PlayerInventory.OnInventoryChanged 구독은 Awake()가 아니라 Start()에서 합니다 - 씬 로드 시점에
    // 존재하는 모든 오브젝트의 Awake()는 어떤 오브젝트의 Start()보다도 먼저 전부 끝나는 게 유니티가
    // 보장하는 순서라서, Start() 시점이면 PlayerInventory.Instance가 이미 확실히 설정되어 있습니다.
    private void Start()
    {
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.OnInventoryChanged += HandleInventoryChanged;
            subscribedToInventory = true;
        }
    }

    private void OnDestroy()
    {
        if (subscribedToInventory && PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.OnInventoryChanged -= HandleInventoryChanged;
        }
    }

    /// <summary>data 퀘스트를 새로 시작합니다. 이미 진행 중이거나 이미 완료된 퀘스트면 중복으로
    /// 추가하지 않고, prerequisiteQuests를 다 채우지 못했으면 역시 추가하지 않습니다(각각 경고 로그만
    /// 남기고 조용히 무시합니다). data가 null이면 아무 것도 하지 않습니다(대화 선택지에 퀘스트를
    /// 연결하지 않은 경우를 안전하게 무시하기 위해서입니다).</summary>
    public void AddQuest(QuestData data)
    {
        if (data == null) return;

        if (FindActive(data) != null || FindCompleted(data) != null)
        {
            Debug.LogWarning($"[QuestManager] '{data.questName}'은 이미 진행 중이거나 완료된 퀘스트라 다시 추가하지 않습니다.", data);
            return;
        }

        if (!ArePrerequisitesMet(data))
        {
            Debug.LogWarning($"[QuestManager] '{data.questName}'은 선행조건(prerequisiteQuests)을 아직 " +
                              "다 채우지 못해 받을 수 없습니다.", data);
            return;
        }

        QuestProgress progress = new QuestProgress(data);
        activeQuests.Add(progress);

        OnQuestAdded?.Invoke(progress);

        // Collect 목표가 있는 퀘스트라면, 추가되는 순간 이미 갖고 있던 아이템 수량이 즉시 반영되어야
        // 합니다(퀘스트를 받기 전부터 재료를 갖고 있던 경우) - 바로 한 번 재계산합니다.
        if (RecalculateCollectObjectives(progress))
        {
            OnQuestProgressChanged?.Invoke(progress);
        }
        CheckCompletion(progress);
    }

    /// <summary>monsterId를 가진 몬스터가 죽었을 때 호출하세요(MonsterFSM.ChangeState(State.Die) 참고).
    /// 지금 진행 중인 퀘스트 중 이 몬스터를 목표로 하는 Kill 목표가 있으면(아직 다 채우지 못했다면)
    /// 카운트를 1 올립니다. monsterId가 비어있으면 무시합니다(몬스터에 ID를 설정하지 않은 경우를
    /// 안전하게 넘어가기 위해서입니다).</summary>
    public void ReportKill(string monsterId)
    {
        if (string.IsNullOrEmpty(monsterId)) return;

        // 완료 처리 중 보상 지급(AddItem 등)이 인벤토리 이벤트를 재귀적으로 발생시킬 수 있어서,
        // 순회 도중 리스트가 바뀌어도 안전하도록 스냅샷을 떠서 돕니다.
        foreach (QuestProgress progress in activeQuests.ToArray())
        {
            bool changed = false;

            for (int i = 0; i < progress.data.objectives.Length; i++)
            {
                QuestData.Objective obj = progress.data.objectives[i];
                if (obj.type != QuestData.ObjectiveType.Kill) continue;
                if (obj.targetMonsterId != monsterId) continue;
                if (progress.objectiveCounts[i] >= obj.targetCount) continue;

                progress.objectiveCounts[i]++;
                changed = true;
            }

            if (changed)
            {
                OnQuestProgressChanged?.Invoke(progress);
                CheckCompletion(progress);
            }
        }
    }

    /// <summary>npcId를 가진 NPC와 상호작용했을 때 호출하세요(NPCTalker.Interact() 참고). ReportKill()과
    /// 완전히 같은 방식으로, 지금 진행 중인 퀘스트 중 이 NPC를 목표로 하는 TalkToNpc 목표가 있으면
    /// (아직 다 채우지 못했다면) 카운트를 1 올립니다. npcId가 비어있으면 무시합니다(NPC에 ID를 설정하지
    /// 않은 경우를 안전하게 넘어가기 위해서입니다).</summary>
    public void ReportTalkToNpc(string npcId)
    {
        if (string.IsNullOrEmpty(npcId)) return;

        // ReportKill()과 같은 이유로 스냅샷을 떠서 돕니다(완료 시 보상 지급이 다른 이벤트를 재귀적으로
        // 발생시킬 수 있습니다).
        foreach (QuestProgress progress in activeQuests.ToArray())
        {
            bool changed = false;

            for (int i = 0; i < progress.data.objectives.Length; i++)
            {
                QuestData.Objective obj = progress.data.objectives[i];
                if (obj.type != QuestData.ObjectiveType.TalkToNpc) continue;
                if (obj.targetNpcId != npcId) continue;
                if (progress.objectiveCounts[i] >= obj.targetCount) continue;

                progress.objectiveCounts[i]++;
                changed = true;
            }

            if (changed)
            {
                OnQuestProgressChanged?.Invoke(progress);
                CheckCompletion(progress);
            }
        }
    }

    private void HandleInventoryChanged()
    {
        // ReportKill()과 같은 이유로 스냅샷을 떠서 돕니다(완료 시 보상 지급이 이 이벤트를 다시
        // 발생시킬 수 있습니다).
        foreach (QuestProgress progress in activeQuests.ToArray())
        {
            if (RecalculateCollectObjectives(progress))
            {
                OnQuestProgressChanged?.Invoke(progress);
                CheckCompletion(progress);
            }
        }
    }

    /// <summary>progress의 모든 Collect 목표를 PlayerInventory.Instance.GetItemCount() 기준으로 다시
    /// 계산합니다. 하나라도 값이 바뀌었으면 true를 반환합니다.</summary>
    private bool RecalculateCollectObjectives(QuestProgress progress)
    {
        if (PlayerInventory.Instance == null) return false;

        bool changed = false;

        for (int i = 0; i < progress.data.objectives.Length; i++)
        {
            QuestData.Objective obj = progress.data.objectives[i];
            if (obj.type != QuestData.ObjectiveType.Collect) continue;
            if (obj.targetItem == null) continue;

            int owned = PlayerInventory.Instance.GetItemCount(obj.targetItem);
            int clamped = Mathf.Min(owned, obj.targetCount);

            if (progress.objectiveCounts[i] != clamped)
            {
                progress.objectiveCounts[i] = clamped;
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>목표가 방금 전부 채워졌는지 확인합니다. requiresTurnIn이 false인 퀘스트는 바로
    /// CompleteQuest()로 넘어가고, true인 퀘스트는 아직 완료 처리하지 않고 "보고 대기" 상태로만
    /// 표시합니다(실제 완료는 TurnInQuest() 호출을 기다립니다).</summary>
    private void CheckCompletion(QuestProgress progress)
    {
        if (progress.isCompleted) return;
        if (!progress.AllObjectivesComplete()) return;

        if (progress.data.requiresTurnIn)
        {
            if (progress.isReadyToTurnIn) return; // 이미 보고 대기 상태로 알려줬으면 중복으로 알리지 않습니다.

            progress.isReadyToTurnIn = true;
            OnQuestReadyToTurnIn?.Invoke(progress);
            return;
        }

        CompleteQuest(progress);
    }

    /// <summary>퀘스트를 실제로 완료 처리합니다 - activeQuests에서 completedQuests로 옮기고 보상을
    /// 지급한 뒤 OnQuestCompleted를 발행합니다. requiresTurnIn이 false인 퀘스트는 CheckCompletion()이
    /// 목표 달성 즉시 이 함수를 호출하고, true인 퀘스트는 TurnInQuest()가 이 함수를 호출합니다.</summary>
    private void CompleteQuest(QuestProgress progress)
    {
        progress.isCompleted = true;
        activeQuests.Remove(progress);
        completedQuests.Add(progress);

        GrantRewards(progress.data);

        OnQuestCompleted?.Invoke(progress);

        // 이 퀘스트의 완료를 targetQuest로 삼는 CompleteQuest 목표가 있는지 확인합니다(파일 상단
        // [CompleteQuest 목표] 참고) - "여러 작은 퀘스트를 다 깨야 완료되는 큰 퀘스트" 기능입니다.
        CheckDependentQuestObjectives(progress.data);
    }

    /// <summary>completedQuestData가 방금 완료됐을 때 호출합니다. 지금 진행 중인 퀘스트 중 이
    /// completedQuestData를 targetQuest로 하는 CompleteQuest 목표가 있으면(아직 목표 수량 미만이면)
    /// 카운트를 1 올립니다 - ReportKill()/ReportTalkToNpc()와 완전히 같은 증가 방식이지만, 외부에서
    /// 호출하는 게 아니라 CompleteQuest() 내부에서만 호출됩니다.</summary>
    private void CheckDependentQuestObjectives(QuestData completedQuestData)
    {
        // ReportKill()과 같은 이유로 스냅샷을 떠서 돕니다 - 이 순회 도중 다른 퀘스트가 완료되면
        // CompleteQuest()가 재귀적으로 다시 호출되어 activeQuests/completedQuests가 바뀔 수 있습니다.
        foreach (QuestProgress progress in activeQuests.ToArray())
        {
            bool changed = false;

            for (int i = 0; i < progress.data.objectives.Length; i++)
            {
                QuestData.Objective obj = progress.data.objectives[i];
                if (obj.type != QuestData.ObjectiveType.CompleteQuest) continue;
                if (obj.targetQuest != completedQuestData) continue;
                if (progress.objectiveCounts[i] >= obj.targetCount) continue;

                progress.objectiveCounts[i]++;
                changed = true;
            }

            if (changed)
            {
                OnQuestProgressChanged?.Invoke(progress);
                CheckCompletion(progress);
            }
        }
    }

    /// <summary>NPC 대화 선택지 등에서, requiresTurnIn 퀘스트를 보고해서 완료 처리할 때 호출하세요
    /// (TalkScript.Choice.questToTurnIn → TalkManager.SelectChoice() 참고). data가 null이거나, 지금
    /// 진행 중인 퀘스트가 아니거나, 아직 목표를 다 채우지 못해 보고할 수 없는 상태면 경고만 남기고
    /// 아무 것도 하지 않습니다(대화 선택지에 연결을 안 했거나 순서가 꼬인 경우를 안전하게 무시하기
    /// 위해서입니다). 성공하면 true를 반환합니다.</summary>
    public bool TurnInQuest(QuestData data)
    {
        if (data == null) return false;

        QuestProgress progress = FindActive(data);
        if (progress == null)
        {
            Debug.LogWarning($"[QuestManager] '{data.questName}'은 지금 진행 중인 퀘스트가 아니라서 보고할 수 없습니다.", data);
            return false;
        }

        if (!progress.isReadyToTurnIn)
        {
            Debug.LogWarning($"[QuestManager] '{data.questName}'은 아직 목표를 다 채우지 못해 보고할 수 없습니다.", data);
            return false;
        }

        CompleteQuest(progress);
        return true;
    }

    /// <summary>실제로 보상을 지급하고, 전리품을 주울 때/몬스터가 보상을 드롭할 때와 똑같이 화면 왼쪽
    /// 로그(UIIngameLoot)에도 하나씩 표시합니다(LootPickup.Interact()/RewardOrb.Absorb()와 같은
    /// 패턴). UIIngameLoot가 씬에 없는 테스트 씬 등에서도 안전하도록 ?.로 호출합니다 - 로그가 안
    /// 떠도 보상 지급 자체는 정상적으로 이뤄집니다.</summary>
    private void GrantRewards(QuestData data)
    {
        if (data.rewardExp > 0 && PlayerStats.Instance != null)
        {
            PlayerStats.Instance.AddExperience(data.rewardExp);

            if (expRewardIcon != null)
            {
                UIIngameLoot.Instance?.AddLoot(expRewardIcon, $"{expRewardDisplayName} x{data.rewardExp}");
            }
        }

        if (data.rewardGold > 0 && PlayerCurrency.Instance != null)
        {
            PlayerCurrency.Instance.AddGold(data.rewardGold);

            if (goldRewardIcon != null)
            {
                UIIngameLoot.Instance?.AddLoot(goldRewardIcon, $"{goldRewardDisplayName} x{data.rewardGold}");
            }
        }

        if (data.rewardItems != null && PlayerInventory.Instance != null)
        {
            foreach (QuestData.RewardItem rewardItem in data.rewardItems)
            {
                if (rewardItem.item == null || rewardItem.amount <= 0) continue;
                PlayerInventory.Instance.AddItem(rewardItem.item, rewardItem.amount);
                UIIngameLoot.Instance?.AddLoot(rewardItem.item.icon, $"{rewardItem.item.displayName} x{rewardItem.amount}");
            }
        }
    }

    private QuestProgress FindActive(QuestData data)
    {
        foreach (QuestProgress p in activeQuests)
        {
            if (p.data == data) return p;
        }
        return null;
    }

    private QuestProgress FindCompleted(QuestData data)
    {
        foreach (QuestProgress p in completedQuests)
        {
            if (p.data == data) return p;
        }
        return null;
    }

    /// <summary>data.prerequisiteQuests에 넣어둔 퀘스트들이 전부 완료됐는지 확인합니다. 배열이
    /// 비어있거나 null이면(선행조건 없음) 항상 true입니다. 배열 안의 null 항목은 무시합니다(인스펙터에서
    /// 빈 슬롯을 남겨둔 경우를 안전하게 넘어가기 위해서입니다).</summary>
    private bool ArePrerequisitesMet(QuestData data)
    {
        if (data.prerequisiteQuests == null) return true;

        foreach (QuestData prereq in data.prerequisiteQuests)
        {
            if (prereq == null) continue;
            if (FindCompleted(prereq) == null) return false;
        }

        return true;
    }

    /// <summary>data가 지금 진행 중인 퀘스트인지 여부입니다(완료 보고 대기 상태 포함). data가 null이면
    /// false입니다.</summary>
    public bool IsQuestActive(QuestData data)
    {
        return data != null && FindActive(data) != null;
    }

    /// <summary>data가 이미 완료된 퀘스트인지 여부입니다. data가 null이면 false입니다.</summary>
    public bool IsQuestCompleted(QuestData data)
    {
        return data != null && FindCompleted(data) != null;
    }

    /// <summary>data가 지금 진행 중이면서 목표를 전부 채워 "완료 보고 대기(ReadyToTurnIn)" 상태인지
    /// 여부입니다 - requiresTurnIn이 false인 퀘스트는 목표를 채우는 즉시 바로 완료되어 이 상태를
    /// 거치지 않으므로 항상 false입니다. data가 null이면 false입니다.</summary>
    public bool IsQuestReadyToTurnIn(QuestData data)
    {
        if (data == null) return false;
        QuestProgress progress = FindActive(data);
        return progress != null && progress.isReadyToTurnIn;
    }

    /// <summary>data를 지금 AddQuest()로 받을 수 있는 상태인지(중복이 아니고 선행조건도 다 채웠는지)
    /// 미리 확인하고 싶을 때 쓰세요 - 예를 들어 NPC 대화에서 아직 선행조건을 못 채운 퀘스트는 아예
    /// 선택지 자체를 보여주지 않고 싶을 때 이 함수로 미리 물어보면 됩니다. data가 null이면
    /// false입니다.</summary>
    public bool CanStartQuest(QuestData data)
    {
        if (data == null) return false;
        if (FindActive(data) != null || FindCompleted(data) != null) return false;
        return ArePrerequisitesMet(data);
    }
}