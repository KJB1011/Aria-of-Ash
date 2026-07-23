// ============================================================================
// UIIngameQuest.cs
// ----------------------------------------------------------------------------
// HUD에 항상 떠 있는 퀘스트 진행 추적기입니다. UIIngameInteraction.cs와 같은 구조입니다 - 다만
// 매 프레임 폴링하는 대신, QuestManager가 발행하는 이벤트(OnQuestAdded/OnQuestProgressChanged/
// OnQuestCompleted)를 구독해서 실제로 뭔가 바뀐 순간에만 다시 그립니다.
//
// [표시 규칙]
//   진행 중인 퀘스트(QuestManager.ActiveQuests)가 하나도 없으면 자동으로 숨겨지고, 하나라도 생기면
//   페이드인으로 나타납니다 - UIIngameInteraction의 Show()/Hide() 패턴과 동일합니다. 클릭 대상이
//   아니므로 CanvasGroup의 interactable/blocksRaycasts는 항상 꺼둡니다(순수 표시 전용 HUD).
//
// [씬 준비]
//   1) HUD의 원하는 위치(보통 화면 한쪽 구석)에 패널을 만들고 이 스크립트와 CanvasGroup을 붙이세요
//      (CanvasGroup은 RequireComponent로 자동 추가됩니다).
//   2) 그 안에 Vertical Layout Group 등을 붙인 Content를 가진 ScrollRect를 만들고 View Ingame Quest
//      필드에 연결하세요.
//   3) UIIngameQuestBar.cs가 붙은 프리팹을 Entry Prefab 필드에 연결하세요.
// ============================================================================

using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class UIIngameQuest : MonoBehaviour
{
    [SerializeField] ScrollRect _viewIngameQuest;
    [SerializeField] UIIngameQuestBar _entryPrefab;

    [Header("표시/숨김")]
    public float fadeDuration = 0.15f;

    private CanvasGroup canvasGroup;
    private readonly List<UIIngameQuestBar> activeEntries = new List<UIIngameQuestBar>();
    private Tween fadeTween;
    private bool isVisible;
    private bool subscribedToQuest;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    // QuestManager 이벤트 구독은 Awake()가 아니라 Start()에서 합니다 - 씬 로드 시점에 존재하는 모든
    // 오브젝트의 Awake()는 어떤 오브젝트의 Start()보다도 먼저 전부 끝나는 게 유니티가 보장하는
    // 순서라서, Start() 시점이면 QuestManager.Instance가 이미 확실히 설정되어 있습니다.
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

    private void OnDestroy()
    {
        if (subscribedToQuest && QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestAdded -= HandleQuestChanged;
            QuestManager.Instance.OnQuestProgressChanged -= HandleQuestChanged;
            QuestManager.Instance.OnQuestReadyToTurnIn -= HandleQuestChanged;
            QuestManager.Instance.OnQuestCompleted -= HandleQuestChanged;
        }
    }

    private void HandleQuestChanged(QuestProgress progress)
    {
        // 완료된 퀘스트는 QuestManager.ActiveQuests에서 이미 빠져있으므로, 다시 그리면 자동으로
        // 추적기 목록에서도 사라집니다.
        RefreshEntries();
    }

    private void RefreshEntries()
    {
        foreach (UIIngameQuestBar entry in activeEntries)
        {
            if (entry != null) Destroy(entry.gameObject);
        }
        activeEntries.Clear();

        if (QuestManager.Instance != null)
        {
            foreach (QuestProgress progress in QuestManager.Instance.ActiveQuests)
            {
                UIIngameQuestBar entry = Instantiate(_entryPrefab, _viewIngameQuest.content);
                entry.Setup(progress);
                activeEntries.Add(entry);
            }
        }

        if (activeEntries.Count > 0) Show();
        else Hide();
    }

    private void Show()
    {
        if (isVisible) return;
        isVisible = true;

        fadeTween?.Kill();
        fadeTween = canvasGroup.DOFade(1f, fadeDuration).SetUpdate(true); // 다른 팝업이 게임을 멈춰도(Time.timeScale = 0) 이 페이드는 얼어붙지 않습니다.
    }

    private void Hide()
    {
        if (!isVisible) return;
        isVisible = false;

        fadeTween?.Kill();
        fadeTween = canvasGroup.DOFade(0f, fadeDuration).SetUpdate(true);
    }
}