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
// [완료 연출 - OnQuestCompleted만 따로 처리]
//   OnQuestAdded/OnQuestProgressChanged/OnQuestReadyToTurnIn은 그대로 HandleQuestChanged()가
//   받아서 RefreshEntries()로 전체를 다시 그립니다. 하지만 OnQuestCompleted는 곧바로 다시 그리지
//   않고 HandleQuestCompleted()가 따로 받습니다 - 완료된 그 퀘스트의 UIIngameQuestBar를
//   activeEntries에서 골라내 completingEntries로 옮긴 뒤, PlayCompletedAndRemove() 코루틴이
//   1) UIIngameQuestBar.PlayCompletedEffect()로 ClearImage 슬라이드 연출을 재생하고,
//   2) completedDisplayDuration(기본 3초)만큼 더 기다렸다가,
//   3) 그제서야 그 항목을 Destroy합니다.
//   이렇게 분리한 이유는, 완료된 퀘스트는 QuestManager 쪽에서 이미 ActiveQuests에서 빠진 상태라
//   RefreshEntries()를 그대로 불러버리면(모든 이벤트가 원래 그랬듯) 그 즉시 항목이 통째로
//   Destroy되어 연출을 재생할 틈이 없기 때문입니다. completingEntries에 있는 동안은
//   activeEntries/RefreshEntries()가 전혀 건드리지 않으므로, 다른 퀘스트가 그 사이에 추가/진행돼도
//   서로 꼬이지 않습니다. Show()/Hide() 판단도 completingEntries가 하나라도 있으면 계속 보이도록
//   함께 확인합니다(연출 도중에 패널이 사라지는 일이 없도록).
//
// [씬 준비]
//   1) HUD의 원하는 위치(보통 화면 한쪽 구석)에 패널을 만들고 이 스크립트와 CanvasGroup을 붙이세요
//      (CanvasGroup은 RequireComponent로 자동 추가됩니다).
//   2) 그 안에 Vertical Layout Group 등을 붙인 Content를 가진 ScrollRect를 만들고 View Ingame Quest
//      필드에 연결하세요.
//   3) UIIngameQuestBar.cs가 붙은 프리팹을 Entry Prefab 필드에 연결하세요(ClearImage 연출 설정은
//      그 프리팹/스크립트 쪽에서 합니다 - UIIngameQuestBar.cs 상단 주석 참고).
//   4) 완료 연출이 다 끝난 뒤 실제로 사라지기까지 대기할 시간을 Completed Display Duration에서
//      조절하세요(기본 3초).
// ============================================================================

using System.Collections;
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

    [Header("완료 연출")]
    [Tooltip("퀘스트가 완료되어 ClearImage 연출까지 다 재생한 뒤, 실제로 항목이 사라지기까지 " +
              "추가로 기다리는 시간(초)입니다.")]
    public float completedDisplayDuration = 3f;

    private CanvasGroup canvasGroup;
    private readonly List<UIIngameQuestBar> activeEntries = new List<UIIngameQuestBar>();
    private readonly List<UIIngameQuestBar> completingEntries = new List<UIIngameQuestBar>();
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
            QuestManager.Instance.OnQuestCompleted += HandleQuestCompleted;
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
            QuestManager.Instance.OnQuestCompleted -= HandleQuestCompleted;
        }
    }

    private void HandleQuestChanged(QuestProgress progress)
    {
        RefreshEntries();
    }

    /// <summary>OnQuestCompleted 전용 핸들러입니다(파일 상단 [완료 연출] 참고). 이미 화면에 떠 있는
    /// 그 퀘스트의 항목을 찾아 activeEntries에서 completingEntries로 옮기고, 연출 재생 + 대기 +
    /// 제거를 담당하는 코루틴을 시작합니다. 화면에 떠 있는 항목을 못 찾으면(추적기를 연 적이 없는 등
    /// 드문 경우) 그냥 RefreshEntries()로 정리합니다.</summary>
    private void HandleQuestCompleted(QuestProgress progress)
    {
        UIIngameQuestBar entry = activeEntries.Find(e => e != null && e.Progress == progress);
        if (entry == null)
        {
            RefreshEntries();
            return;
        }

        activeEntries.Remove(entry);
        completingEntries.Add(entry);
        StartCoroutine(PlayCompletedAndRemove(entry));
    }

    /// <summary>ClearImage 슬라이드 연출을 재생하고, completedDisplayDuration만큼 더 기다린 뒤 이
    /// 항목을 실제로 Destroy합니다. entry가 completingEntries에 있는 동안은 RefreshEntries()가
    /// 손대지 않으므로, 그 사이 다른 퀘스트가 추가/진행돼도 서로 꼬이지 않습니다.</summary>
    private IEnumerator PlayCompletedAndRemove(UIIngameQuestBar entry)
    {
        Tween moveTween = entry.PlayCompletedEffect();
        if (moveTween != null) yield return moveTween.WaitForCompletion();

        yield return new WaitForSeconds(completedDisplayDuration);

        completingEntries.Remove(entry);
        if (entry != null) Destroy(entry.gameObject);

        if (activeEntries.Count == 0 && completingEntries.Count == 0) Hide();
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

        // completingEntries(완료 연출 재생 중인 항목)가 하나라도 있으면 다른 활성 퀘스트가 없어도
        // 계속 보여야 합니다 - 연출 도중에 패널이 사라지면 안 되기 때문입니다.
        if (activeEntries.Count > 0 || completingEntries.Count > 0) Show();
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