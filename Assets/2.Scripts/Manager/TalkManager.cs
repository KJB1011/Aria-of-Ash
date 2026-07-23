// ============================================================================
// TalkManager.cs
// ----------------------------------------------------------------------------
// TalkScript 하나를 받아서 재생을 총괄하는 대화 매니저입니다. 씬에 하나만 있으면 되고, NPC 상호작용
// 등 다른 스크립트는 TalkManager.Instance.StartTalk(script, anchor)만 호출하면 됩니다. 실제
// 이름/텍스트/선택지 버튼을 그리는 UI는 아직 만들지 않았습니다(다음 단계) - 이 매니저가 발행하는
// OnTalkChanged/OnTalkEnded 이벤트를 그 UI가 구독해서 그리면 됩니다. PlayerInventory가
// OnInventoryChanged를 발행하고 UIInventory가 구독해서 다시 그리는 것과 완전히 같은 방식입니다.
//
// [진행 흐름]
//   StartTalk(script, anchor)를 부르면 talks[0]부터 시작합니다. 선택지가 없는 Talks에서는 Advance()를
//   부르면(보통 "다음" 클릭) 배열상 다음 Talks로 자동 진행하고, 더 이상 다음이 없으면 EndTalk()이
//   자동으로 호출됩니다. 선택지가 있는 Talks(HasChoices)에서는 Advance() 대신 SelectChoice(choiceIndex)를
//   불러야 합니다 - choices[choiceIndex].targetIndex와 같은 index를 가진 Talks로 바로 이동합니다
//   (targetIndex가 -1이거나 존재하지 않는 index면 대화를 종료합니다).
//
// [카메라]
//   dialogueCamera는 Follow/LookAt 없이 이 스크립트가 직접 transform을 옮기는 전용 Cinemachine
//   카메라여야 합니다(Body/Aim을 "Do Nothing"으로 두세요). 대화가 시작되면 Priority를
//   talkingCameraPriority로 올려서(게임플레이 카메라보다 높게) Cinemachine이 자동으로 그 카메라로
//   블렌드하고, 끝나면 idleCameraPriority로 다시 낮춰서 게임플레이 카메라로 자연스럽게 돌아갑니다.
//   대화 시작/종료 순간에만 이 Cinemachine 블렌드가 자연스럽게 일어나고, 대화 도중 각 Talks로
//   넘어갈 때는 블렌드 없이 그 자리로 즉시 컷합니다(원신의 컷 전환 느낌) - 나중에 줄 사이도 부드럽게
//   팬 하고 싶어지면 ApplyCamera()를 코루틴 보간으로 바꾸면 됩니다.
//   [주의] idleCameraPriority는 게임플레이 카메라의 Priority보다 '반드시 더 낮아야' 합니다 - 같은
//   값이면 Cinemachine이 동률로 보고 전환하지 않아서, 대화가 끝나도 카메라가 게임플레이 카메라로
//   돌아오지 않는 문제가 생깁니다(idleCameraPriority 필드의 툴팁 참고).
//
// [게임 진행 정지 - Time.timeScale은 멈추지 않습니다]
//   다른 팝업(인벤토리 등)들과 달리 대화 중에는 Time.timeScale을 건드리지 않습니다 - 대화 도중
//   재생되는 애니메이션(캐릭터/NPC 연출, onTalkStart로 트는 이벤트 등)이 정상 속도로 계속 흘러가야
//   하기 때문입니다. 대신 마우스 커서만 풀어서(Cursor.lockState = None) 선택지를 클릭할 수 있게
//   합니다. 플레이어가 대화 중 마음대로 움직이거나 공격하지 못하게 막는 건 시간을 멈추는 방식이
//   아니라 입력 자체를 걸러내는 방식입니다 - UICanvas.IsUIOpen이 이 매니저의 IsTalking을 함께
//   확인하도록 연동해뒀고(UICanvas.cs 참고), PlayerController.Update()가 그 IsUIOpen을 보고
//   이동/공격/스킬 등 조작 전체를 대화 중엔 아예 읽지 않습니다(IsAnyUIOpen() 가드). 몬스터 등
//   나머지 씬은 대화와 무관하게 평소처럼 계속 움직입니다 - 대화 중에도 배경에서 일어나는 일을
//   보여주고 싶다면(원신처럼) 이 방식이 맞고, 반대로 대화 중 완전히 멈춰있길 원한다면 다시
//   Time.timeScale = 0f를 넣어야 합니다. 다만 Escape로 UI를 닫는 UICanvas.HandleEscapePressed()에는
//   일부러 연동하지 않았습니다 - 중요한 스토리 대화가 실수로 Escape 한 번에 통째로 스킵되는 걸 막기
//   위해서입니다(스킵 기능이 필요해지면 나중에 별도 버튼/조건으로 추가하는 걸 추천합니다).
//
// [씬 준비]
//   1) 빈 오브젝트에 이 스크립트를 붙이세요. 씬에 정확히 하나만 있어야 합니다.
//   2) GameObject > Cinemachine > Camera로 대화 전용 카메라를 하나 만들고, Body/Aim을 전부
//      "Do Nothing"으로 두세요(이 스크립트가 transform을 직접 옮깁니다). 그 카메라를
//      Dialogue Camera 필드에 연결하세요.
//   3) (참고) 이제 대화 중에도 Time.timeScale은 1을 유지하므로, Cinemachine Brain의 Update Method는
//      기본값(Normal Game Time) 그대로 둬도 카메라 블렌드가 끊기지 않습니다.
//   4) 실제 대화창 UI는 별도로 만들어서, 이 스크립트의 OnTalkChanged/OnTalkEnded 이벤트를 구독해
//      텍스트/선택지를 그리면 됩니다(다음 단계에서 만들 예정).
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

public class TalkManager : MonoBehaviour
{
    /// <summary>씬에 하나만 있는 컴포넌트라, 다른 스크립트에서 여기로 바로 접근합니다.</summary>
    public static TalkManager Instance { get; private set; }

    [Header("대화 전용 카메라")]
    [Tooltip("Follow/LookAt 없이 이 스크립트가 직접 위치/회전을 옮기는 전용 Cinemachine 카메라입니다. " +
              "Body/Aim을 Do Nothing으로 두세요.")]
    public CinemachineCamera dialogueCamera;
    [Tooltip("대화 중 dialogueCamera에게 부여할 Priority입니다. 게임플레이 카메라보다 높아야 " +
              "Cinemachine이 이 카메라로 블렌드합니다.")]
    public int talkingCameraPriority = 20;
    [Tooltip("대화가 아닐 때 dialogueCamera의 Priority입니다. 반드시 게임플레이 카메라(예: " +
              "CinemachineOrbitalFollow가 붙은 평소 카메라)의 Priority보다 '확실히 낮아야' 합니다 - " +
              "같은 값(동률)이면 안 됩니다. Cinemachine은 우선순위가 더 높은 카메라가 나타났을 때만 " +
              "전환하고, 값이 같으면(동률) 지금 이미 Live인 카메라(=방금까지 켜져 있던 dialogueCamera)를 " +
              "그대로 유지해버려서 대화가 끝나도 카메라가 게임플레이 카메라로 안 돌아오는 증상이 " +
              "생깁니다. 새로 만든 Cinemachine 카메라의 기본 Priority는 보통 0이므로, 게임플레이 " +
              "카메라를 직접 만들 때 Priority를 건드리지 않았다면 0일 가능성이 높습니다 - 이 값의 " +
              "기본값(-10)이 게임플레이 카메라의 0보다 낮도록 미리 맞춰뒀지만, 게임플레이 카메라의 " +
              "Priority를 나중에 음수로 바꾸거나 이 값을 직접 수정했다면 둘이 같아지지 않도록 다시 " +
              "확인하세요.")]
    public int idleCameraPriority = -10;

    /// <summary>지금 대화가 진행 중인지 여부입니다. UICanvas.IsUIOpen이 이 값을 함께 확인해서,
    /// 대화 중엔 공격/스킬 등 게임플레이 입력을 막습니다.</summary>
    public bool IsTalking { get; private set; }

    /// <summary>지금 재생 중인 Talks입니다. 대화 중이 아니면 null입니다.</summary>
    public TalkScript.Talks CurrentTalk { get; private set; }

    /// <summary>지금 대화 상대(NPC)의 기준점입니다 - StartTalk(script, anchor)로 넘겨받은 그 anchor를
    /// 그대로 노출합니다. PlayerController가 대화 중에 이 위치를 바라보도록 회전시키는 데 씁니다.
    /// 대화 중이 아니면 null입니다.</summary>
    public Transform CurrentAnchor => currentAnchor;

    /// <summary>새로운 Talks가 재생되기 시작할 때마다 발생합니다 - 대화 UI가 이걸 구독해서
    /// 이름/텍스트/선택지를 새로 그리면 됩니다.</summary>
    public event Action<TalkScript.Talks> OnTalkChanged;

    /// <summary>대화가 완전히 끝났을 때 발생합니다 - 대화 UI가 이걸 구독해서 창을 닫으면 됩니다.</summary>
    public event Action OnTalkEnded;

    [Header("디버그")]
    [Tooltip("켜두면 대화 중 카메라를 옮길 때마다(ApplyCamera) anchor의 실제 위치/회전과 그걸로 계산한 " +
              "카메라 위치/회전을 콘솔에 출력합니다. \"처음 대화만 카메라 위치가 이상하다\" 같은 문제를 " +
              "추적할 때 켜서, 대화를 여러 번 시작해보며 로그값이 매번 똑같은지 비교해보세요.")]
    public bool debugLogCamera = false;

    private TalkScript currentScript;
    private Transform currentAnchor;
    private readonly Dictionary<int, int> indexToPosition = new Dictionary<int, int>();
    private int currentPosition;

    private CursorLockMode previousCursorLockState;
    private bool previousCursorVisible;

    private void Awake()
    {
        Instance = this;

        if (dialogueCamera != null)
        {
            dialogueCamera.Priority = idleCameraPriority;
        }
    }

    /// <summary>script를 anchor 위치 기준으로 재생을 시작합니다. 이미 다른 대화가 진행 중이면
    /// 그것부터 즉시 끝낸(EndTalk) 뒤 새로 시작합니다. anchor는 카메라 상대 좌표를 계산할
    /// 기준점입니다(보통 말을 거는 NPC의 Transform). startTalkIndex를 생략하거나 음수를 넘기면
    /// 기존과 동일하게 배열의 맨 처음(talks[0])부터 시작합니다. 0 이상의 값을 넘기면 그 값과 같은
    /// Talks.index를 가진 Talks부터 시작합니다(퀘스트 진행 상태에 따라 다른 대사로 시작하고 싶을 때
    /// 사용 - NPCTalker.ResolveStartTalkIndex() 참고). 그런 index가 없으면 경고를 남기고 talks[0]부터
    /// 시작합니다.</summary>
    public void StartTalk(TalkScript script, Transform anchor, int startTalkIndex = -1)
    {
        if (script == null || script.talks == null || script.talks.Length == 0)
        {
            Debug.LogWarning("[TalkManager] talks가 비어있는 TalkScript는 재생할 수 없습니다.", script);
            return;
        }

        if (IsTalking)
        {
            EndTalk();
        }

        currentScript = script;
        currentAnchor = anchor;
        BuildIndexLookup(script);

        IsTalking = true;

        previousCursorLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Time.timeScale은 건드리지 않습니다 - 대화 도중 애니메이션(캐릭터/NPC 연출, onTalkStart로
        // 트는 이벤트 등)이 정상 속도로 계속 재생되어야 하기 때문입니다. 플레이어가 대화 중 딴 짓을
        // 못 하게 막는 건 시간을 멈추는 게 아니라, PlayerController.Update()가 이미
        // UICanvas.Instance.IsUIOpen(= 이 매니저의 IsTalking 포함)을 보고 이동/공격/스킬 입력 전체를
        // 그 자체에서 걸러내고 있어서(IsAnyUIOpen() 가드) 별도로 시간을 멈출 필요가 없습니다.

        // [순서 중요] 카메라를 목표 위치로 먼저 옮겨두고, 그 다음에 Priority를 올립니다(반대
        // 순서가 아닙니다). Priority를 먼저 올려버리면 Cinemachine이 "이 카메라로 전환해야 한다"는
        // 걸 그 프레임에 즉시 감지하는데, 이 시점엔 아직 GoToPosition(0)이 실행되지 않아서
        // dialogueCamera의 transform이 이전 위치(에디터에 배치해둔 기본 위치, 또는 지난 대화의
        // 마지막 위치)에 그대로 남아있는 상태입니다. 대화 중 각 줄로 넘어갈 때는 이미 Live 상태라
        // 문제가 없지만, "대화가 하나도 진행된 적 없는 첫 실행"에는 Cinemachine이 전환 시작 시점의
        // 카메라 상태를 이 어긋난 위치로 캐싱해버려서 talks[0]에 설정한 위치로 제대로 이동하지
        // 않는 것처럼 보이는 문제가 있었습니다(대화를 한 번이라도 성공적으로 마치고 나면, 그 다음
        // 부터는 dialogueCamera가 이미 한 번 Live였던 적이 있어서 더 이상 이 문제가 나타나지
        // 않습니다). GoToPosition(...)을 먼저 호출해 transform을 확정한 뒤에 Priority를 올리면
        // Cinemachine이 전환을 감지하는 시점에 이미 올바른 위치가 반영되어 있어 매번(첫 대화
        // 포함) 정확한 위치로 전환됩니다.
        int startPosition = 0;
        if (startTalkIndex >= 0)
        {
            if (!indexToPosition.TryGetValue(startTalkIndex, out startPosition))
            {
                Debug.LogWarning($"[TalkManager] index {startTalkIndex}를 가진 Talks를 찾을 수 없어 " +
                                  "0번(배열 맨 처음)부터 시작합니다.", script);
                startPosition = 0;
            }
        }
        GoToPosition(startPosition);

        if (dialogueCamera != null)
        {
            dialogueCamera.Priority = talkingCameraPriority;
        }
    }

    /// <summary>선택지가 없는 Talks에서 "다음" 입력(클릭 등)이 들어왔을 때 호출하세요. endsConversation이
    /// 켜져 있거나 더 이상 다음 Talks가 없으면 대화를 바로 종료하고, 그렇지 않으면 배열상 다음
    /// Talks로 진행합니다. 선택지가 있는 Talks에서는 이 함수가 아무 것도 하지 않습니다 - 대신
    /// SelectChoice()를 호출하세요.</summary>
    public void Advance()
    {
        if (!IsTalking || CurrentTalk == null) return;
        if (CurrentTalk.HasChoices) return; // 선택지가 있으면 SelectChoice()로만 진행합니다.

        // 여러 퀘스트 상태별 대화 묶음을 하나의 talks[] 배열에 이어붙여둔 경우, 이 묶음이 배열 끝이
        // 아닌 중간에서 끝나야 할 수 있습니다(TalkScript.cs의 endsConversation 설명 참고) - 이 값이
        // 켜져 있으면 선택지 없이도 여기서 바로 대화를 종료합니다.
        if (CurrentTalk.endsConversation)
        {
            EndTalk();
            return;
        }

        int nextPosition = currentPosition + 1;
        if (nextPosition >= currentScript.talks.Length)
        {
            EndTalk();
            return;
        }

        GoToPosition(nextPosition);
    }

    /// <summary>선택지가 있는 Talks에서 choiceIndex번째 선택지를 골랐을 때 호출하세요. 그 선택지의
    /// targetIndex와 같은 index를 가진 Talks로 이동합니다(targetIndex가 -1이거나 존재하지 않는
    /// index면 안전하게 대화를 종료합니다).</summary>
    public void SelectChoice(int choiceIndex)
    {
        if (!IsTalking || CurrentTalk == null || !CurrentTalk.HasChoices) return;
        if (choiceIndex < 0 || choiceIndex >= CurrentTalk.choices.Length) return;

        TalkScript.Choice choice = CurrentTalk.choices[choiceIndex];

        // 선택지에 연결된 퀘스트가 있으면(questToGrant), 대화가 계속되든 여기서 끝나든 상관없이
        // 먼저 지급합니다. QuestData는 애셋-애셋 참조라 TalkScript(애셋)에 안전하게 저장할 수 있고,
        // 실제 지급 로직은 여기(코드, 애셋이 아님)에서 QuestManager.Instance.AddQuest()를 호출하는
        // 방식입니다 - onTalkStart UnityEvent가 씬 오브젝트를 직접 참조할 수 없는 것과 같은 문제를
        // 피해갑니다(TalkScript.cs 헤더 주석 참고).
        QuestManager.Instance?.AddQuest(choice.questToGrant);

        // 마찬가지로 선택지에 "완료 보고" 대상 퀘스트가 연결되어 있으면(questToTurnIn) 여기서 보고
        // 처리합니다 - requiresTurnIn 퀘스트는 목표를 다 채워도 자동으로 완료되지 않고 이 호출이
        // 있어야 비로소 완료/보상 지급이 이뤄집니다(QuestManager.TurnInQuest 참고). 아직 목표를 못
        // 채웠거나 진행 중인 퀘스트가 아니면 QuestManager가 알아서 경고만 남기고 무시합니다.
        QuestManager.Instance?.TurnInQuest(choice.questToTurnIn);

        int targetIndex = choice.targetIndex;
        if (targetIndex < 0)
        {
            EndTalk();
            return;
        }

        if (!indexToPosition.TryGetValue(targetIndex, out int targetPosition))
        {
            Debug.LogWarning($"[TalkManager] index {targetIndex}를 가진 Talks를 찾을 수 없어 대화를 종료합니다.", currentScript);
            EndTalk();
            return;
        }

        GoToPosition(targetPosition);
    }

    /// <summary>대화를 즉시 종료합니다. 카메라/커서/타임스케일을 대화 시작 전 상태로 되돌립니다.
    /// 대화 중이 아니면 아무 것도 하지 않습니다.</summary>
    public void EndTalk()
    {
        if (!IsTalking) return;
        IsTalking = false;

        Cursor.lockState = previousCursorLockState;
        Cursor.visible = previousCursorVisible;

        if (dialogueCamera != null)
        {
            dialogueCamera.Priority = idleCameraPriority;
        }

        CurrentTalk = null;
        currentScript = null;
        currentAnchor = null;

        OnTalkEnded?.Invoke();
    }

    private void GoToPosition(int position)
    {
        currentPosition = position;
        CurrentTalk = currentScript.talks[position];

        ApplyCamera(CurrentTalk);

        CurrentTalk.onTalkStart?.Invoke();
        OnTalkChanged?.Invoke(CurrentTalk);
    }

    /// <summary>anchor 기준 상대 좌표(cameraLocalPosition/cameraLocalEulerAngles)를 월드 좌표로
    /// 변환해서 dialogueCamera에 블렌드 없이 즉시 적용합니다.</summary>
    private void ApplyCamera(TalkScript.Talks talk)
    {
        if (dialogueCamera == null) return;

        Transform origin = currentAnchor != null ? currentAnchor : transform;
        Vector3 worldPosition = origin.TransformPoint(talk.cameraLocalPosition);
        Quaternion worldRotation = origin.rotation * Quaternion.Euler(talk.cameraLocalEulerAngles);

        if (debugLogCamera)
        {
            // "첫 대화만 카메라 위치가 이상하다" 같은 문제를 추적할 때 켜보세요. anchor 자체의
            // 위치/회전이 첫 대화와 이후 대화에서 다르게 찍힌다면 anchor(보통 NPC의 자식 오브젝트)
            // 쪽 문제(예: 애니메이션 리그의 뼈에 붙어 있어서 첫 프레임엔 아직 T포즈 위치인 경우)이고,
            // anchor 값은 항상 똑같은데 실제 화면 속 카메라만 다르게 보인다면 Cinemachine
            // Brain(Default Blend 등) 쪽 문제일 가능성이 큽니다.
            Debug.Log($"[TalkManager] ApplyCamera - anchor='{origin.name}' anchorPos={origin.position} " +
                      $"anchorRot={origin.rotation.eulerAngles} → worldPos={worldPosition} worldRot={worldRotation.eulerAngles} " +
                      $"(Priority={dialogueCamera.Priority})", dialogueCamera);
        }

        dialogueCamera.transform.SetPositionAndRotation(worldPosition, worldRotation);
    }

    /// <summary>script.talks를 훑어서 index → 배열 위치 매핑을 새로 만듭니다. index가 중복되면
    /// 먼저 등록된 쪽을 유지하고 경고를 남깁니다.</summary>
    private void BuildIndexLookup(TalkScript script)
    {
        indexToPosition.Clear();

        for (int i = 0; i < script.talks.Length; i++)
        {
            int index = script.talks[i].index;
            if (indexToPosition.ContainsKey(index))
            {
                Debug.LogWarning($"[TalkManager] '{script.name}'에 index {index}가 중복됩니다 - 선택지가 " +
                                  "이 값을 참조하면 둘 중 먼저 등록된 Talks로만 이동합니다. 인스펙터에서 " +
                                  "index를 서로 겹치지 않게 맞춰주세요.", script);
                continue;
            }
            indexToPosition.Add(index, i);
        }
    }
}