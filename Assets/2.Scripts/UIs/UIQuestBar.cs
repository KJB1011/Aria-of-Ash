// ============================================================================
// UIQuestBar.cs
// ----------------------------------------------------------------------------
// UIQuest(퀘스트 로그 창)의 Content 안에 퀘스트 하나마다 Instantiate되는 항목 프리팹입니다.
// UIIngameQuestBar(HUD용)와 비슷하지만, 전체 설명과 완료 여부, 보상(경험치/골드/아이템)까지 함께
// 보여줍니다.
//
// [보상 텍스트]
//   QuestData.rewardExp/rewardGold/rewardItems 중 실제로 값이 있는 것만("0"이거나 비어있으면
//   건너뜁니다) "EXP +100, 골드 +50, 슬라임 젤리 x3" 형태로 한 줄에 이어붙입니다. 보상이 하나도
//   없으면 빈 문자열을 넣습니다(Txt Rewards 오브젝트 자체를 숨기고 싶다면 이 스크립트를 그에 맞게
//   확장하세요).
//
// [완료 보고형 퀘스트 표시 - _imgReadyToTurnIn]
//   QuestData.requiresTurnIn이 켜진 퀘스트는 목표를 다 채워도 자동으로 완료되지 않고
//   QuestProgress.isReadyToTurnIn만 true가 됩니다(아직 activeQuests에 남아있는 상태) - 이 경우
//   _imgReadyToTurnIn을 켜서 "NPC에게 보고하세요" 같은 표시를 해줄 수 있습니다. 완전히 완료되면
//   (isCompleted) _imgCompleted가 켜지고 _imgReadyToTurnIn은 꺼집니다.
//
// [프리팹 준비]
//   1) TextMeshProUGUI를 Txt Quest Name / Txt Description / Txt Objectives / Txt Rewards 필드에
//      연결하세요.
//   2) 완료된 퀘스트를 표시할 Image(_imgCompleted, 예: "완료" 도장 아이콘)를 연결하세요 - 비워두면
//      무시합니다.
//   3) (완료 보고형 퀘스트를 쓴다면) 보고 대기 상태를 표시할 Image(_imgReadyToTurnIn, 예: "!" 아이콘)도
//      연결하세요 - 비워두면 무시합니다.
// ============================================================================

using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIQuestBar : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _txtQuestName;
    [SerializeField] TextMeshProUGUI _txtDescription;
    [SerializeField] TextMeshProUGUI _txtObjectives;
    [SerializeField] TextMeshProUGUI _txtRewards;
    [SerializeField] Image _imgCompleted;
    [SerializeField] Image _imgReadyToTurnIn;

    private static readonly StringBuilder builder = new StringBuilder();

    /// <summary>이 항목이 나타낼 퀘스트 진행 상황을 설정합니다.</summary>
    public void Setup(QuestProgress progress)
    {
        _txtQuestName.text = progress.data.questName;
        _txtDescription.text = progress.data.description;

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

        if (_txtRewards != null)
        {
            _txtRewards.text = BuildRewardsText(progress.data);
        }

        if (_imgCompleted != null)
        {
            _imgCompleted.enabled = progress.isCompleted;
        }

        if (_imgReadyToTurnIn != null)
        {
            _imgReadyToTurnIn.enabled = progress.isReadyToTurnIn && !progress.isCompleted;
        }
    }

    /// <summary>QuestData의 보상(경험치/골드/아이템) 중 실제로 값이 있는 것만 "EXP +100, 골드 +50,
    /// 슬라임 젤리 x3" 형태로 이어붙입니다. 보상이 하나도 없으면 빈 문자열을 돌려줍니다.</summary>
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