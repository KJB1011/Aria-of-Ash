// ============================================================================
// UIClearQuestBar.cs
// ----------------------------------------------------------------------------
// UIQuest의 ClearQuestWindow(완료된 퀘스트) 탭에 뜨는 항목 프리팹입니다. UIQuestBar(진행 중인 퀘스트
// 탭용)와 거의 같은 정보(이름/설명/목표/보상)를 보여주지만, 여기 뜨는 퀘스트는 전부 이미 완료된
// 것들이라 "진행 중"임을 나타내는 표시(완료 여부 도장, 보고 대기 "!" 아이콘 등)는 필요 없어 뺐습니다.
//
// [프리팹 준비]
//   1) TextMeshProUGUI를 Txt Quest Name / Txt Description / Txt Objectives / Txt Rewards 필드에
//      연결하세요(Description/Objectives/Rewards는 선택 사항 - 비워두면 그냥 건너뜁니다).
//   2) 완성된 프리팹을 UIQuest의 Clear Entry Prefab 필드에 연결하세요.
// ============================================================================

using System.Text;
using TMPro;
using UnityEngine;

public class UIClearQuestBar : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _txtQuestName;
    [SerializeField] TextMeshProUGUI _txtDescription;
    [SerializeField] TextMeshProUGUI _txtObjectives;
    [SerializeField] TextMeshProUGUI _txtRewards;

    private static readonly StringBuilder builder = new StringBuilder();

    /// <summary>이 항목이 나타낼 완료된 퀘스트 진행 상황을 설정합니다. ClearQuestWindow에는 이미
    /// 완료된(QuestManager.CompletedQuests) 것만 넘어오므로 완료 여부를 따로 표시하지 않습니다.</summary>
    public void Setup(QuestProgress progress)
    {
        if (_txtQuestName != null) _txtQuestName.text = progress.data.questName;
        if (_txtDescription != null) _txtDescription.text = progress.data.description;

        if (_txtObjectives != null)
        {
            builder.Clear();
            for (int i = 0; i < progress.data.objectives.Length; i++)
            {
                QuestData.Objective objective = progress.data.objectives[i];
                if (i > 0) builder.AppendLine();
                builder.Append(objective.description)
                       .Append(" (")
                       .Append(progress.objectiveCounts[i])
                       .Append("/")
                       .Append(objective.targetCount)
                       .Append(")");
            }
            _txtObjectives.text = builder.ToString();
        }

        if (_txtRewards != null)
        {
            _txtRewards.text = BuildRewardsText(progress.data);
        }
    }

    /// <summary>QuestData의 보상(경험치/골드/아이템) 중 실제로 값이 있는 것만 "EXP +100, 골드 +50,
    /// 슬라임 젤리 x3" 형태로 이어붙입니다. UIQuestBar.BuildRewardsText와 완전히 같은 로직입니다 -
    /// 이미 지급된 보상을 그대로 보여주는 용도입니다.</summary>
    private static string BuildRewardsText(QuestData data)
    {
        builder.Clear();
        bool hasAny = false;

        if (data.rewardExp > 0)
        {
            builder.Append("EXP +").Append(data.rewardExp);
            hasAny = true;
        }

        if (data.rewardGold > 0)
        {
            if (hasAny) builder.Append(", ");
            builder.Append("골드 +").Append(data.rewardGold);
            hasAny = true;
        }

        if (data.rewardItems != null)
        {
            foreach (QuestData.RewardItem rewardItem in data.rewardItems)
            {
                if (rewardItem.item == null || rewardItem.amount <= 0) continue;

                if (hasAny) builder.Append(", ");
                builder.Append(rewardItem.item.displayName).Append(" x").Append(rewardItem.amount);
                hasAny = true;
            }
        }

        return builder.ToString();
    }
}