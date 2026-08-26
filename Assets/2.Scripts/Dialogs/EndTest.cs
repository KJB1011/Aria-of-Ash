// ============================================================================
// EndTest.cs
// ----------------------------------------------------------------------------
// 포트폴리오용 베타테스트 종료 지점에 붙이는 스크립트입니다. EndTestGame()이 호출되면 기존 UINotice
// 팝업으로 "베타테스트는 여기까지 입니다." 알림창을 띄웁니다.
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