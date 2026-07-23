// ============================================================================
// UIExit.cs
// ----------------------------------------------------------------------------
// 게임을 종료할지 묻는 팝업입니다. 다른 알림/확인창(UINotice/UIYesNo/UITrash)과 달리 인게임
// UICanvas에 묶이지 않고 완전히 독립적으로 동작합니다 - 나중에 만들 로그인 씬에서 GameManager를
// 만들면 그 아래 자식 오브젝트로 이 스크립트를 붙여서 DontDestroyOnLoad로 씬을 넘나들며 계속
// 살아있게 할 계획이라고 하셨죠. 그 구조를 그대로 지원하도록 이 스크립트 자체는 UICanvas나
// 특정 씬의 다른 무엇도 필요로 하지 않게 만들었습니다 - GameManager를 만드실 때 이 스크립트가
// 붙은 오브젝트를 GameManager의 자식으로 두고, GameManager(부모)에 DontDestroyOnLoad만
// 걸어주시면 별도 코드 수정 없이 그대로 어떤 씬에서든 계속 동작합니다.
//
// [언제 뜨는가 - "인게임 UI를 제외한 나머지가 모두 꺼져있을 때"]
//   인벤토리/캐릭터정보/옵션 등 UICanvas가 관리하는 팝업이나 알림/확인창이 하나라도 열려있으면
//   Escape는 이 창이 아니라 그 UI를 닫는 데 먼저 쓰입니다(UICanvas.cs의 HandleEscapePressed
//   참고) - 그 쪽에서 "닫을 UI가 하나도 없다"고 판단했을 때만 UICanvas가 이 스크립트의 Show()를
//   대신 호출해줍니다. 그래서 이 스크립트는 UICanvas.Instance가 존재하는 씬(인게임)에서는 직접
//   Escape를 듣고 "열지"는 않습니다 - UICanvas가 알아서 열어줄 때까지 기다립니다. 반대로 UICanvas가
//   아예 없는 씬(예: 나중에 만들 로그인 씬)에서는 그런 중재자가 없으므로 이 스크립트가 직접
//   Escape를 듣고 판단해서 엽니다. (닫는 것(취소)은 씬에 UICanvas가 있든 없든 항상 이 스크립트가
//   직접 처리합니다 - 열려있는 동안 Escape를 또 누르면 취소와 같은 동작입니다.)
//
// [게임 종료]
//   확인을 누르면 Application.Quit()을 호출합니다. 에디터에서는 빌드된 실행 파일이 아니라서
//   Application.Quit()이 아무 효과가 없으므로, 에디터에서 테스트할 때는 대신 플레이 모드를
//   꺼줍니다(#if UNITY_EDITOR).
//
// [씬 준비]
//   1) 종료 확인 팝업 오브젝트(항상 활성화 상태로 두세요)에 이 스크립트와 CanvasGroup을 붙이세요.
//   2) 확인 버튼의 OnClick에 ClickOKButton()을, 취소 버튼의 OnClick에 ClickCancelButton()을
//      연결하세요.
//   3) 씬에 이 스크립트를 가진 오브젝트가 정확히 하나 있어야 합니다(나중에 GameManager를 만들면
//      그 아래로 옮기되, 여전히 씬(들)에 정확히 하나만 존재해야 합니다).
// ============================================================================

using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CanvasGroup))]
public class UIExit : MonoBehaviour
{
    /// <summary>씬(또는 DontDestroyOnLoad로 살아남은 오브젝트)에 하나만 있는 컴포넌트라, 다른
    /// 스크립트에서 여기로 바로 접근합니다.</summary>
    public static UIExit Instance { get; private set; }

    [Header("표시/숨김")]
    public float fadeDuration = 0.15f;

    private CanvasGroup canvasGroup;
    private Tween fadeTween;
    private bool isOpen;

    private CursorLockMode previousCursorLockState;
    private bool previousCursorVisible;
    private float previousTimeScale;

    private InputAction escapeAction;

    /// <summary>지금 열려있는지 여부입니다.</summary>
    public bool IsOpen => isOpen;

    private void Awake()
    {
        Instance = this;

        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        escapeAction = new InputAction("ExitEscape", InputActionType.Button, "<Keyboard>/escape");
    }

    private void OnEnable()
    {
        escapeAction.Enable();
    }

    private void OnDisable()
    {
        escapeAction.Disable();
    }

    private void Update()
    {
        if (!escapeAction.WasPressedThisFrame()) return;

        if (isOpen)
        {
            ClickCancelButton();
            return;
        }

        // UICanvas가 있는 씬에서는 "닫을 UI가 하나도 없을 때"만 UICanvas가 대신 Show()를
        // 호출해줍니다 - 여기서 직접 열어버리면 인벤토리 등을 막 닫으면서 동시에 종료 확인창까지
        // 튀어나오는 문제가 생길 수 있어서, 그 판단을 UICanvas에게 완전히 맡깁니다.
        if (UICanvas.Instance != null) return;

        Show();
    }

    /// <summary>종료 확인 팝업을 엽니다. 다른 스크립트에서 직접 부를 수도 있지만, 보통은 Escape
    /// 입력(위 Update() 또는 UICanvas.HandleEscapePressed)을 통해 호출됩니다.</summary>
    public void Show()
    {
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

    /// <summary>확인 버튼 OnClick에 연결하세요. 게임을 종료합니다.</summary>
    public void ClickOKButton()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>취소 버튼 OnClick에 연결하세요. 종료하지 않고 닫습니다. Escape를 또 눌러도
    /// (위 Update()에서) 이 함수가 호출됩니다.</summary>
    public void ClickCancelButton()
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