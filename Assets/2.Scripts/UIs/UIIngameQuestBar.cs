// ============================================================================
// UIIngameQuestBar.cs
// ----------------------------------------------------------------------------
// UIIngameQuest(HUD 퀘스트 추적기)의 Content 안에 퀘스트 하나마다 Instantiate되는 항목 프리팹입니다.
// 퀘스트 이름과 목표별 진행도("슬라임 처치 (2/5)" 형태)를 보여줍니다. 클릭 등 상호작용은 없고
// 표시 전용입니다 - UIInventoryBar와 달리 OnClicked 이벤트가 없습니다.
//
// [완료 보고형 퀘스트 - 목표를 다 채운 뒤]
//   QuestData.requiresTurnIn이 켜진 퀘스트는 목표를 다 채워도(QuestProgress.isReadyToTurnIn = true)
//   여전히 ActiveQuests에 남아있어서 HUD에 계속 표시됩니다 - 이때 목표 텍스트 아래에 "(NPC에게
//   보고하세요)" 한 줄을 추가로 붙여서, 플레이어가 다 채웠는데도 왜 안 사라지는지 헷갈리지 않게
//   합니다.
//
// [프리팹 준비]
//   1) TextMeshProUGUI를 Txt Quest Name 필드에 연결하세요.
//   2) 목표 텍스트를 표시할 TextMeshProUGUI를 Txt Objectives 필드에 연결하세요(목표가 여러 개면
//      줄바꿈으로 이어붙입니다 - 목표마다 별도 오브젝트로 나누고 싶다면 이 스크립트를 그에 맞게
//      확장하세요).
// ============================================================================

using System.Text;
using TMPro;
using UnityEngine;

public class UIIngameQuestBar : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _txtQuestName;
    [SerializeField] TextMeshProUGUI _txtObjectives;

    private static readonly StringBuilder builder = new StringBuilder();

    /// <summary>이 항목이 나타낼 퀘스트 진행 상황을 설정합니다.</summary>
    public void Setup(QuestProgress progress)
    {
        _txtQuestName.text = progress.data.questName;

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

        if (progress.data.requiresTurnIn && progress.isReadyToTurnIn && !progress.isCompleted)
        {
            builder.AppendLine().Append("(NPC에게 보고하세요)");
        }

        _txtObjectives.text = builder.ToString();
    }
}