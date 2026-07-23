// ============================================================================
// UICanvas.cs
// ----------------------------------------------------------------------------
// 전체적인 UI들을 관리하는 Canvas입니다. UIInventory처럼 "팝업" 형태로 여닫는 창을 열 때
// 게임 시간을 멈추고(Time.timeScale = 0), 닫으면 다시 흐르게 합니다. 팝업은 한 번에 하나만
// 열리도록 - 이미 다른 팝업이 열려있는 상태에서 새 팝업을 열면 기존 팝업을 먼저 닫습니다.
//
// [팝업이 되려면]
//   OpenUI()/CloseUI()로 여닫을 UI 오브젝트는 IUIWindow를 구현하고 있어야 합니다 (UIInventory가
//   이미 구현되어 있습니다). Open()/Close() 안에서 실제 보이기/숨기기(CanvasGroup 페이드 등)는
//   각 UI가 알아서 처리하고, UICanvas는 "언제 열고 닫을지"와 "그 동안 시간을 멈출지"만 관리합니다.
//   IUIWindow를 구현하지 않은 오브젝트를 넘기면 GetComponent가 null을 돌려줘서 바로
//   NullReferenceException이 납니다 - 어떤 UI를 팝업으로 등록하는 걸 깜빡했는지 바로 드러납니다.
//
// [_uiIngame은 팝업이 아닙니다]
//   HP바 등 상시 노출되는 인게임 HUD라서 OpenUI()/CloseUI() 대상이 아니고, 시간도 멈추지 않습니다.
//   다른 스크립트에서 참조가 필요할 때 쓸 수 있도록 필드만 갖고 있습니다.
//
// [_uiCharInfo]
//   UICharacterInfo가 IUIWindow를 구현하고 있어서 Inventory와 완전히 같은 방식으로 팝업 등록됩니다 -
//   다른 스크립트는 UICanvas.Instance.CharacterInfo로 꺼내 씁니다(예: UIIngame.ClickCharInfoButton()).
//
// [_uiOption]
//   UIOption도 IUIWindow를 구현하고 있어서 Inventory/CharInfo와 완전히 같은 방식으로 팝업
//   등록됩니다 - 다른 스크립트는 UICanvas.Instance.Option으로 꺼내 씁니다(예:
//   UIIngame.ClickOptionButton()).
//
// [_uiQuest]
//   UIQuest(퀘스트 로그 창)도 IUIWindow를 구현하고 있어서 Inventory/CharInfo/Option과 완전히 같은
//   방식으로 팝업 등록됩니다 - 다른 스크립트는 UICanvas.Instance.Quest로 꺼내 씁니다(예:
//   UIIngame.ClickQuestButton()). L 키는 UIQuest 자신이 직접 처리합니다(I키를 자기가 직접 처리하는
//   UIInventory와 같은 패턴).
//
// [다른 UI가 서로를 찾는 창구]
//   UIInventory 등 개별 UI 스크립트에는 따로 static Instance를 두지 않았습니다 - 대신 UICanvas가
//   모든 UI를 붙잡고 있다가, Ingame/Inventory 같은 타입이 있는 프로퍼티로 꺼내 쓸 수 있게
//   해줍니다. 예) UICanvas.Instance.Inventory.ToggleInventory(); 다른 UI가 필요해지면
//   여기에 같은 방식으로 프로퍼티를 하나씩 추가하면 됩니다.
//
// [씬 준비]
//   1) Canvas 오브젝트에 이 스크립트를 붙이고 In Game / Inventory / Char Info 필드에 각각의
//      UI 루트 오브젝트를 연결하세요.
//   2) Inventory(그리고 나중에 Char Info) UI 오브젝트는 항상 활성화(Active) 상태로 두세요 -
//      SetActive로 껐다 켜는 대신, 각 UI가 자기 CanvasGroup 알파로 보이기/숨기기를 처리합니다
//      (그래야 여닫을 때 페이드 애니메이션이 끊기지 않습니다).
//   3) 씬에 이 스크립트를 가진 오브젝트가 정확히 하나 있어야 합니다 - 다른 UI가
//      UICanvas.Instance로 바로 접근합니다.
//
// [IsUIOpen]
//   지금 열려있는 UI가 하나라도 있는지를 나타냅니다(currentPopup 뿐 아니라 UINotice/UIYesNo/UITrash도
//   함께 확인합니다 - 아래 [Escape로 UI 닫기] 참고). PlayerController가 Update()에서 이 값을
//   확인해서, UI가 열려있는 동안은(Time.timeScale = 0으로 게임이 멈춰있는 동안은) 공격/스킬/대시
//   입력을 아예 읽지 않도록 하는 데 씁니다 - 안 그러면 UI가 열려있을 때 눌렀던 클릭이 큐에
//   쌓여있다가 창을 닫는 순간(Time.timeScale이 다시 1이 되는 순간) 뒤늦게 공격으로 튀어나오는
//   문제가 생깁니다.
//
// [Escape로 UI 닫기]
//   Escape 키를 누르면 HandleEscapePressed()가 "지금 열려있는 UI 중 가장 먼저 닫아야 할 것"을
//   순서대로 찾아서 하나만 닫습니다 - 알림/확인창(UINotice → UIYesNo → UITrash, 다른 팝업 위에
//   겹쳐 뜨는 보조 팝업들이라 먼저 닫습니다) 다음 메인 패널(currentPopup - 인벤토리/캐릭터정보/
//   옵션 중 열려있는 것)입니다. 이 중 닫을 게 하나도 없으면(=인게임 UI 말고는 아무것도 안 열려있는
//   상태) UIExit.Instance.Show()를 대신 호출해서 종료 확인창을 띄웁니다 - UIExit은 인게임
//   UICanvas와 무관하게 독립적으로 동작하도록 만들어졌지만(나중에 GameManager 아래로 옮겨 씬을
//   넘나들며 쓸 계획), 인게임 씬에서는 이렇게 UICanvas가 "언제 열어줄지"만 대신 판단해줍니다
//   (UIExit.cs 참고 - 그래야 인벤토리를 닫는 것과 종료 확인창이 뜨는 게 같은 Escape 입력에 동시에
//   겹쳐 일어나지 않습니다).
// ============================================================================

using UnityEngine;
using UnityEngine.InputSystem;

public class UICanvas : MonoBehaviour
{
    /// <summary>씬에 하나만 있는 컴포넌트라, 다른 UI 스크립트에서 여기로 바로 접근합니다.</summary>
    public static UICanvas Instance { get; private set; }

    [SerializeField] GameObject _uiIngame;
    [SerializeField] GameObject _uiInventory;
    [SerializeField] GameObject _uiCharInfo;
    [SerializeField] GameObject _uiOption;
    [SerializeField] GameObject _uiQuest;

    /// <summary>_uiIngame에서 꺼내둔 UIIngame입니다. 다른 스크립트는 UICanvas.Instance.Ingame으로 씁니다.</summary>
    public UIIngame Ingame { get; private set; }
    /// <summary>_uiInventory에서 꺼내둔 UIInventory입니다. 다른 스크립트는 UICanvas.Instance.Inventory로 씁니다.</summary>
    public UIInventory Inventory { get; private set; }
    /// <summary>_uiCharInfo에서 꺼내둔 UICharacterInfo입니다. 다른 스크립트는 UICanvas.Instance.CharacterInfo로 씁니다.</summary>
    public UICharacterInfo CharacterInfo { get; private set; }
    /// <summary>_uiOption에서 꺼내둔 UIOption입니다. 다른 스크립트는 UICanvas.Instance.Option으로 씁니다.</summary>
    public UIOption Option { get; private set; }
    /// <summary>_uiQuest에서 꺼내둔 UIQuest입니다. 다른 스크립트는 UICanvas.Instance.Quest로 씁니다.</summary>
    public UIQuest Quest { get; private set; }

    private GameObject currentPopup;

    /// <summary>지금 열려있는 UI가 하나라도 있으면 true입니다. PlayerController가 입력 처리를
    /// 건너뛸지 판단하는 데 사용합니다(위 [IsUIOpen] 설명 참고). currentPopup(메인 패널)뿐 아니라
    /// UINotice/UIYesNo/UITrash/UIExit이 열려있는 경우, TalkManager가 대화를 재생 중인 경우
    /// (IsTalking), CutsceneManager가 컷씬을 재생 중인 경우(IsAnyCutscenePlaying)도 함께 확인합니다 -
    /// 이들은 UICanvas가 직접 관리하는 팝업이 아니라 각자 static Instance/static 플래그로 스스로 열고
    /// 닫지만, "게임 입력을 막아야 하는 UI/연출이 떠 있다"는 의미에서는 currentPopup과 동등하게
    /// 취급합니다(컷씬 중에 인벤토리를 열거나 Escape로 끼어들 수 없도록 막는 목적입니다 -
    /// PlayerController의 입력 차단은 CutsceneManager.PlayRoutine()이 BeginCutsceneControl()로 따로
    /// 처리하므로, 여기서는 다른 UI가 끼어드는 것만 막으면 됩니다).</summary>
    public bool IsUIOpen =>
        currentPopup != null ||
        (UINotice.Instance != null && UINotice.Instance.IsOpen) ||
        (UIYesNo.Instance != null && UIYesNo.Instance.IsOpen) ||
        (UITrash.Instance != null && UITrash.Instance.IsOpen) ||
        (UIExit.Instance != null && UIExit.Instance.IsOpen) ||
        (TalkManager.Instance != null && TalkManager.Instance.IsTalking) ||
        CutsceneManager.IsAnyCutscenePlaying;

    private InputAction escapeAction;

    private void Awake()
    {
        Instance = this;

        // _uiIngame/_uiInventory/_uiCharInfo/_uiOption/_uiQuest에는 반드시 각각 UIIngame/UIInventory/
        // UICharacterInfo/UIOption/UIQuest 컴포넌트가 붙어있어야 합니다 - 없으면 여기서 바로 null이
        // 담기고, Ingame/Inventory/CharacterInfo/Option/Quest를 실제로 쓰는 시점에
        // NullReferenceException이 나서 어떤 필드 연결을 빠뜨렸는지 바로 드러납니다.
        Ingame = _uiIngame.GetComponent<UIIngame>();
        Inventory = _uiInventory.GetComponent<UIInventory>();
        CharacterInfo = _uiCharInfo.GetComponent<UICharacterInfo>();
        Option = _uiOption.GetComponent<UIOption>();
        Quest = _uiQuest.GetComponent<UIQuest>();

        escapeAction = new InputAction("CloseTopUI", InputActionType.Button, "<Keyboard>/escape");
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
        if (escapeAction.WasPressedThisFrame())
        {
            HandleEscapePressed();
        }
    }

    /// <summary>Escape 키를 누른 순간 호출됩니다. 지금 열려있는 UI 중 "가장 먼저 닫아야 할 것"
    /// 하나만 찾아서 닫습니다 - 알림/확인창(UINotice/UIYesNo/UITrash, 다른 팝업 위에 겹쳐 뜨는
    /// 보조 팝업들)과 UIExit(이미 열려있다면 이것도 먼저 닫습니다)을 메인 패널(currentPopup)보다
    /// 먼저 확인합니다. 닫을 UI가 하나도 없으면(=인게임 UI 말고는 아무것도 안 열려있는 상태)
    /// 그때 비로소 UIExit에게 종료 확인창을 띄워달라고 요청합니다.
    /// [주의] UIExit가 "이미 열려있는지" 여부를 반드시 Show() 호출보다 먼저 확인해야 합니다 -
    /// UIExit.cs도 더 이상 자기 Update()에서 따로 Escape를 읽지 않고 이 메서드의 판단만 그대로
    /// 따르므로, 열림/닫힘 판단이 전부 이 한 메서드 안에서 순서대로 이루어져야 실행 순서와 무관하게
    /// 항상 정확합니다(예전에는 UIExit이 자기 Update()에서도 독립적으로 Escape를 읽어서, 두
    /// 스크립트의 Update() 실행 순서에 따라 방금 닫힌 걸 곧바로 다시 열어버리는 경합이 있었습니다).</summary>
    private void HandleEscapePressed()
    {
        if (UINotice.Instance != null && UINotice.Instance.IsOpen)
        {
            UINotice.Instance.Close();
            return;
        }

        if (UIYesNo.Instance != null && UIYesNo.Instance.IsOpen)
        {
            UIYesNo.Instance.ClickCancelButton();
            return;
        }

        if (UITrash.Instance != null && UITrash.Instance.IsOpen)
        {
            UITrash.Instance.ClickCancelButton();
            return;
        }

        if (UIExit.Instance != null && UIExit.Instance.IsOpen)
        {
            UIExit.Instance.ClickCancelButton();
            return;
        }

        if (currentPopup != null)
        {
            CloseUI(currentPopup);
            return;
        }

        // 인게임 UI 말고는 아무것도 열려있지 않습니다 - 종료 확인창을 띄웁니다.
        if (UIExit.Instance != null)
        {
            UIExit.Instance.Show();
        }
    }

    /// <summary>ui를 팝업으로 엽니다(IUIWindow.Open() 호출) 그리고 게임 시간을 멈춥니다. 이미 다른
    /// 팝업이 열려있으면 그것부터 닫은 뒤 새 팝업을 엽니다. 이미 열려있는 걸 다시 열라고 하면
    /// 무시합니다.</summary>
    public void OpenUI(GameObject ui)
    {
        if (ui == currentPopup) return;

        if (currentPopup != null)
        {
            GetWindow(currentPopup).Close();
        }

        currentPopup = ui;
        GetWindow(ui).Open();
        Time.timeScale = 0f;
    }

    /// <summary>지금 열려있는 팝업을 닫고(IUIWindow.Close() 호출) 게임 시간을 다시 흐르게 합니다.
    /// ui가 지금 열려있는 팝업이 아니면(이미 닫혔거나 다른 팝업으로 바뀐 뒤라면) 아무 것도 하지
    /// 않습니다.</summary>
    public void CloseUI(GameObject ui)
    {
        if (ui != currentPopup) return;

        GetWindow(ui).Close();
        currentPopup = null;
        Time.timeScale = 1f;
    }

    private static IUIWindow GetWindow(GameObject ui)
    {
        return ui.GetComponent<IUIWindow>();
    }
}