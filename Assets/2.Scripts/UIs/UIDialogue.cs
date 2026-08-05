// ============================================================================
// UIDialogue.cs
// ----------------------------------------------------------------------------
// TalkManager가 재생하는 대화를 화면에 그리는 UI입니다. 인벤토리/옵션처럼 핫키나 버튼으로 직접
// 여닫는 게 아니라, TalkManager가 발행하는 OnTalkChanged/OnTalkEnded 이벤트만 구독해서 그때그때
// 보이기/숨기기와 내용 갱신을 합니다 - 커서 잠금 해제, Time.timeScale 정지, 카메라 전환은 이미
// TalkManager가 전부 처리하므로 이 스크립트는 순수하게 "화면에 무엇을 보여줄지"만 담당합니다.
// 그래서 UICanvas의 팝업(OpenUI/CloseUI) 대상으로 등록하지 않았습니다.
//
// [타이핑 연출 + 2단계 클릭]
//   Talks가 바뀌면 대사를 한 글자씩 타이핑하듯 보여줍니다(typingCharsPerSecond). 이 타이핑은
//   Time.timeScale이 0인 동안에도 정상 속도로 진행되도록 Time.unscaledDeltaTime을 씁니다(다른
//   팝업들의 DOTween SetUpdate(true)와 같은 이유). 대화창(또는 화면)을 클릭하면(ClickAdvance()) -
//   아직 타이핑 중이면 즉시 전체 텍스트를 다 보여주기만 하고, 이미 다 보여준 상태라면 그때
//   비로소 다음으로 진행합니다(비주얼노벨/원신에서 흔한 2단계 클릭 방식). Space 키로도 같은
//   동작을 할 수 있게 해뒀습니다.
//
// [선택지]
//   타이핑이 끝난 시점에 그 Talks에 선택지가 있으면(HasChoices) 선택지 버튼들을 만들어서
//   보여주고, ClickAdvance()로는 진행할 수 없게 합니다(TalkManager.Advance()가 선택지 있는
//   Talks에서는 스스로 무시하므로 여기서 따로 막을 필요는 없습니다). 선택지 버튼을 누르면
//   UIDialogueChoiceButton이 알려주는 index를 그대로 TalkManager.SelectChoice()에 넘깁니다.
//
// [레터박스]
//   이번 구현에는 포함하지 않았습니다 - 나중에 필요해지면 CanvasGroup과 같이 페이드/슬라이드되는
//   위아래 바 이미지를 추가하면 됩니다.
//
// [스킵 - 선택지가 나올 때까지 자동 진행]
//   ClickSkipButton()으로 켜고 끄는 토글입니다. 켜져 있는 동안엔 각 Talks의 타이핑 연출을
//   건너뛰고 대사를 즉시 전부 보여준 뒤, 그 Talks에 선택지가 없으면 skipStepDelay(기본 0.05초)
//   뒤에 자동으로 TalkManager.Advance()를 호출해서 다음 줄로 넘어갑니다 - 이 과정을 선택지가
//   있는 Talks를 만나거나 대화가 끝날 때까지 반복합니다. 선택지가 있는 Talks에 도달하면 자동으로
//   스킵을 끄고 선택지를 보여줘서, 대신 골라주는 일은 없습니다(플레이어가 직접 골라야 함). 대화가
//   끝나면(OnTalkEnded) 스킵 상태도 자동으로 초기화되므로, 다음 대화는 항상 스킵이 꺼진 상태로
//   시작합니다.
//
// [씬 준비]
//   1) 대화창 패널(항상 활성화 상태로 두세요 - Hierarchy에서 이 오브젝트의 Active 체크박스를 끄지
//      마세요. 평소에 안 보이는 건 CanvasGroup 알파가 0이라 그런 것뿐이고, 오브젝트 자체를
//      비활성화해두면 대화 시작 시점에 "코루틴을 시작할 수 없다"는 에러가 납니다 - ShowPanel()에
//      안전장치를 넣어뒀지만, 애초에 항상 켜두는 게 원래 의도입니다)에 이 스크립트와 CanvasGroup을
//      붙이세요.
//   2) 화자 이름/대사 텍스트를 각각 Txt Speaker Name/Txt Dialogue 필드에 연결하세요.
//   3) 대화창(또는 화면 전체를 덮는) 영역에 Button을 하나 만들고 OnClick에 ClickAdvance()를
//      연결하세요 - 이 버튼이 "클릭해서 다음으로" 역할을 합니다.
//   4) "다음" 표시(▼ 아이콘 등, 선택 사항)를 Advance Indicator 필드에 연결하세요 - 타이핑 중이거나
//      선택지가 떠 있는 동안엔 자동으로 숨겨집니다. 안 붙여도 나머지 기능은 정상 동작합니다.
//   5) 선택지 버튼 프리팹(UIDialogueChoiceButton이 붙은 것)을 Choice Prefab에, 그 버튼들이
//      나열될 부모(Vertical Layout Group 등)를 Choice Container에 연결하세요.
//   6) 스킵 버튼을 하나 만들고 OnClick에 ClickSkipButton()을 연결하세요(선택 사항 - 안 만들어도
//      나머지 기능은 정상 동작합니다). 버튼 모양을 토글처럼 바꾸고 싶다면 IsSkipping 프로퍼티로
//      지금 스킵 중인지 확인해서 색을 바꾸는 등으로 활용하면 됩니다.
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CanvasGroup))]
public class UIDialogue : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _txtSpeakerName;
    [SerializeField] TextMeshProUGUI _txtDialogue;
    [SerializeField] GameObject _advanceIndicator;
    [SerializeField] Transform _choiceContainer;
    [SerializeField] UIDialogueChoiceButton _choicePrefab;

    [Header("표시/숨김")]
    public float fadeDuration = 0.15f;

    [Header("타이핑 연출")]
    [Tooltip("초당 몇 글자씩 보여줄지입니다. 0 이하면 한 번에 전부 보여줍니다.")]
    public float typingCharsPerSecond = 30f;

    [Header("스킵 (선택지가 나올 때까지 자동 진행)")]
    [Tooltip("스킵 중 한 줄 넘길 때마다 대기하는 시간(초, 실제 시간 기준)입니다. 너무 짧으면 무슨 " +
              "내용이 지나갔는지 보이지 않고, 너무 길면 스킵한 느낌이 안 나므로 적당히 조절하세요.")]
    public float skipStepDelay = 0.05f;

    private CanvasGroup canvasGroup;
    private Tween fadeTween;
    private bool isVisible;
    private bool subscribedToTalk;

    private Coroutine typingRoutine;
    private bool isTyping;
    private string currentFullText;

    private bool isSkipping;
    private Coroutine skipRoutine;

    /// <summary>지금 스킵(자동 진행) 중인지 여부입니다. 스킵 버튼의 켜짐/꺼짐 표시 등에 활용할 수 있습니다.</summary>
    public bool IsSkipping => isSkipping;

    private readonly List<UIDialogueChoiceButton> activeChoiceButtons = new List<UIDialogueChoiceButton>();

    private InputAction advanceKeyAction;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        advanceKeyAction = new InputAction("DialogueAdvance", InputActionType.Button, "<Keyboard>/space");
    }

    private void OnEnable()
    {
        advanceKeyAction.Enable();
    }

    private void OnDisable()
    {
        advanceKeyAction.Disable();

        if (subscribedToTalk)
        {
            TalkManager.Instance.OnTalkChanged -= HandleTalkChanged;
            TalkManager.Instance.OnTalkEnded -= HandleTalkEnded;
            subscribedToTalk = false;
        }
    }

    // TalkManager.Instance를 참조하는 건 Awake/OnEnable이 아니라 Start()에서 합니다 - 씬 로드
    // 시점에 존재하는 모든 오브젝트의 Awake()는 어떤 오브젝트의 Start()보다도 먼저 전부 끝나는 게
    // 유니티가 보장하는 순서라서, Start() 시점이면 TalkManager.Instance가 이미 설정되어 있습니다.
    private void Start()
    {
        if (TalkManager.Instance == null)
        {
            Debug.LogWarning("[UIDialogue] 씬에 TalkManager가 없어서 대화 UI가 아무 것도 하지 않습니다.", this);
            return;
        }

        TalkManager.Instance.OnTalkChanged += HandleTalkChanged;
        TalkManager.Instance.OnTalkEnded += HandleTalkEnded;
        subscribedToTalk = true;
    }

    private void Update()
    {
        if (advanceKeyAction.WasPressedThisFrame())
        {
            ClickAdvance();
        }
    }

    /// <summary>대화창(또는 화면 전체 클릭 영역) Button의 OnClick에 연결하세요. 타이핑 중이면
    /// 즉시 전체 텍스트를 보여주고, 이미 다 보여준 상태면 다음으로 진행합니다(선택지가 떠 있는
    /// Talks라면 TalkManager.Advance()가 스스로 무시하므로 여기서 따로 막지 않습니다).</summary>
    public void ClickAdvance()
    {
        SoundManager.Instance.PlayUIClickSfx();
        if (!isVisible) return;

        if (isTyping)
        {
            CompleteTypingInstantly();
            return;
        }

        TalkManager.Instance.Advance();
    }

    private void HandleTalkChanged(TalkScript.Talks talk)
    {
        // isVisible 값과 무관하게(아래 ShowPanel() 호출 여부와 상관없이) 매번 무조건 확인합니다.
        // 자세한 이유는 EnsureActiveInHierarchy() 주석 참고.
        EnsureActiveInHierarchy();

        if (!isVisible) ShowPanel();

        _txtSpeakerName.text = talk.speakerName;
        ClearChoiceButtons();
        HideAdvanceIndicator();

        if (isSkipping)
        {
            // 스킵 중에는 타이핑 연출 없이 즉시 전부 보여주고, 다음 판단(OnLineFullyShown)으로 넘어갑니다.
            StopTypingIfRunning();
            currentFullText = talk.dialogueText ?? string.Empty;
            _txtDialogue.text = currentFullText;
            OnLineFullyShown();
        }
        else
        {
            StartTyping(talk.dialogueText);
        }
    }

    private void HandleTalkEnded()
    {
        StopTypingIfRunning();
        StopSkipRoutineIfRunning();
        isSkipping = false; // 대화가 끝났으니 다음 대화는 항상 스킵이 꺼진 상태로 시작합니다.
        ClearChoiceButtons();
        HidePanel();
    }

    /// <summary>스킵 버튼 OnClick에 연결하세요. 스킵을 켜고 끄는 토글입니다. 켜는 순간 지금
    /// 타이핑 중이면 즉시 완료시키고, 이미 다 보여준 줄이면 바로 다음 판단(선택지 확인/자동 진행
    /// 예약)을 다시 태웁니다. 끄면 예약되어 있던 자동 진행만 취소하고, 지금 보이는 줄은 그대로
    /// 둡니다(다음 줄부터 다시 타이핑 연출로 재생됩니다).</summary>
    public void ClickSkipButton()
    {
        SoundManager.Instance.PlayUIClickSfx();
        if (!isVisible) return;

        isSkipping = !isSkipping;

        if (!isSkipping)
        {
            StopSkipRoutineIfRunning();
            return;
        }

        if (isTyping) CompleteTypingInstantly();
        else OnLineFullyShown();
    }

    /// <summary>이 오브젝트(그리고 그 부모 계층 전부)가 실제로 Hierarchy 상에서 활성 상태인지
    /// 확인하고, 비활성화된 게 있으면 전부 강제로 켭니다. HandleTalkChanged()에서 isVisible 값과
    /// 무관하게 매번 호출합니다 - 아래 두 가지 문제를 동시에 막기 위해서입니다.
    ///
    /// [문제 1 - gameObject.activeSelf만으로는 부족함] 이전 수정에서는 ShowPanel() 안에서
    /// "!gameObject.activeSelf"만 검사했는데, activeSelf는 이 오브젝트 "자기 자신"의 체크박스만
    /// 봅니다. 만약 이 오브젝트의 부모(또는 조상) 중 하나가 비활성화되어 있으면, 이 오브젝트 자신의
    /// activeSelf는 여전히 true라서 검사를 통과하지만, 실제로는 activeInHierarchy가 false라
    /// StartCoroutine()이 똑같이 "게임 오브젝트가 비활성 상태다" 에러를 냅니다 - 바로 이게 수정을
    /// 했는데도 똑같은 에러가 계속 재현된 원인일 가능성이 높습니다. 그래서 이제 자기 자신부터
    /// 부모를 타고 올라가면서(transform.parent) 비활성화된 오브젝트를 전부 켭니다.
    ///
    /// [문제 2 - isVisible이 실제 상태와 어긋날 수 있음] 기존엔 이 안전장치가 ShowPanel() 안에만
    /// 있어서 "if (!isVisible) ShowPanel();" 조건을 통과해야만 실행됐습니다. 만약 무언가(다른
    /// 스크립트, 씬 세팅 등)가 isVisible 플래그와 무관하게 이 오브젝트나 그 부모를 직접
    /// SetActive(false)로 꺼버리면, isVisible은 여전히 true로 남아있어 ShowPanel() 자체가
    /// 호출되지 않고 안전장치도 건너뛰어집니다. 그래서 이 함수는 isVisible과 상관없이 매번
    /// 무조건 호출되도록 HandleTalkChanged() 맨 앞으로 옮겼습니다.
    ///
    /// 만약 이 로그가 계속 찍힌다면, 어떤 부모 오브젝트가 왜 꺼지는지(어떤 스크립트가
    /// SetActive(false)를 호출하는지) 원인 쪽을 찾아서 고치는 게 근본적인 해결책입니다 - 이
    /// 함수는 "일단 강제로 켜서 에러는 안 나게" 만드는 안전장치일 뿐입니다.</summary>
    private void EnsureActiveInHierarchy()
    {
        Transform t = transform;
        while (t != null)
        {
            if (!t.gameObject.activeSelf)
            {
                Debug.LogWarning($"[UIDialogue] '{t.gameObject.name}'이(가) 비활성화되어 있어서 " +
                                  "강제로 다시 켰습니다 - 대화 도중 이 오브젝트(또는 부모)를 끄는 " +
                                  "코드가 있는지 확인해보세요.", t.gameObject);
                t.gameObject.SetActive(true);
            }
            t = t.parent;
        }
    }

    private void ShowPanel()
    {
        isVisible = true;

        fadeTween?.Kill();
        fadeTween = canvasGroup.DOFade(1f, fadeDuration).SetUpdate(true); // 대화 중엔 Time.timeScale이 0이라 SetUpdate(true) 필요.
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    private void HidePanel()
    {
        isVisible = false;

        fadeTween?.Kill();
        fadeTween = canvasGroup.DOFade(0f, fadeDuration).SetUpdate(true);
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    // ------------------------------------------------------------------
    // 타이핑 연출 - Time.timeScale이 0인 동안에도 정상 속도로 진행되도록 unscaledDeltaTime을 씁니다.
    // ------------------------------------------------------------------

    private void StartTyping(string fullText)
    {
        StopTypingIfRunning();

        currentFullText = fullText ?? string.Empty;
        typingRoutine = StartCoroutine(TypeTextRoutine());
    }

    private IEnumerator TypeTextRoutine()
    {
        isTyping = true;
        _txtDialogue.text = string.Empty;

        float secondsPerChar = typingCharsPerSecond > 0f ? 1f / typingCharsPerSecond : 0f;
        float timer = 0f;
        int shownCount = 0;

        while (shownCount < currentFullText.Length)
        {
            timer += Time.unscaledDeltaTime;

            while (timer >= secondsPerChar && shownCount < currentFullText.Length)
            {
                timer -= secondsPerChar;
                shownCount++;
            }

            _txtDialogue.text = currentFullText.Substring(0, shownCount);
            yield return null;
        }

        OnLineFullyShown();
    }

    private void CompleteTypingInstantly()
    {
        StopTypingIfRunning();
        _txtDialogue.text = currentFullText;
        OnLineFullyShown();
    }

    /// <summary>지금 줄(대사)이 화면에 전부 나타난 시점에 호출됩니다 - 타이핑이 자연스럽게 끝났을
    /// 때, 클릭/스킵으로 즉시 완료시켰을 때 전부 이 함수를 거칩니다. 선택지가 있으면 스킵 여부와
    /// 무관하게 스킵을 끄고 선택지를 보여줍니다(대신 골라줄 수는 없으므로). 선택지가 없으면 "다음"
    /// 표시를 보여주고, 스킵 중이면 skipStepDelay 뒤 자동으로 다음 줄로 넘어가도록 예약합니다.</summary>
    private void OnLineFullyShown()
    {
        isTyping = false;
        typingRoutine = null;

        TalkScript.Talks currentTalk = TalkManager.Instance.CurrentTalk;
        bool hasChoices = currentTalk != null && currentTalk.HasChoices;

        if (hasChoices)
        {
            isSkipping = false;
            StopSkipRoutineIfRunning();
            ShowChoices(currentTalk.choices);
            return;
        }

        ShowAdvanceIndicator();

        if (isSkipping)
        {
            StopSkipRoutineIfRunning();
            skipRoutine = StartCoroutine(AutoAdvanceAfterDelay());
        }
    }

    /// <summary>skipStepDelay만큼 기다린 뒤 TalkManager.Advance()를 호출합니다. 대기하는 동안
    /// 스킵이 꺼지면(ClickSkipButton 등으로) 아무 것도 하지 않습니다 - 그 사이에 선택지를 만나
    /// OnLineFullyShown()이 이미 스킵을 꺼버린 경우도 여기 포함됩니다.</summary>
    private IEnumerator AutoAdvanceAfterDelay()
    {
        yield return new WaitForSecondsRealtime(skipStepDelay);
        skipRoutine = null;

        if (isSkipping)
        {
            TalkManager.Instance.Advance();
        }
    }

    private void StopTypingIfRunning()
    {
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }
        isTyping = false;
    }

    private void StopSkipRoutineIfRunning()
    {
        if (skipRoutine != null)
        {
            StopCoroutine(skipRoutine);
            skipRoutine = null;
        }
    }

    // ------------------------------------------------------------------
    // 선택지
    // ------------------------------------------------------------------

    private void ShowChoices(TalkScript.Choice[] choices)
    {
        ClearChoiceButtons();

        for (int i = 0; i < choices.Length; i++)
        {
            UIDialogueChoiceButton button = Instantiate(_choicePrefab, _choiceContainer);
            button.Setup(i, choices[i].choiceText);
            button.OnClicked += HandleChoiceClicked;
            activeChoiceButtons.Add(button);
        }
    }

    private void HandleChoiceClicked(int choiceIndex)
    {
        TalkManager.Instance.SelectChoice(choiceIndex);
    }

    private void ClearChoiceButtons()
    {
        foreach (UIDialogueChoiceButton button in activeChoiceButtons)
        {
            if (button != null) Destroy(button.gameObject);
        }
        activeChoiceButtons.Clear();
    }

    private void ShowAdvanceIndicator()
    {
        if (_advanceIndicator != null) _advanceIndicator.SetActive(true);
    }

    private void HideAdvanceIndicator()
    {
        if (_advanceIndicator != null) _advanceIndicator.SetActive(false);
    }
}