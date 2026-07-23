// ============================================================================
// UIIngameInteraction.cs
// ----------------------------------------------------------------------------
// 플레이어 옆에 뜨는 "상호작용 가능 목록" UI입니다. InteractionDetector(Player)가 감지한
// 범위 안의 상호작용 대상들을 매 프레임 읽어와서 UILootInterBar 항목으로 하나씩 보여주고,
// 지금 마우스 휠로 선택된 대상에 체크마크를 켭니다. 범위 안에 대상이 하나도 없으면 자동으로
// 숨겨지고, 하나라도 들어오면 페이드인으로 나타납니다.
//
// [원래 스크립트와 달라진 점]
//   _viewIngameInteraction을 UI Toolkit의 ScrollView 대신 UGUI의 ScrollRect로 바꿨습니다.
//   UILootInterBar가 Image/TextMeshProUGUI를 쓰는 일반 GameObject 프리팹(UGUI)이라, UI Toolkit의
//   VisualElement 트리에는 애초에 들어갈 수 없습니다. 씬에서 이 필드에 UGUI Scroll View의
//   ScrollRect 컴포넌트를 연결해주세요.
//
// [씬 준비]
//   1) 플레이어 옆(혹은 원하는 화면 위치)에 배치할 패널에 이 스크립트와 CanvasGroup을 붙이세요
//      (CanvasGroup은 RequireComponent로 자동 추가됩니다).
//   2) 그 안에 Vertical Layout Group 등을 붙인 Content를 가진 ScrollRect를 만들고
//      View Ingame Interaction 필드에 연결하세요.
//   3) UILootInterBar 프리팹을 Bar Prefab 필드에 연결하세요.
//   4) Detector는 비워두면 Awake()에서 씬의 InteractionDetector(Player에 붙인 컴포넌트)를
//      자동으로 찾습니다.
// ============================================================================

using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class UIIngameInteraction : MonoBehaviour
{
    [SerializeField] ScrollRect _viewIngameInteraction;
    [SerializeField] UILootInterBar _barPrefab;
    [SerializeField] InteractionDetector _detector;

    [Header("표시/숨김")]
    public float fadeDuration = 0.15f;

    private CanvasGroup canvasGroup;
    private readonly List<UILootInterBar> activeBars = new List<UILootInterBar>();
    private readonly List<IInteractable> lastInteractables = new List<IInteractable>();
    private Tween fadeTween;
    private bool isVisible;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (_detector == null)
        {
            _detector = FindFirstObjectByType<InteractionDetector>();
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void Update()
    {
        // 대화 중이거나(TalkManager.IsTalking) 인벤토리/옵션 등 다른 팝업이 열려있는 동안엔
        // (UICanvas.IsUIOpen이 이 전부를 이미 포함해서 확인해줍니다 - PlayerController의
        // IsAnyUIOpen()과 같은 방식) 상호작용 프롬프트를 숨깁니다. NPC와 대화하는 도중에도
        // InteractionDetector는 계속 스캔 중이라(F키 자체는 커서가 풀려서 막히지만) 범위 안에
        // NPC가 그대로 남아있어 프롬프트가 계속 떠 있었는데, 대화 중엔 어차피 F를 눌러도 아무 일도
        // 일어나지 않으니 UI도 함께 숨기는 게 자연스럽습니다.
        if (IsBlockingUIOpen())
        {
            Hide();
            return;
        }

        IReadOnlyList<IInteractable> current = _detector.NearbyInteractables;

        // 범위 안의 구성(멤버/순서)이 실제로 바뀐 프레임에만 UILootInterBar들을 다시 만듭니다 -
        // 매 프레임 통째로 다시 만들면 스캔 간격(0.1초)마다 불필요한 Instantiate/Destroy가 반복됩니다.
        if (!ListsEqual(current, lastInteractables))
        {
            RebuildBars(current);
        }

        UpdateCheckMarks();

        if (current.Count > 0) Show();
        else Hide();
    }

    /// <summary>인벤토리/옵션/알림/대화 등 다른 UI가 지금 하나라도 열려있는지 확인합니다.
    /// UICanvas.Instance가 없는 씬(테스트 씬 등)에서도 안전하게 false를 돌려줍니다.</summary>
    private static bool IsBlockingUIOpen()
    {
        return UICanvas.Instance != null && UICanvas.Instance.IsUIOpen;
    }

    private void RebuildBars(IReadOnlyList<IInteractable> current)
    {
        foreach (UILootInterBar bar in activeBars)
        {
            if (bar != null) Destroy(bar.gameObject);
        }
        activeBars.Clear();

        for (int i = 0; i < current.Count; i++)
        {
            UILootInterBar bar = Instantiate(_barPrefab, _viewIngameInteraction.content);
            bar.SetLootInterBar(current[i].InteractionName);
            activeBars.Add(bar);
        }

        lastInteractables.Clear();
        lastInteractables.AddRange(current);
    }

    private void UpdateCheckMarks()
    {
        int selected = _detector.SelectedIndex;
        for (int i = 0; i < activeBars.Count; i++)
        {
            activeBars[i].SetCheckMark(i == selected);
        }
    }

    private void Show()
    {
        if (isVisible) return;
        isVisible = true;

        fadeTween?.Kill();
        fadeTween = canvasGroup.DOFade(1f, fadeDuration).SetUpdate(true); // 다른 팝업(UIInventory 등)이 게임을 멈춰도(Time.timeScale = 0) 이 페이드는 얼어붙지 않습니다.
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    private void Hide()
    {
        if (!isVisible) return;
        isVisible = false;

        fadeTween?.Kill();
        fadeTween = canvasGroup.DOFade(0f, fadeDuration).SetUpdate(true);
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>순서까지 포함해서 두 목록이 완전히 같은지(참조 기준) 비교합니다.</summary>
    private static bool ListsEqual(IReadOnlyList<IInteractable> a, List<IInteractable> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (!ReferenceEquals(a[i], b[i])) return false;
        }
        return true;
    }
}