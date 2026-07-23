// ============================================================================
// UIYesNo.cs
// ----------------------------------------------------------------------------
// 확인/취소가 둘 다 필요할 때 쓰는 범용 팝업입니다. UINotice와 같은 이유로 UICanvas의 "메인 패널"
// 방식(팝업 한 번에 하나만 열림)이 아니라 static Instance + 자체 보이기/숨기기 관리 방식입니다.
//
// [사용법]
//   UIYesNo.Instance.Show("정말 삭제하시겠습니까?", () => { /* 확인 눌렀을 때 */ }, () => { /* 취소 눌렀을 때(생략 가능) */ });
//   onCancel은 생략(null)해도 됩니다 - 취소를 눌러도 그냥 닫히기만 하면 되는 경우가 많습니다.
//
// [ESC로 닫기]
//   UINotice와 동일하게 이 창은 Escape를 직접 듣지 않고, UICanvas가 대신 판단해서 "취소" 버튼을
//   누른 것과 똑같이(ClickCancelButton) 처리해줍니다(UICanvas.cs의 HandleEscapePressed 참고).
//
// [게임을 멈추는 방식]
//   UINotice와 완전히 동일합니다 - Show() 시점의 Time.timeScale/커서 상태를 저장해뒀다가 닫을 때
//   그대로 복원해서, 이미 다른 팝업 위에 겹쳐 떠도 안전합니다.
//
// [씬 준비]
//   1) 팝업 오브젝트(항상 활성화 상태로 두세요)에 이 스크립트와 CanvasGroup을 붙이세요.
//   2) 메시지를 표시할 TextMeshProUGUI를 Txt Message 필드에 연결하세요.
//   3) 확인 버튼의 OnClick에 ClickOKButton()을, 취소 버튼의 OnClick에 ClickCancelButton()을
//      연결하세요.
//   4) 씬에 이 스크립트를 가진 오브젝트가 정확히 하나 있어야 합니다.
// ============================================================================

using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class UIYesNo : MonoBehaviour
{
    public static UIYesNo Instance { get; private set; }

    [SerializeField] TextMeshProUGUI _txtMessage;

    [Header("표시/숨김")]
    public float fadeDuration = 0.15f;

    private CanvasGroup canvasGroup;
    private Tween fadeTween;
    private bool isOpen;

    private CursorLockMode previousCursorLockState;
    private bool previousCursorVisible;
    private float previousTimeScale;

    // Show()로 넘겨받은 콜백입니다. 확인/취소 버튼을 누르는 순간 한 번만 호출하고 바로 비웁니다
    // (닫혀있는 동안 이전 콜백이 남아있다가 다음에 실수로 다시 호출되는 일이 없도록).
    private Action onYes;
    private Action onNo;

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

    /// <summary>message를 표시하며 확인/취소 팝업을 엽니다. onConfirm은 확인 버튼을 눌렀을 때,
    /// onCancel은 취소 버튼(또는 Escape)을 눌렀을 때 한 번 호출됩니다 - onCancel은 null이어도
    /// 됩니다(그냥 닫히기만 함).</summary>
    public void Show(string message, Action onConfirm, Action onCancel = null)
    {
        _txtMessage.text = message;
        onYes = onConfirm;
        onNo = onCancel;

        if (isOpen) return;
        isOpen = true;

        previousCursorLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        fadeTween?.Kill();
        fadeTween = canvasGroup.DOFade(1f, fadeDuration).SetUpdate(true);
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    /// <summary>확인 버튼 OnClick에 연결하세요.</summary>
    public void ClickOKButton()
    {
        Action callback = onYes;
        Close();
        callback?.Invoke();
    }

    /// <summary>취소 버튼 OnClick에 연결하세요. UICanvas의 Escape 처리에서도 이 함수를 그대로
    /// 호출합니다.</summary>
    public void ClickCancelButton()
    {
        Action callback = onNo;
        Close();
        callback?.Invoke();
    }

    private void Close()
    {
        if (!isOpen) return;
        isOpen = false;

        onYes = null;
        onNo = null;

        Cursor.lockState = previousCursorLockState;
        Cursor.visible = previousCursorVisible;
        Time.timeScale = previousTimeScale;

        fadeTween?.Kill();
        fadeTween = canvasGroup.DOFade(0f, fadeDuration).SetUpdate(true);
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }
}