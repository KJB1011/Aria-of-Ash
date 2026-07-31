// ============================================================================
// EndTest.cs
// ----------------------------------------------------------------------------
// 포트폴리오용 베타테스트 종료 지점에 붙이는 스크립트입니다. EndTestGame()이 호출되면 기존 UINotice
// 팝업으로 "베타테스트는 여기까지 입니다." 알림창을 띄웁니다.
//
// [NPC 대화에서 호출하는 법 - TriggerEvent 경유]
//   TalkScript(대화 애셋)는 ScriptableObject라 씬 오브젝트를 직접 참조할 수 없어서, 대화의
//   Choice/Talks에 이 스크립트의 EndTestGame()을 바로 연결할 수는 없습니다(TalkScript.cs 상단 주석
//   참고). 대신 이렇게 연결하세요:
//     1) TriggerEvent 스텝 하나짜리 CutsceneData 애셋을 새로 만드세요(예: "EndTestCutscene" -
//        step.type = TriggerEvent, step.eventKey = "EndTest").
//     2) 그 대화의 Talks.cutsceneToPlayAfter(또는 원하는 Choice)에 이 CutsceneData를 연결하세요 -
//        대화가 그 지점에 도달하면 컷씬이 재생되면서 TriggerEvent가 실행됩니다.
//     3) 이 씬의 CutsceneManager Trigger Events 리스트에 같은 키("EndTest")를 등록하고, UnityEvent에
//        이 컴포넌트의 EndTestGame()을 연결하세요(CutsceneManager.cs 상단 [씬 준비] 6번 참고) -
//        매개변수가 없는 메서드라 인스펙터 드롭다운에 바로 나타납니다.
//
// [동작]
//   UINotice.Instance.Show()로 알림창을 띄웁니다(UINotice.cs 참고) - Time.timeScale을 0으로 멈추고
//   커서를 풀어주는 처리까지 기존 팝업이 전부 대신 해줍니다.
//
// [씬 준비]
//   1) 빈 오브젝트(또는 NPC 오브젝트 자신)에 이 스크립트를 붙이세요.
//   2) 이 씬에 UINotice가 준비되어 있어야 합니다(UINotice.cs의 [씬 준비] 참고) - 없으면 경고만
//      남기고 알림창은 뜨지 않습니다.
// ============================================================================

using UnityEngine;

public class EndTest : MonoBehaviour
{
    [Header("알림 메시지")]
    [Tooltip("UINotice로 띄울 안내 문구입니다.")]
    [SerializeField] private string _noticeMessage = "베타테스트는 여기까지 입니다.";

    /// <summary>베타테스트 종료 지점에 도달했을 때 호출하세요(NPC 대화 → TriggerEvent 경유 - 파일 상단
    /// [NPC 대화에서 호출하는 법] 참고). 매개변수가 없어야 UnityEvent 인스펙터 드롭다운에 나타납니다
    /// (MiddleSlimeBoss.PlayShockwaveForCutscene()과 같은 이유).</summary>
    public void EndTestGame()
    {
        NoticePopup();
    }

    /// <summary>"베타테스트는 여기까지 입니다."라고 알림창을 띄워주는 함수입니다 - 새로 만들지 않고
    /// 기존 UINotice 팝업을 그대로 재사용합니다(UINotice.cs 참고). UINotice가 씬에 없으면 경고만
    /// 남기고 넘어갑니다(NullReferenceException 없이 안전하게).</summary>
    public void NoticePopup()
    {
        if (UINotice.Instance == null)
        {
            Debug.LogWarning("[EndTest] UINotice가 씬에 없어 알림창을 띄울 수 없습니다.", this);
            return;
        }

        UINotice.Instance.Show(_noticeMessage);
    }
}