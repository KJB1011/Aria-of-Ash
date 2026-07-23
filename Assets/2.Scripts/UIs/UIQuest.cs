// ============================================================================
// UIQuest.cs
// ----------------------------------------------------------------------------
// L키 또는 버튼 클릭으로 열고 닫는 퀘스트 로그 창입니다. UIInventory.cs와 완전히 같은 구조입니다 -
// 이 스크립트 자체는 static Instance를 두지 않고, 다른 스크립트는 UICanvas.Instance.Quest로 꺼내
// 씁니다. QuestManager의 OnQuestAdded/OnQuestProgressChanged/OnQuestCompleted 이벤트를 구독해서
// 창이 열려있든 아니든 항상 최신 목록을 유지하다가, 열렸을 때 바로 맞는 상태가 보이게 합니다.
//
// [진행 중 + 완료 목록을 함께 표시]
//   RefreshEntries()가 QuestManager.ActiveQuests를 먼저, CompletedQuests를 그 아래에 이어서 그립니다.
//   UIQuestBar가 완료 여부(_imgCompleted)를 함께 표시하므로 한 목록 안에서 구분됩니다.
//
// [씬 준비]
//   1) 퀘스트 창 패널(전체를 여닫을 오브젝트)에 이 스크립트와 CanvasGroup을 붙이세요(CanvasGroup은
//      RequireComponent로 자동 추가됩니다) - 여닫을 때 DOTween으로 알파를 페이드합니다.
//   2) Vertical Layout Group 등을 붙인 Content를 가진 ScrollRect를 View Quest 필드에 연결하세요.
//   3) UIQuestBar 프리팹을 Entry Prefab 필드에 연결하세요.
//   4) 버튼으로도 여닫고 싶다면, 그 버튼의 OnClick에 이 컴포넌트의 ToggleQuest()를 연결하세요.
//      L 키는 코드에서 자동으로 처리되므로 따로 설정할 게 없습니다.
//   5) 이 오브젝트는 항상 활성화(Active) 상태로 두세요 - UICanvas가 SetActive로 껐다 켜는 게 아니라,
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
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class UIQuest : MonoBehaviour, IUIWindow
{
    [SerializeField] ScrollRect _viewQuest;
    [SerializeField] UIQuestBar _entryPrefab;

    [Header("표시/숨김")]
    public float fadeDuration = 0.15f;

    private CanvasGroup canvasGroup;
    private readonly List<UIQuestBar> activeEntries = new List<UIQuestBar>();
    private Tween fadeTween;
    private bool isOpen;
    private bool subscribedToQuest;

    // 퀘스트 창을 열기 직전의 커서 상태를 저장해뒀다가, 닫을 때 그대로 복원합니다 - UIInventory와
    // 같은 이유입니다(열기 전에 이미 커서가 풀려있었다면 닫아도 다시 잠그지 않습니다).
    private CursorLockMode previousCursorLockState;
    private bool previousCursorVisible;

    private InputAction toggleAction;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

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

    /// <summary>IUIWindow 구현. UICanvas.OpenUI()가 호출합니다 - 직접 호출하지 말고 ToggleQuest()나
    /// UICanvas.Instance.OpenUI(gameObject)를 쓰세요.</summary>
    public void Open()
    {
        if (isOpen) return;
        isOpen = true;

        previousCursorLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        RefreshEntries(); // 닫혀있는 동안 진행됐을 수 있는 퀘스트 상태를 여는 순간 최신으로 맞춥니다.

        fadeTween?.Kill();
        fadeTween = canvasGroup.DOFade(1f, fadeDuration).SetUpdate(true); // 게임이 멈춰도(Time.timeScale = 0) 페이드는 정상 속도로 재생됩니다.
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    /// <summary>IUIWindow 구현. UICanvas.CloseUI()가 호출합니다 - 직접 호출하지 말고 ToggleQuest()나
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

    private void HandleQuestChanged(QuestProgress progress)
    {
        RefreshEntries();
    }

    /// <summary>진행 중인 퀘스트를 먼저, 완료된 퀘스트를 그 아래에 이어서 그립니다.</summary>
    private void RefreshEntries()
    {
        foreach (UIQuestBar entry in activeEntries)
        {
            if (entry != null) Destroy(entry.gameObject);
        }
        activeEntries.Clear();

        if (QuestManager.Instance == null) return;

        foreach (QuestProgress progress in QuestManager.Instance.ActiveQuests)
        {
            UIQuestBar entry = Instantiate(_entryPrefab, _viewQuest.content);
            entry.Setup(progress);
            activeEntries.Add(entry);
        }

        foreach (QuestProgress progress in QuestManager.Instance.CompletedQuests)
        {
            UIQuestBar entry = Instantiate(_entryPrefab, _viewQuest.content);
            entry.Setup(progress);
            activeEntries.Add(entry);
        }
    }
}