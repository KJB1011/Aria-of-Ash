// ============================================================================
// UIControls.cs
// ----------------------------------------------------------------------------
// 조작법 안내 패널입니다. 인게임 씬이 시작되면 자동으로 뜨고, 패널의 닫기 버튼을 누르면
// 사라집니다. 별도 튜토리얼 스테이지 없이 조작법을 알려주기 위한 용도입니다.
//
// [자동으로 뜨기]
//   다른 UI들과 달리 버튼이나 키 입력으로 여는 게 아니라, 이 컴포넌트 자신의 Start()에서
//   UICanvas.Instance.OpenUI(gameObject)를 직접 호출해서 씬이 시작되자마자 스스로 엽니다.
//   (PlayerStats/PlayerInventory 등을 참조하는 다른 UI들과 마찬가지로 Start() 시점이면
//   UICanvas.Awake()가 이미 끝나있는 게 유니티가 보장하는 순서라 안전합니다.) 씬을 다시 시작할
//   때마다(재접속 등) 매번 뜹니다 - "한 번만 보여주기"가 필요하면 PlayerPrefs로 "이미 봤는지"
//   플래그를 하나 추가해서 Start()에서 확인하는 식으로 쉽게 확장할 수 있습니다(지금은 요청하신
//   범위가 아니라 추가하지 않았습니다).
//
// [닫기 버튼으로만 닫기]
//   처음에는 아무 키나 누르면 닫히게 했었는데, 패널에 이미 닫기 버튼을 만들어두셨다고 해서 그
//   버튼만으로 닫도록 바꿨습니다 - OnClick에 ClickExitButton()을 연결하세요(아래 [씬 준비] 4번).
//   버튼을 눌러야 하므로 Open()에서 다른 클릭 가능한 창들(UIInventory 등)과 마찬가지로 커서를
//   풀어줍니다 - 커서가 잠긴 채로는(화면 중앙에 고정되고 안 보이는 상태) 버튼을 조준해 클릭할
//   수 없기 때문입니다.
//
// [옵션창에서 다시 보기]
//   UIOption에 "조작법" 버튼을 하나 만들어서 OnClick에 UIOption.ClickShowControlsButton()을
//   연결하면 됩니다 - 내부에서 UICanvas.Instance.OpenUI(UICanvas.Instance.Controls.gameObject)를
//   호출해서, 지금 열려있는 옵션 창을 이 조작법 패널로 바로 바꿔줍니다(UICanvas.OpenUI()가 원래
//   "이미 다른 팝업이 열려있으면 그것부터 닫고 새 팝업을 연다"는 방식으로 설계되어 있어서, 옵션
//   창을 먼저 직접 닫을 필요 없이 그냥 OpenUI만 호출하면 됩니다).
//
// [열고 닫을 때 커서 처리 - 다른 팝업들과 같은 패턴]
//   Open()에서 Cursor.lockState = None / visible = true로 풀어서 닫기 버튼을 클릭할 수 있게 하고,
//   Close()에서는 무조건 다시 Locked / false로 되돌립니다(UIInventory/UICharacterInfo/UIOption/
//   UIQuest와 완전히 같은 이유 - 옵션 창에서 이 패널을 열었다가 닫아도, 씬 시작 시 자동으로
//   열렸다가 닫혀도 항상 게임플레이 기본 상태로 돌아오게 하기 위함입니다).
//
// [씬 준비]
//   1) 조작법 안내 이미지/텍스트를 담은 패널 오브젝트에 이 스크립트와 CanvasGroup을 붙이세요
//      (CanvasGroup은 RequireComponent로 자동 추가됩니다).
//   2) 이 오브젝트는 항상 활성화(Active) 상태로 두세요 - 다른 팝업들과 마찬가지로 SetActive가
//      아니라 CanvasGroup 알파로 보이기/숨기기를 처리합니다.
//   3) UICanvas의 Controls 필드에 이 오브젝트를 연결하세요(UICanvas.cs도 함께 수정해서 Controls
//      프로퍼티를 추가해뒀습니다).
//   4) 만들어두신 닫기 버튼의 OnClick에 이 스크립트의 ClickExitButton()을 연결하세요 - 절대로
//      Close()를 직접 연결하지 마세요(IUIWindow.cs 상단 경고 참고 - Close()만 연결하면 커서는
//      돌아온 것처럼 보여도 UICanvas.currentPopup/Time.timeScale이 그대로 남아 게임이 멈춘 채로
//      있는 문제가 생깁니다).
// ============================================================================

using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class UIControls : MonoBehaviour, IUIWindow
{
    [Header("표시/숨김")]
    public float fadeDuration = 0.15f;

    private CanvasGroup canvasGroup;
    private Tween fadeTween;
    private bool isOpen;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    // UICanvas.Awake()는 씬의 모든 오브젝트의 Awake()가 끝난 뒤에야 어느 오브젝트의 Start()든
    // 실행된다는 유니티의 순서 보장 덕분에, 여기 Start() 시점이면 UICanvas.Instance가 이미
    // 준비되어 있습니다(PlayerStats 등을 참조하는 다른 UI들과 같은 패턴).
    private void Start()
    {
        UICanvas.Instance.OpenUI(gameObject);
    }

    /// <summary>닫기 버튼 OnClick에 연결하세요. Close()를 직접 연결하지 마세요 - IUIWindow.cs 상단의
    /// 경고를 참고하세요.</summary>
    public void ClickExitButton()
    {
        UICanvas.Instance.CloseUI(gameObject);
    }

    /// <summary>IUIWindow 구현. UICanvas.OpenUI()가 호출합니다 - 직접 호출하지 마세요. 닫기 버튼을
    /// 클릭할 수 있도록 커서를 풉니다(다른 팝업들과 같은 패턴).</summary>
    public void Open()
    {
        if (isOpen) return;
        isOpen = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        fadeTween?.Kill();
        fadeTween = canvasGroup.DOFade(1f, fadeDuration).SetUpdate(true); // 게임이 멈춰도(Time.timeScale = 0) 페이드는 정상 속도로 재생됩니다.
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    /// <summary>IUIWindow 구현. UICanvas.CloseUI()가 호출합니다 - 직접 호출하지 마세요. 닫히는 순간
    /// 커서를 무조건 다시 잠그고 숨깁니다(다른 팝업들과 같은 이유 - 위 [열고 닫을 때 커서 처리]
    /// 참고).</summary>
    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        fadeTween?.Kill();
        fadeTween = canvasGroup.DOFade(0f, fadeDuration).SetUpdate(true);
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }
}