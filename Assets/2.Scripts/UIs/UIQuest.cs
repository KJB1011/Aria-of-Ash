// ============================================================================
// UIQuest.cs
// ----------------------------------------------------------------------------
// L키 또는 버튼 클릭으로 열고 닫는 퀘스트 로그 창입니다. UIInventory.cs와 완전히 같은 구조입니다 -
// 이 스크립트 자체는 static Instance를 두지 않고, 다른 스크립트는 UICanvas.Instance.Quest로 꺼내
// 씁니다. QuestManager의 OnQuestAdded/OnQuestProgressChanged/OnQuestCompleted 이벤트를 구독해서
// 창이 열려있든 아니든 항상 최신 목록을 유지하다가, 열렸을 때 바로 맞는 상태가 보이게 합니다.
//
// [사이드 탭 - QuestWindow / ClearQuestWindow]
// UICharacterInfo(CharInfo/SkillInfo)와 같은 방식으로, 왼쪽 사이드 버튼 두 개로 서로 전환합니다.
// 다만 CharInfo/SkillInfo와 달리 두 탭이 패널을 따로 두지 않고 같은 배경/같은 ScrollRect(View
// Quest) 하나를 그대로 공유합니다 - 사이드 버튼을 누르면 그 안의 내용물만 통째로 비우고(기존
// 항목을 Destroy) 새 탭에 맞는 목록으로 다시 채웁니다.
//   - QuestWindow(진행 중인 퀘스트): QuestManager.ActiveQuests를 Entry Prefab(UIQuestBar)으로 표시.
//   - ClearQuestWindow(완료된 퀘스트): QuestManager.CompletedQuests를 Clear Entry Prefab
//     (UIClearQuestBar - 진행 중 표시(보고 대기 "!" 등)가 필요 없는 별도 프리팹)으로 표시.
// 창이 닫혀있는 동안에도 QuestManager 이벤트가 오면 그때그때 다시 그려두므로(HandleQuestChanged),
// 다음에 열었을 때 바로 최신 상태가 보입니다 - 단, 지금 보고 있지 않은 탭까지 미리 그려두지는
// 않고(같은 ScrollRect를 공유하므로 동시에 둘 다 그릴 수 없습니다) 지금 선택된 탭만 갱신합니다.
// 다른 탭으로 전환하는 순간(SetActiveTab) 그 시점 기준으로 새로 그리므로 항상 최신 내용입니다.
//
// [씬 준비]
//   1) 퀘스트 창 패널(전체를 여닫을 오브젝트)에 이 스크립트와 CanvasGroup을 붙이세요(CanvasGroup은
//      RequireComponent로 자동 추가됩니다) - 여닫을 때 DOTween으로 알파를 페이드합니다.
//   2) Vertical Layout Group 등을 붙인 Content를 가진 ScrollRect 하나를 View Quest 필드에
//      연결하세요 - QuestWindow/ClearQuestWindow 두 탭이 이 ScrollRect 하나를 그대로 같이 씁니다.
//   3) 진행 중인 퀘스트용 UIQuestBar 프리팹을 Entry Prefab 필드에, 완료된 퀘스트용 UIClearQuestBar
//      프리팹을 Clear Entry Prefab 필드에 각각 연결하세요.
//   4) UICharacterInfo의 사이드 탭과 같은 방식으로, 두 개의 사이드 버튼(OnClick에 각각
//      ClickSideQuestButton()/ClickSideClearQuestButton() 연결)과, 선택 안 된 쪽을 어둡게 표시할
//      Image 두 개(Img Side Quest Black / Img Side Clear Quest Black)를 준비하세요.
//   4-1) (선택) 탭 위에 "진행중인 퀘스트"/"완료된 퀘스트"라고 자동으로 바뀌는 타이틀을 쓰고
//        싶다면, 그 TextMeshProUGUI를 Txt Window Title 필드에 연결하세요 - 탭을 전환할 때마다
//        스크립트가 알아서 텍스트를 바꿔줍니다. 비워두면 그냥 무시합니다.
//   5) 버튼으로도 여닫고 싶다면, 그 버튼의 OnClick에 이 컴포넌트의 ToggleQuest()를 연결하세요.
//      L 키는 코드에서 자동으로 처리되므로 따로 설정할 게 없습니다.
//   6) 이 오브젝트는 항상 활성화(Active) 상태로 두세요 - UICanvas가 SetActive로 껐다 켜는 게 아니라,
//      이 스크립트가 CanvasGroup 알파로 보이기/숨기기를 처리합니다. 씬 시작 시 기본적으로 닫혀있습니다
//      (알파 0, 상호작용 불가).
//
// [UICanvas 연동]
//   IUIWindow를 구현해서, UICanvas가 "팝업 하나만 열리게, 열려있는 동안 게임 시간을 멈추게"
//   관리해줍니다(UIInventory와 완전히 같은 방식). ToggleQuest()(L 키/버튼)는
//   UICanvas.Instance.OpenUI()/CloseUI()를 호출할 뿐이고, 실제 Open()/Close()는 UICanvas가 그 안에서
//   호출해줍니다 - 직접 이 컴포넌트의 Open()/Close()를 호출하지 마세요(팝업 하나만 열리게 하는 관리가
//   깨집니다).
// ============================================================================

using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class UIQuest : MonoBehaviour, IUIWindow
{
    [Header("사이드 탭")]
    [SerializeField] Image _imgSideQuestBlack;
    [SerializeField] Image _imgSideClearQuestBlack;
    [Tooltip("QuestWindow/ClearQuestWindow 탭 위에 뜨는 타이틀 텍스트입니다 - 탭을 전환할 때마다 " +
              "\"진행중인 퀘스트\"/\"완료된 퀘스트\"로 자동으로 바뀝니다. 비워두면 무시합니다.")]
    [SerializeField] TextMeshProUGUI _txtWindowTitle;

    [Header("배경 - QuestWindow/ClearQuestWindow가 이 ScrollRect 하나를 공유합니다")]
    [SerializeField] ScrollRect _viewQuest;
    [Tooltip("QuestWindow(진행 중인 퀘스트) 탭에서 쓰는 항목 프리팹입니다.")]
    [SerializeField] UIQuestBar _entryPrefab;
    [Tooltip("ClearQuestWindow(완료된 퀘스트) 탭에서 쓰는 항목 프리팹입니다 - UIQuestBar와 별도로 " +
              "만든 프리팹입니다(UIClearQuestBar.cs 참고).")]
    [SerializeField] UIClearQuestBar _clearEntryPrefab;

    [Header("표시/숨김")]
    public float fadeDuration = 0.15f;

    private CanvasGroup canvasGroup;
    private readonly List<UIQuestBar> activeQuestEntries = new List<UIQuestBar>();
    private readonly List<UIClearQuestBar> clearedQuestEntries = new List<UIClearQuestBar>();
    private Tween fadeTween;
    private bool isOpen;
    private bool isShowingQuestWindow = true;
    private bool subscribedToQuest;

    private InputAction toggleAction;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        SetSideTabVisual(true); // 씬 저장 상태와 무관하게 항상 QuestWindow 탭으로 시작합니다.

        toggleAction = new InputAction("ToggleQuest", InputActionType.Button, "<Keyboard>/l");
    }

    private void OnEnable()
    {
        toggleAction.Enable();
    }

    private void OnDisable()
    {
        toggleAction.Disable();

        if (subscribedToQuest && QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestAdded -= HandleQuestChanged;
            QuestManager.Instance.OnQuestProgressChanged -= HandleQuestChanged;
            QuestManager.Instance.OnQuestReadyToTurnIn -= HandleQuestChanged;
            QuestManager.Instance.OnQuestCompleted -= HandleQuestChanged;
            subscribedToQuest = false;
        }
    }

    // QuestManager 이벤트 구독은 Awake/OnEnable이 아니라 Start()에서 합니다 - UIInventory의
    // PlayerInventory 구독과 같은 이유입니다(씬 로드 시 모든 Awake()가 어떤 Start()보다도 먼저
    // 끝나는 게 유니티가 보장하는 순서라서, Start() 시점이면 QuestManager.Instance가 이미 확실히
    // 설정되어 있습니다).
    private void Start()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestAdded += HandleQuestChanged;
            QuestManager.Instance.OnQuestProgressChanged += HandleQuestChanged;
            QuestManager.Instance.OnQuestReadyToTurnIn += HandleQuestChanged;
            QuestManager.Instance.OnQuestCompleted += HandleQuestChanged;
            subscribedToQuest = true;
        }

        RefreshEntries();
    }

    private void Update()
    {
        if (toggleAction.WasPressedThisFrame())
        {
            ToggleQuest();
        }
    }

    /// <summary>L 키/버튼 OnClick에서 호출하는 열기/닫기 토글 함수입니다. UICanvas에게 요청만 하고,
    /// 실제 Open()/Close() 호출은 UICanvas가 해줍니다 - 그래야 팝업이 한 번에 하나만 열리고, 여는 동안
    /// 게임 시간이 멈추는 게 같이 관리됩니다. 다른 스크립트에서 이 창을 열고 싶으면(예: UIIngame의
    /// 퀘스트 버튼) UICanvas.Instance.Quest.ToggleQuest()로 접근하세요.</summary>
    public void ToggleQuest()
    {
        if (isOpen) UICanvas.Instance.CloseUI(gameObject);
        else UICanvas.Instance.OpenUI(gameObject);
    }

    /// <summary>X(나가기) 버튼 OnClick 전용입니다. ToggleQuest()는 이 창이 열려있는 동안만 호출될 것을
    /// 전제로 하면 결과가 완전히 같지만, UICharacterInfo.ClickExitButton()/UIOption.ClickExitButton()과
    /// 이름을 맞춰서 다른 창들과 똑같은 이름의 함수를 X 버튼에 연결할 수 있게 별도로 만들어뒀습니다.
    /// [중요] 절대 Close()를 OnClick에 직접 연결하지 마세요 - Close()는 이 창을 "보이기/숨기기"만
    /// 담당할 뿐 UICanvas.currentPopup을 비우거나 Time.timeScale을 되돌리지 않아서, 화면은 닫힌 것처럼
    /// 보여도 게임 시간이 계속 멈춰있는 상태로 남는 버그가 생깁니다(IUIWindow.cs 상단 경고 참고 -
    /// 실제로 이 버그가 보고되었습니다). 반드시 이 함수 또는 ToggleQuest()를 통해서만 닫으세요.</summary>
    public void ClickExitButton()
    {
        SoundManager.Instance.PlayUIClickSfx();
        UICanvas.Instance.CloseUI(gameObject);
    }

    /// <summary>IUIWindow 구현. UICanvas.OpenUI()가 호출합니다 - 직접 호출하지 말고 ToggleQuest()나
    /// UICanvas.Instance.OpenUI(gameObject)를 쓰세요.</summary>
    public void Open()
    {
        if (isOpen) return;
        isOpen = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SetSideTabVisual(true); // 열 때마다 항상 QuestWindow 탭부터 보여줍니다.
        isShowingQuestWindow = true;
        RefreshEntries(); // 닫혀있는 동안 진행됐을 수 있는 퀘스트 상태를 여는 순간 최신으로 맞춥니다.

        fadeTween?.Kill();
        fadeTween = canvasGroup.DOFade(1f, fadeDuration).SetUpdate(true); // 게임이 멈춰도(Time.timeScale = 0) 페이드는 정상 속도로 재생됩니다.
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    /// <summary>IUIWindow 구현. UICanvas.CloseUI()가 호출합니다 - 직접 호출하지 말고 ToggleQuest()나
    /// UICanvas.Instance.CloseUI(gameObject)를 쓰세요. 닫히는 순간 커서를 무조건 다시 잠그고 숨깁니다
    /// (UIInventory.Close() 참고 - 열기 직전 상태를 복원하는 대신 항상 게임플레이 기본 상태로
    /// 되돌리는 방식으로 통일했습니다).</summary>
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

    // ------------------------------------------------------------------
    // 사이드 탭 전환 (QuestWindow ↔ ClearQuestWindow)
    // ------------------------------------------------------------------

    public void ClickSideQuestButton()
    {
        SoundManager.Instance.PlayUIClickSfx();
        if (isShowingQuestWindow) return; // 이미 QuestWindow 상태면 무시.
        isShowingQuestWindow = true;
        SetSideTabVisual(true);
        RefreshEntries();
    }

    public void ClickSideClearQuestButton()
    {
        SoundManager.Instance.PlayUIClickSfx();
        if (!isShowingQuestWindow) return; // 이미 ClearQuestWindow 상태면 무시.
        isShowingQuestWindow = false;
        SetSideTabVisual(false);
        RefreshEntries();
    }

    /// <summary>선택 안 된 쪽을 어둡게 표시하는 sideBlack 이미지들과, 위에 뜨는 타이틀 텍스트를
    /// 갱신합니다(UICharacterInfo의 SetActiveTab과 같은 방식) - 실제 목록 내용을 다시 그리는 건
    /// RefreshEntries()가 따로 담당합니다.</summary>
    private void SetSideTabVisual(bool showQuestWindow)
    {
        if (_imgSideQuestBlack != null) _imgSideQuestBlack.gameObject.SetActive(!showQuestWindow);
        if (_imgSideClearQuestBlack != null) _imgSideClearQuestBlack.gameObject.SetActive(showQuestWindow);

        if (_txtWindowTitle != null) _txtWindowTitle.text = showQuestWindow ? "진행중인 퀘스트" : "완료된 퀘스트";
    }

    // ------------------------------------------------------------------
    // 목록 표시
    // ------------------------------------------------------------------

    private void HandleQuestChanged(QuestProgress progress)
    {
        RefreshEntries();
    }

    /// <summary>지금 선택된 탭(isShowingQuestWindow)에 맞는 목록만 View Quest에 새로 그립니다. 두 탭이
    /// 같은 ScrollRect를 공유하므로, 먼저 기존에 떠 있던 항목(어느 탭 것이든)을 전부 지운 뒤 다시
    /// 채웁니다.</summary>
    private void RefreshEntries()
    {
        ClearAllEntries();

        if (QuestManager.Instance == null) return;

        if (isShowingQuestWindow)
        {
            foreach (QuestProgress progress in QuestManager.Instance.ActiveQuests)
            {
                UIQuestBar entry = Instantiate(_entryPrefab, _viewQuest.content);
                entry.Setup(progress);
                activeQuestEntries.Add(entry);
            }
        }
        else
        {
            foreach (QuestProgress progress in QuestManager.Instance.CompletedQuests)
            {
                UIClearQuestBar entry = Instantiate(_clearEntryPrefab, _viewQuest.content);
                entry.Setup(progress);
                clearedQuestEntries.Add(entry);
            }
        }
    }

    private void ClearAllEntries()
    {
        foreach (UIQuestBar entry in activeQuestEntries)
        {
            if (entry != null) Destroy(entry.gameObject);
        }
        activeQuestEntries.Clear();

        foreach (UIClearQuestBar entry in clearedQuestEntries)
        {
            if (entry != null) Destroy(entry.gameObject);
        }
        clearedQuestEntries.Clear();
    }
}