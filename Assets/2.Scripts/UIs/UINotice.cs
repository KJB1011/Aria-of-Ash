// ============================================================================
// UINotice.cs
// ----------------------------------------------------------------------------
// 확인 버튼 하나만 있는 "정보 안내" 팝업입니다. 다른 스크립트가 어디서든
// UINotice.Instance.Show("메시지")로 띄울 수 있는 유틸리티 팝업이라, UIInventory/UIOption처럼
// UICanvas에 "메인 패널"로 등록(팝업 한 번에 하나만 열리는 방식)하지 않았습니다 - 이미 인벤토리 등
// 다른 팝업이 열려있는 위에 겹쳐서 띄우는 경우가 많아서 그 방식과는 맞지 않습니다. 대신
// SoundManager/PlayerInventory처럼 static Instance를 갖고 스스로 보이기/숨기기를 처리합니다.
//
// [사용법]
//   아무 스크립트에서나 UINotice.Instance.Show("가방이 가득 찼습니다."); 처럼 호출하면 됩니다.
//   확인 버튼을 누르면(ClickOKButton) 닫힙니다.
//
// [ESC로 닫기]
//   이 창 자체는 Escape 키를 직접 듣지 않습니다 - UICanvas가 Escape를 누르는 순간 "지금 열려있는
//   UI 중 가장 먼저 닫아야 할 것"을 판단해서 대신 Close()를 호출해줍니다(UICanvas.cs의
//   HandleEscapePressed 참고). UICanvas가 없는 씬에서 단독으로 쓰는 경우엔 Escape로 안 닫히니
//   그럴 땐 OK 버튼으로만 닫아주세요.
//
// [게임을 멈추는 방식 - 다른 팝업 위에 겹쳐 떠도 안전한 이유]
//   Show() 시점의 Time.timeScale/커서 상태를 저장해뒀다가 Close()에서 그대로 되돌립니다(UICanvas처럼
//   무조건 0→1로 고정하지 않음). 예를 들어 이미 인벤토리가 열려서 timeScale이 0인 상태에서
//   알림창을 띄워도, 닫을 때 1이 아니라 원래의 0으로 돌아가서 인벤토리가 계속 멈춰있는 상태가
//   유지됩니다. 아무 팝업도 없이 단독으로 띄우면 평소처럼 0→1로 정상 복귀합니다.
//
// [씬 준비]
//   1) 알림 팝업 오브젝트(항상 활성화 상태로 두세요)에 이 스크립트와 CanvasGroup을 붙이세요
//      (CanvasGroup은 RequireComponent로 자동 추가됩니다).
//   2) 메시지를 표시할 TextMeshProUGUI를 Txt Message 필드에 연결하세요.
//   3) 확인 버튼의 OnClick에 ClickOKButton()을 연결하세요.
//   4) 씬에 이 스크립트를 가진 오브젝트가 정확히 하나 있어야 합니다(다른 스크립트가
//      UINotice.Instance로 바로 접근합니다).
// ============================================================================

using DG.Tweening;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class UINotice : MonoBehaviour
{
    /// <summary>씬에 하나만 있는 컴포넌트라, 다른 스크립트에서 여기로 바로 접근합니다.</summary>
    public static UINotice Instance { get; private set; }

    [SerializeField] TextMeshProUGUI _txtMessage;

    [Header("표시/숨김")]
    public float fadeDuration = 0.15f;

    private CanvasGroup canvasGroup;
    private Tween fadeTween;
    private bool isOpen;

    // 열기 직전의 커서/타임스케일 상태를 저장해뒀다가 닫을 때 그대로 복원합니다(UIInventory와 같은
    // 이유 - 이미 다른 팝업이 열려서 멈춰있던 상태 위에 겹쳐 떠도 안전하게 원래 상태로 돌아갑니다).
    private CursorLockMode previousCursorLockState;
    private bool previousCursorVisible;
    private float previousTimeScale;

    /// <summary>지금 열려있는지 여부입니다. UICanvas가 Escape 처리 우선순위를 판단할 때 확인합니다.</summary>
    public bool IsOpen => isOpen;

    private void Awake()
    {
        Instance = this;

        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>message를 표시하며 팝업을 엽니다. 이미 열려있는 상태에서 다시 호출하면(예: 다른
    /// 알림이 연달아 뜨는 경우) 메시지만 새로 바꿔치기하고, 열기 애니메이션/상태 저장은 다시
    /// 하지 않습니다.</summary>
    public void Show(string message)
    {
        _txtMessage.text = message;

        if (isOpen) return;
        isOpen = true;

        previousCursorLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        fadeTween?.Kill();
        fadeTween = canvasGroup.DOFade(1f, fadeDuration).SetUpdate(true); // 게임이 멈춰도 페이드는 정상 속도로 재생됩니다.
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    /// <summary>확인 버튼 OnClick에 연결하세요.</summary>
    public void ClickOKButton()
    {
        SoundManager.Instance.PlayUIClickSfx();
        Close();
    }

    /// <summary>이 창을 닫습니다. 확인 버튼(ClickOKButton) 또는 UICanvas의 Escape 처리에서 호출됩니다.</summary>
    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;

        Cursor.lockState = previousCursorLockState;
        Cursor.visible = previousCursorVisible;
        Time.timeScale = previousTimeScale;

        fadeTween?.Kill();
        fadeTween = canvasGroup.DOFade(0f, fadeDuration).SetUpdate(true);
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }
}