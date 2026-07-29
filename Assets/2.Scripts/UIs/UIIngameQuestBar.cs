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
//   보고하세요)" 한 줄을 추가로 붙이고, Img Checkmark(연결해뒀다면)를 켜서, 플레이어가 다 채웠는데도
//   왜 안 사라지는지 헷갈리지 않게 합니다.
//
// [완료 연출 - ClearImage]
//   퀘스트가 완전히 완료되면(activeQuests에서 빠짐) UIIngameQuest.RefreshEntries()가 이 항목을
//   곧바로 Destroy하지 않고, 대신 PlayCompletedEffect()를 먼저 호출합니다 - 미리 배치해둔
//   ClearImage를 왼쪽(원래 위치에서 Clear Image Start Offset만큼 떨어진 곳)에서 원래 자리(중앙)로
//   부드럽게 슬라이드시킵니다(밖에서 들어오는 느낌). 그 뒤 UIIngameQuest가 일정 시간(기본 3초, HUD
//   컴포넌트의 Completed Display Duration)을 더 기다렸다가 이 항목을 실제로 Destroy합니다 - 실제
//   대기/제거 타이밍은 이 스크립트가 아니라 UIIngameQuest.cs가 관리합니다(UIIngameQuestBar는 연출
//   재생만 담당).
//
// [프리팹 준비]
//   1) TextMeshProUGUI를 Txt Quest Name 필드에 연결하세요.
//   2) 목표 텍스트를 표시할 TextMeshProUGUI를 Txt Objectives 필드에 연결하세요(목표가 여러 개면
//      줄바꿈으로 이어붙입니다 - 목표마다 별도 오브젝트로 나누고 싶다면 이 스크립트를 그에 맞게
//      확장하세요).
//   3) (선택) 완료 보고 대기 상태를 표시할 체크마크 Image를 Img Checkmark 필드에 연결하세요 - 평소엔
//      꺼져있다가 완료 보고를 기다리는 동안만 켜집니다. 비워두면 체크마크 없이 텍스트로만 표시됩니다.
//   4) (선택) 미리 만들어두신 ClearImage 오브젝트를 Clear Image 필드에 연결하세요 - 원하는 최종
//      위치(보통 중앙)에 미리 배치해두시면 됩니다. 이 스크립트가 Awake()에서 그 위치를 기억해뒀다가,
//      완료되는 순간 Clear Image Start Offset만큼 왼쪽으로 이동시킨 뒤 다시 그 자리로 슬라이드시킵니다.
//      평소에는 자동으로 비활성화해두므로 씬에서 따로 꺼둘 필요는 없습니다. 비워두면 연출 없이
//      Completed Display Duration만큼 대기한 뒤 바로 사라집니다.
// ============================================================================

using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIIngameQuestBar : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _txtQuestName;
    [SerializeField] TextMeshProUGUI _txtObjectives;
    [Tooltip("평소(진행 중이고 아직 완료 보고 대기가 아닐 때)에 켜져있는 표시입니다. 완료 보고 대기 " +
              "상태가 되거나 퀘스트가 완전히 완료되면 꺼지고 Img Checkmark가 대신 켜집니다.")]
    [SerializeField] GameObject _questMark;
    [Tooltip("완료 보고 대기 상태이거나 퀘스트가 완전히 완료됐을 때 켜지는 표시입니다(Quest Mark와 " +
              "정확히 반대로 켜/꺼집니다).")]
    [SerializeField] Image _imgCheckmark;

    [Header("완료 연출 - ClearImage")]
    [Tooltip("퀘스트 완료 시 왼쪽에서 중앙(원래 배치해둔 자리)으로 슬라이드해 들어오는 이미지입니다. " +
              "평소엔 꺼져있다가 PlayCompletedEffect()가 호출될 때만 나타납니다. 비워두면 연출 없이 " +
              "넘어갑니다.")]
    [SerializeField] RectTransform _clearImage;
    [Tooltip("ClearImage가 원래 자리까지 이동하는 데 걸리는 시간(초)입니다.")]
    public float clearImageMoveDuration = 0.4f;
    [Tooltip("ClearImage가 시작하는 지점의 오프셋입니다 - 원래 자리(중앙)에서 이 값만큼 떨어진 곳에서 " +
              "시작해 부드럽게 들어옵니다. 왼쪽에서 들어오게 하려면 X를 음수로 크게(예: -400) 주세요.")]
    public Vector2 clearImageStartOffset = new Vector2(-400f, 0f);

    /// <summary>이 항목이 지금 나타내고 있는 퀘스트입니다. UIIngameQuest가 OnQuestCompleted로 넘어온
    /// QuestProgress와 같은 항목을 찾을 때(참조 비교) 씁니다.</summary>
    public QuestProgress Progress { get; private set; }

    private Vector2 clearImageTargetAnchoredPosition;
    private Tween clearImageTween;

    private static readonly StringBuilder builder = new StringBuilder();

    private void Awake()
    {
        if (_clearImage != null)
        {
            clearImageTargetAnchoredPosition = _clearImage.anchoredPosition;
            _clearImage.gameObject.SetActive(false);
        }
    }

    /// <summary>이 항목이 나타낼 퀘스트 진행 상황을 설정합니다.</summary>
    public void Setup(QuestProgress progress)
    {
        Progress = progress;
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

        // requiresTurnIn 퀘스트가 목표를 다 채워서 완료 보고를 기다리는 상태인지 여부입니다.
        // (progress가 이 항목으로 그려지는 시점엔 항상 ActiveQuests에 있는 상태라 isCompleted는
        // 이론상 항상 false지만, 텍스트 조건과 동일하게 방어적으로 남겨둡니다.)
        bool readyToTurnIn = progress.data.requiresTurnIn && progress.isReadyToTurnIn && !progress.isCompleted;

        if (readyToTurnIn)
        {
            builder.AppendLine().Append("(NPC에게 보고하세요)");
        }

        _txtObjectives.text = builder.ToString();

        SetMarkState(readyToTurnIn);
    }

    /// <summary>Quest Mark(평소)/Img Checkmark(완료 보고 대기 또는 완전 완료) 표시를 서로 반대로
    /// 켜고 끕니다. showCheckmark가 true면 Checkmark를 켜고 Quest Mark를 끕니다.</summary>
    private void SetMarkState(bool showCheckmark)
    {
        if (_questMark != null) _questMark.SetActive(!showCheckmark);
        if (_imgCheckmark != null) _imgCheckmark.enabled = showCheckmark;
    }

    /// <summary>퀘스트가 완료된 순간 UIIngameQuest가 호출합니다. Quest Mark/Checkmark를 항상 "완료"
    /// 상태(Checkmark 켜짐)로 고정한 뒤, ClearImage를 원래 자리에서 clearImageStartOffset만큼 떨어진
    /// 곳으로 옮겨뒀다가 다시 원래 자리(중앙)까지 부드럽게 슬라이드시킵니다. requiresTurnIn이 없는
    /// 퀘스트는 완료 보고 대기 상태를 거치지 않고 곧장 완료될 수 있어서(Setup()이 한 번도 Checkmark로
    /// 바꿔줄 기회가 없었을 수 있음), 여기서 한 번 더 강제로 맞춰줍니다. Clear Image가 연결되어 있지
    /// 않으면 슬라이드 연출 없이 null을 돌려줍니다 - 호출하는 쪽(UIIngameQuest)은 null이면 곧바로 대기
    /// 단계로 넘어가면 됩니다.</summary>
    public Tween PlayCompletedEffect()
    {
        SetMarkState(true);

        if (_clearImage == null) return null;

        _clearImage.gameObject.SetActive(true);
        _clearImage.anchoredPosition = clearImageTargetAnchoredPosition + clearImageStartOffset;

        clearImageTween?.Kill();
        clearImageTween = _clearImage.DOAnchorPos(clearImageTargetAnchoredPosition, clearImageMoveDuration).SetUpdate(true);
        return clearImageTween;
    }
}