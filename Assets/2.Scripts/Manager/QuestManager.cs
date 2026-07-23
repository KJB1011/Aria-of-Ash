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
    /// 추가하지 않습니다. data가 null이면 아무 것도 하지 않습니다(대화 선택지에 퀘스트를 연결하지
    /// 않은 경우를 안전하게 무시하기 위해서입니다).</summary>
    public void AddQuest(QuestData data)
    {
        if (data == null) return;

        if (FindActive(data) != null || FindCompleted(data) != null)
        {
            Debug.LogWarning($"[QuestManager] '{data.questName}'은 이미 진행 중이거나 완료된 퀘스트라 다시 추가하지 않습니다.", data);
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

    private void GrantRewards(QuestData data)
    {
        if (data.rewardExp > 0 && PlayerStats.Instance != null)
        {
            PlayerStats.Instance.AddExperience(data.rewardExp);
        }

        if (data.rewardGold > 0 && PlayerCurrency.Instance != null)
        {
            PlayerCurrency.Instance.AddGold(data.rewardGold);
        }

        if (data.rewardItems != null && PlayerInventory.Instance != null)
        {
            foreach (QuestData.RewardItem rewardItem in data.rewardItems)
            {
                if (rewardItem.item == null || rewardItem.amount <= 0) continue;
                PlayerInventory.Instance.AddItem(rewardItem.item, rewardItem.amount);
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
}