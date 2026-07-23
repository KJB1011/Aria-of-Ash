// ============================================================================
// UIInventory.cs
// ----------------------------------------------------------------------------
// I키 또는 버튼 클릭으로 열고 닫는 인벤토리 창입니다. 이 스크립트 자체는 static Instance를 두지
// 않습니다 - 다른 스크립트가 이 인벤토리에 접근해야 하면 UICanvas.Instance.Inventory로 꺼내
// 쓰세요(UICanvas가 모든 UI를 붙잡고 있다가 타입이 있는 프로퍼티로 내어줍니다). PlayerInventory의 내용이 바뀔 때마다
// (아이템 획득 등) 자동으로 갱신되고, ScrollRect의 Content 아래로 UIInventoryBar를 한 줄씩
// 채워서 아이템 아이콘 + 개수를 보여줍니다. _txtGold는 PlayerCurrency의 골드가 바뀔 때마다
// (OnGoldChanged 이벤트) 자동으로 최신 값으로 갱신됩니다 - 인벤토리가 닫혀있는 동안 골드를
// 얻어도 계속 최신 상태를 유지하다가, 열었을 때 바로 맞는 값이 보입니다.
//
// [씬 준비]
//   1) 인벤토리 창 패널(전체를 여닫을 오브젝트)에 이 스크립트와 CanvasGroup을 붙이세요
//      (CanvasGroup은 RequireComponent로 자동 추가됩니다) - 여닫을 때 DOTween으로 알파를 페이드합니다.
//   2) Vertical/Grid Layout Group 등을 붙인 Content를 가진 ScrollRect를 View Inventory 필드에 연결하세요.
//   3) UIInventoryBar 프리팹을 Bar Prefab 필드에 연결하세요.
//   4) 버튼으로도 여닫고 싶다면, 그 버튼의 OnClick에 이 컴포넌트의 ToggleInventory()를 연결하세요.
//      I 키는 코드에서 자동으로 처리되므로 따로 설정할 게 없습니다.
//   5) 이 오브젝트는 항상 활성화(Active) 상태로 두세요 - UICanvas가 SetActive로 껐다 켜는 게
//      아니라, 이 스크립트가 CanvasGroup 알파로 보이기/숨기기를 처리합니다. 씬 시작 시
//      기본적으로 닫혀있습니다(알파 0, 상호작용 불가).
//
// [UICanvas 연동]
//   IUIWindow를 구현해서, UICanvas가 "팝업 하나만 열리게, 열려있는 동안 게임 시간을 멈추게"
//   관리해줍니다. ToggleInventory()(I 키/버튼)는 UICanvas.Instance.OpenUI()/CloseUI()를 호출할
//   뿐이고, 실제 Open()/Close()는 UICanvas가 그 안에서 호출해줍니다 - 직접 이 컴포넌트의
//   Open()/Close()를 호출하지 마세요(팝업 하나만 열리게 하는 관리가 깨집니다).
//   DOFade에 SetUpdate(true)를 붙여서, Time.timeScale이 0이 되어도(게임이 멈춰도) 페이드
//   애니메이션 자체는 정상 속도로 재생되도록 했습니다 - 안 그러면 열리는 순간 시간이 멈춰서
//   페이드가 같이 얼어붙습니다.
//
// [마우스 커서]
//   열리는 순간 마우스 커서 잠금을 자동으로 풀어서(Cursor.lockState = None) UI를 클릭할 수 있게
//   하고, 닫히면 열기 전 상태로 되돌립니다. CameraController의 HandleLook()이 커서가
//   풀려있으면 카메라 회전도 같이 멈추므로, 인벤토리가 열려있는 동안엔 자연스럽게 카메라도
//   멈춥니다. 만약 인벤토리를 열기 전에 이미(Alt/Esc로) 커서를 풀어둔 상태였다면, 닫을 때 다시
//   잠그지 않고 풀린 상태 그대로 유지합니다 - 사용자가 직접 선택한 상태를 덮어쓰지 않기 위해서입니다.
//
// [칸 클릭으로 선택하기]
//   UIInventoryBar가 클릭되면(Button OnClick → UIInventoryBar.OnClickBar() → OnClicked 이벤트)
//   HandleBarClicked()가 호출됩니다 - 같은 칸을 다시 클릭하면 선택 해제, 다른 칸을 클릭하면 기존
//   선택은 풀리고 새 칸이 선택되는 방식입니다(한 번에 하나만 선택). 인벤토리 내용이 바뀌어
//   RefreshBars()가 다시 그릴 때는(아이템 획득/버리기 등) 선택 상태를 무조건 초기화합니다 - 칸
//   오브젝트 자체가 매번 새로 Instantiate되기 때문입니다.
//
// [버리기 버튼]
//   ClickTrashButton()은 지금 선택된 칸이 있으면 그 슬롯을 UITrash.Instance.Show()에 그대로
//   넘겨서 버리기 수량을 정하는 팝업을 엽니다(UITrash.cs 참고). 선택된 칸이 없으면 아무 것도
//   하지 않습니다 - 필요하면 UINotice로 "아이템을 먼저 선택해주세요" 안내를 추가할 수도 있습니다.
// ============================================================================

using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class UIInventory : MonoBehaviour, IUIWindow
{
    [SerializeField] ScrollRect _viewInventory;
    [SerializeField] UIInventoryBar _barPrefab;
    [SerializeField] TextMeshProUGUI _txtGold;

    [Header("표시/숨김")]
    public float fadeDuration = 0.15f;

    private CanvasGroup canvasGroup;
    private readonly List<UIInventoryBar> activeBars = new List<UIInventoryBar>();
    private Tween fadeTween;
    private bool isOpen;
    private bool subscribedToInventory;
    private bool subscribedToCurrency;

    // 지금 선택된 칸입니다(한 번에 하나만 선택). RefreshBars()가 다시 그릴 때마다(칸 오브젝트가
    // 전부 새로 Instantiate되므로) null로 초기화됩니다 - HandleBarClicked() 참고.
    private UIInventoryBar selectedBar;

    // 인벤토리를 열기 직전의 커서 상태를 저장해뒀다가, 닫을 때 그대로 복원합니다 - 열기 전에
    // 이미(Alt/Esc로) 커서가 풀려있었다면 닫아도 다시 잠그지 않고 풀린 상태를 유지합니다.
    private CursorLockMode previousCursorLockState;
    private bool previousCursorVisible;

    private InputAction toggleAction;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        toggleAction = new InputAction("ToggleInventory", InputActionType.Button, "<Keyboard>/i");
    }

    private void OnEnable()
    {
        toggleAction.Enable();
    }

    private void OnDisable()
    {
        toggleAction.Disable();

        if (subscribedToInventory)
        {
            PlayerInventory.Instance.OnInventoryChanged -= HandleInventoryChanged;
            subscribedToInventory = false;
        }

        if (subscribedToCurrency)
        {
            PlayerCurrency.Instance.OnGoldChanged -= HandleGoldChanged;
            subscribedToCurrency = false;
        }
    }

    // PlayerInventory.OnInventoryChanged / PlayerCurrency.OnGoldChanged 구독은 Awake/OnEnable이 아니라
    // Start()에서 합니다 - 씬 로드 시점에 존재하는 모든 오브젝트의 Awake()는 어떤 오브젝트의 Start()보다도
    // 먼저 전부 끝나는 게 유니티가 보장하는 순서라서, Start() 시점이면 PlayerInventory.Instance /
    // PlayerCurrency.Instance가 이미 확실히 설정되어 있습니다 (OnEnable() 시점엔 실행 순서가
    // 오브젝트마다 달라서 아직 안 됐을 수도 있습니다).
    private void Start()
    {
        PlayerInventory.Instance.OnInventoryChanged += HandleInventoryChanged;
        subscribedToInventory = true;
        RefreshBars();

        PlayerCurrency.Instance.OnGoldChanged += HandleGoldChanged;
        subscribedToCurrency = true;
        UpdateGoldText(PlayerCurrency.Instance.gold); // 시작 시점의 골드로 한 번 맞춰둡니다.
    }

    private void Update()
    {
        if (toggleAction.WasPressedThisFrame())
        {
            ToggleInventory();
        }
    }

    /// <summary>I 키/버튼 OnClick에서 호출하는 열기/닫기 토글 함수입니다. UICanvas에게 요청만 하고,
    /// 실제 Open()/Close() 호출은 UICanvas가 해줍니다 - 그래야 팝업이 한 번에 하나만 열리고, 여는
    /// 동안 게임 시간이 멈추는 게 같이 관리됩니다. 다른 스크립트에서 이 인벤토리를 열고 싶으면
    /// (예: UIIngame의 인벤토리 버튼) UICanvas.Instance.Inventory.ToggleInventory()로 접근하세요 -
    /// UICanvas가 모든 UI를 붙잡고 있다가 타입이 있는 프로퍼티로 꺼내줍니다.</summary>
    public void ToggleInventory()
    {
        if (isOpen) UICanvas.Instance.CloseUI(gameObject);
        else UICanvas.Instance.OpenUI(gameObject);
    }

    /// <summary>IUIWindow 구현. UICanvas.OpenUI()가 호출합니다 - 직접 호출하지 말고 ToggleInventory()나
    /// UICanvas.Instance.OpenUI(gameObject)를 쓰세요.</summary>
    public void Open()
    {
        if (isOpen) return;
        isOpen = true;

        previousCursorLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        fadeTween?.Kill();
        fadeTween = canvasGroup.DOFade(1f, fadeDuration).SetUpdate(true); // 게임이 멈춰도(Time.timeScale = 0) 페이드는 정상 속도로 재생됩니다.
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    /// <summary>IUIWindow 구현. UICanvas.CloseUI()가 호출합니다 - 직접 호출하지 말고 ToggleInventory()나
    /// UICanvas.Instance.CloseUI(gameObject)를 쓰세요.</summary>
    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;

        Cursor.lockState = previousCursorLockState;
        Cursor.visible = previousCursorVisible;

        fadeTween?.Kill();
        fadeTween = canvasGroup.DOFade(0f, fadeDuration).SetUpdate(true);
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void HandleInventoryChanged()
    {
        RefreshBars();
    }

    private void HandleGoldChanged(int gold)
    {
        UpdateGoldText(gold);
    }

    private void UpdateGoldText(int gold)
    {
        _txtGold.text = gold.ToString();
    }

    private void RefreshBars()
    {
        // 칸 오브젝트를 전부 새로 그리므로, 기존에 선택되어 있던 칸 참조도 같이 무효화합니다.
        selectedBar = null;

        foreach (UIInventoryBar bar in activeBars)
        {
            if (bar != null) Destroy(bar.gameObject);
        }
        activeBars.Clear();

        foreach (InventorySlot slot in PlayerInventory.Instance.Slots)
        {
            UIInventoryBar bar = Instantiate(_barPrefab, _viewInventory.content);
            bar.SetInventoryBar(slot);
            bar.SetSelected(false);
            bar.OnClicked += HandleBarClicked;
            activeBars.Add(bar);
        }
    }

    /// <summary>UIInventoryBar.OnClicked 구독 콜백입니다. 이미 선택된 칸을 다시 클릭하면 선택을
    /// 해제하고, 다른 칸을 클릭하면 기존 선택을 풀고 새 칸을 선택합니다(한 번에 하나만 선택).</summary>
    private void HandleBarClicked(UIInventoryBar bar)
    {
        if (selectedBar == bar)
        {
            bar.SetSelected(false);
            selectedBar = null;
            return;
        }

        if (selectedBar != null) selectedBar.SetSelected(false);

        selectedBar = bar;
        bar.SetSelected(true);
    }

    /// <summary>버리기 버튼 OnClick에 연결하세요. 지금 선택된 칸이 있으면 그 아이템을 버리는
    /// 수량을 정하는 UITrash 팝업을 엽니다(UITrash.cs 참고). 선택된 칸이 없으면 아무 것도
    /// 하지 않습니다.</summary>
    public void ClickTrashButton()
    {
        if (selectedBar == null || selectedBar.Slot == null) return;

        UITrash.Instance.Show(selectedBar.Slot);
    }
}