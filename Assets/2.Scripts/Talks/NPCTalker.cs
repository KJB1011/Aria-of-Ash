// ============================================================================
// NPCTalker.cs
// ----------------------------------------------------------------------------
// NPC에게 이 스크립트를 붙이면, 플레이어가 다가가서(InteractionDetector 범위 안) 상호작용
// 키(F)를 누르는 순간 TalkManager로 지정된 TalkScript 대화를 시작합니다. LootPickup과 완전히
// 같은 IInteractable 패턴을 씁니다 - InteractionDetector가 똑같은 방식으로 감지/선택/실행합니다.
//
// [LootPickup과 다른 점 - Collider를 Trigger로 바꾸지 않습니다]
//   LootPickup은 플레이어가 물리적으로 통과해야 해서 Awake()에서 Collider.isTrigger를 강제로
//   true로 바꾸지만, NPC는 보통 플레이어가 부딪혀서 못 지나가야(물리적으로 막혀야) 하므로 이
//   스크립트는 Collider의 isTrigger 값을 건드리지 않습니다 - NPC가 원래 갖고 있던(Trigger가
//   아닌) Collider를 그대로 상호작용 감지에도 함께 씁니다(Physics.OverlapSphereNonAlloc은 Trigger
//   여부와 무관하게 감지합니다). 이 오브젝트(또는 그 Collider)의 레이어가 InteractionDetector의
//   Interactable Mask에 포함되어 있어야 감지됩니다.
//
// [회전시킬 대상이 두 개로 나뉘어 있습니다 - Camera Anchor(스냅) / Model Transform(부드럽게)]
//   대화가 시작되면 이 NPC가 "플레이어를 바라보는" 회전이 필요한 곳이 사실 두 군데입니다.
//     1) Camera Anchor(TalkManager.StartTalk()에 넘기는 anchor, 보통 "CameraPos" 같은 이름의
//        빈 자식 오브젝트) - TalkScript의 카메라 좌표가 이 지점 기준 상대 좌표로 계산되므로,
//        대화가 시작되는 바로 그 프레임에 이미 "플레이어를 바라보는 방향"으로 맞춰져 있어야
//        카메라 앵글이 어색하지 않습니다. 그래서 이건 부드럽게 돌리지 않고 Interact() 시점에
//        즉시(스냅) 회전시킵니다 - 애초에 렌더링되지 않는 빈 오브젝트라 스냅이어도 눈에 띄지
//        않습니다. 대화가 끝나면 마찬가지로 스냅으로 원래 회전으로 되돌립니다.
//     2) Model Transform(실제 3D 모델링/리그) - 이건 화면에 보이는 캐릭터 자체이므로, 스냅이 아니라
//        Talk Rotation Speed(도/초)로 자연스럽게 돌아가고, 대화가 끝나면 같은 속도로 부드럽게
//        원래 방향으로 돌아갑니다.
//   Model Transform을 비워두면 이 오브젝트 자신(루트)을 부드럽게 회전시킵니다. Camera Anchor를
//   비워두면 이 오브젝트 자신(루트)을 스냅으로 회전시킵니다 - 단, 이 둘을 전부 비워두면 루트 하나를
//   스냅과 부드러운 회전 두 가지 방식으로 동시에 조작하려 해서 충돌합니다. 반드시 Camera Anchor는
//   Model Transform과 서로 다른(겹치지 않는) 오브젝트로 연결하세요.
//
// [대화 중 플레이어 바라보기 / 대화 종료 후 원위치 - 자세한 흐름]
//   Interact()가 호출되는 순간, Camera Anchor를 플레이어 쪽으로 즉시 스냅 회전시킨 뒤에
//   TalkManager.StartTalk()를 부릅니다(TalkManager가 그 즉시 anchor의 회전값을 읽어 카메라 각도를
//   계산하기 때문에 순서가 중요합니다). 이후 대화가 진행되는 동안(TalkManager.IsTalking) Model
//   Transform은 매 프레임 플레이어 쪽을 향해 부드럽게 회전/이동합니다. 대화가 끝나면(전역
//   TalkManager.IsTalking이 false가 되는 순간) Camera Anchor는 즉시 원래 회전으로, Model
//   Transform은 부드럽게 원래 위치/회전으로 돌아갑니다. 대화 중 F 입력 자체가 막혀있어(TalkManager가
//   커서를 풀기 때문에 InteractionDetector가 동작하지 않음) 이 NPC가 대화하는 도중 다른 NPC의
//   Interact()가 불려서 상태가 꼬이는 일은 없습니다.
//
// [씬 준비]
//   1) NPC 오브젝트(Collider가 이미 있는 것 - 보통 CapsuleCollider 등 물리 충돌용)에 이
//      스크립트를 붙이세요.
//   2) Npc Name에 상호작용 목록에 표시할 이름을, Talk Script에 재생할 TalkScript 애셋을
//      연결하세요.
//   3) 이 오브젝트(또는 Collider)의 레이어를 InteractionDetector.interactableMask에 포함된
//      레이어로 지정하세요(예: "Interactable").
//   4) Camera Anchor에 카메라 기준점으로 쓸 빈 자식 오브젝트(예: "CameraPos")를 연결하세요.
//   5) Model Transform에 실제 모델링 자식 오브젝트를 연결하세요.
//   6) (선택) Talk Rotation Speed/Position Return Speed로 Model Transform이 플레이어를
//      바라보는/대화 후 원위치로 돌아가는 속도를 조절하세요(Camera Anchor는 항상 스냅이라 이
//      속도의 영향을 받지 않습니다).
//   7) (선택) 말하는 모션을 재생하고 싶다면, Animator Controller에 "Talk"라는 이름의 Trigger
//      파라미터를 추가하고 그 트리거로 전환되는 State(말하는 제스처 모션)를 만들어두세요. 그 뒤
//      TalkScript에서 원하는 Talks(보통 talks[0], 또는 매 줄마다)의 Play Talk Animation On Start
//      체크박스를 켜두세요 - TalkScript.Talks의 On Talk Start(UnityEvent)는 애셋이라 이 NPC
//      오브젝트를 직접 연결할 수 없어서(TalkScript.cs 상단 주석 참고) 대신 이 체크박스 방식을
//      씁니다. 이 스크립트가 대화 시작 시 TalkManager.OnTalkChanged를 구독해서, 그 체크박스가
//      켜진 Talks가 나올 때마다 자동으로 PlayTalkAnimation()을 호출합니다.
// ============================================================================

using UnityEngine;

[RequireComponent(typeof(Collider))]
public class NPCTalker : MonoBehaviour, IInteractable
{
    [Header("상호작용 표시")]
    [Tooltip("상호작용 목록 UI에 표시할 이름입니다. 예: \"마을 주민\"")]
    public string npcName = "NPC";

    [Header("대화")]
    [Tooltip("상호작용하면 재생할 TalkScript입니다.")]
    public TalkScript talkScript;

    [Header("카메라 기준점 (즉시 스냅 회전)")]
    [Tooltip("TalkManager.StartTalk()에 넘길 anchor입니다. 비워두면 이 오브젝트 자신의 Transform을 " +
              "씁니다 - TalkScript의 카메라 좌표가 이 지점 기준 상대 좌표로 계산됩니다. 대화 시작/종료 " +
              "시점에 이 오브젝트만 즉시(스냅) 회전합니다 - 렌더링되지 않는 빈 오브젝트라 스냅이어도 " +
              "눈에 띄지 않습니다.")]
    public Transform cameraAnchor;

    [Header("모델 회전 (부드럽게)")]
    [Tooltip("대화 중 플레이어를 바라보도록(그리고 대화가 끝나면 원래 방향으로) 부드럽게 회전/이동시킬 " +
              "실제 3D 모델링 자식 오브젝트입니다. 비워두면 이 스크립트가 붙은 오브젝트 자신(루트)을 " +
              "회전시킵니다. Camera Anchor와 반드시 서로 다른 오브젝트여야 합니다.")]
    public Transform modelTransform;
    [Tooltip("대화 중엔 이 속도(도/초)로 Model Transform이 플레이어 쪽을 부드럽게 바라보고, 대화가 " +
              "끝나면 같은 속도로 원래 바라보던 방향으로 돌아갑니다.")]
    public float talkRotationSpeed = 360f;
    [Tooltip("대화가 끝난 뒤 Model Transform이 원래 위치로 돌아가는 속도(초당 미터)입니다. 지금은 " +
              "NPC가 대화 중 위치가 바뀌는 기능이 없어서 사실상 안전장치용입니다.")]
    public float positionReturnSpeed = 5f;

    [Header("대화 애니메이션")]
    [Tooltip("PlayTalkAnimation()이 트리거를 발동시킬 Animator입니다. 비워두면 Awake()에서 이 " +
              "오브젝트 → Model Transform → 자식 순서로 자동으로 찾습니다. Animator Controller에는 " +
              "\"Talk\"라는 이름의 Trigger 파라미터가 있어야 합니다.")]
    public Animator animator;

    private static readonly int TalkParam = Animator.StringToHash("Talk");

    private Transform anchorTransform;   // cameraAnchor, 없으면 루트
    private Transform modelTarget;       // modelTransform, 없으면 루트

    private Quaternion originalAnchorRotation;
    private Vector3 originalModelPosition;
    private Quaternion originalModelRotation;

    private Transform playerTransform;
    private bool isFacingPlayer;
    private bool subscribedToTalk;

    private void Awake()
    {
        anchorTransform = cameraAnchor != null ? cameraAnchor : transform;
        modelTarget = modelTransform != null ? modelTransform : transform;

        originalAnchorRotation = anchorTransform.rotation;
        originalModelPosition = modelTarget.position;
        originalModelRotation = modelTarget.rotation;

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        if (animator == null && modelTransform != null)
        {
            // modelTransform 자신뿐 아니라 그 아래 자식(임포트된 모델 프리팹이 한 단계 더 감싸져
            // 있는 경우 등)까지 전부 찾아봅니다.
            animator = modelTransform.GetComponentInChildren<Animator>(true);
        }
        if (animator == null)
        {
            // 루트(이 오브젝트) 기준으로 그 아래 전체(모델 오브젝트가 자식으로 있다면 여기서 잡힙니다).
            animator = GetComponentInChildren<Animator>(true);
        }
        if (animator == null)
        {
            // 혹시 이 스크립트가 루트가 아니라 중간 오브젝트에 붙어있고, Animator는 오히려 그 위
            // 조상 쪽에 있는 특이한 구조라면 마지막으로 여기도 확인합니다.
            animator = GetComponentInParent<Animator>();
        }

        if (animator == null)
        {
            Debug.LogWarning($"[NPCTalker] '{name}': Animator를 찾지 못했습니다 - 이 오브젝트 자신, " +
                              "Model Transform(및 그 자식), 그 하위 전체, 부모 쪽까지 전부 찾아봤지만 " +
                              "없었습니다. 모델링 오브젝트(또는 그 자식)에 Animator 컴포넌트가 실제로 " +
                              "추가되어 있는지, 그리고 그 오브젝트가 이 NPCTalker가 붙은 오브젝트의 " +
                              "자식(하위)으로 제대로 들어있는지 확인해주세요 - 형제(sibling) 오브젝트로 " +
                              "따로 떨어져 있으면 이 탐색으로는 찾을 수 없습니다.", this);
        }
    }

    /// <summary>연결된 Animator의 "Talk" 트리거를 발동시켜 말하는 모션(제스처 등)을 재생합니다.
    /// 직접 호출할 수도 있지만, 보통은 TalkScript의 Talks.playTalkAnimationOnStart 체크박스를
    /// 켜두면 HandleTalkChanged()가 알아서 호출해줍니다(TalkScript.cs 상단 주석 참고 - onTalkStart
    /// UnityEvent로는 이 씬 오브젝트를 직접 연결할 수 없어서 이 방식을 씁니다). Animator를 못
    /// 찾았으면 경고만 남기고 아무 것도 하지 않습니다.</summary>
    public void PlayTalkAnimation()
    {
        if (animator == null)
        {
            Debug.LogWarning($"[NPCTalker] '{name}'에 연결된(또는 자동으로 찾은) Animator가 없어 " +
                              "대화 애니메이션을 재생할 수 없습니다.", this);
            return;
        }

        animator.SetTrigger(TalkParam);
    }

    /// <summary>대화 중(내가 시작한 대화일 때만) TalkManager.OnTalkChanged를 구독해서 호출됩니다.
    /// 그 Talks의 playTalkAnimationOnStart가 켜져 있으면 PlayTalkAnimation()을 호출합니다.</summary>
    private void HandleTalkChanged(TalkScript.Talks talk)
    {
        if (talk != null && talk.playTalkAnimationOnStart)
        {
            PlayTalkAnimation();
        }
    }

    /// <summary>내가 시작한 대화가 끝나면(TalkManager.OnTalkEnded) 구독을 정리합니다 - 다른 NPC와의
    /// 대화에서까지 이 NPC의 애니메이션이 반응하지 않도록, 대화가 끝나는 즉시 반드시 구독을
    /// 해지합니다.</summary>
    private void HandleTalkEnded()
    {
        UnsubscribeFromTalk();
    }

    private void SubscribeToTalk()
    {
        if (subscribedToTalk || TalkManager.Instance == null) return;
        TalkManager.Instance.OnTalkChanged += HandleTalkChanged;
        TalkManager.Instance.OnTalkEnded += HandleTalkEnded;
        subscribedToTalk = true;
    }

    private void UnsubscribeFromTalk()
    {
        if (!subscribedToTalk) return;
        if (TalkManager.Instance != null)
        {
            TalkManager.Instance.OnTalkChanged -= HandleTalkChanged;
            TalkManager.Instance.OnTalkEnded -= HandleTalkEnded;
        }
        subscribedToTalk = false;
    }

    private void OnDisable()
    {
        // 대화 도중 이 오브젝트가 비활성화/파괴되는 예외적인 경우에도 구독이 남아있지 않도록
        // 안전장치로 한 번 더 해지합니다.
        UnsubscribeFromTalk();
    }

    private void Update()
    {
        // 전역 대화 상태가 꺼지면(대화가 끝나면) 이 NPC도 "바라보기"를 그만두고 원위치로 돌아가기
        // 시작합니다. 대화 중엔 입력이 막혀서 다른 NPC와 동시에 대화가 진행될 수 없으므로,
        // isFacingPlayer가 true인 동안의 TalkManager.IsTalking은 항상 "나와의 대화"를 의미합니다.
        if (isFacingPlayer && (TalkManager.Instance == null || !TalkManager.Instance.IsTalking))
        {
            isFacingPlayer = false;
            anchorTransform.rotation = originalAnchorRotation; // 카메라 기준점은 스냅으로 즉시 원위치.
        }

        // Model Transform은 매 프레임 부드럽게 목표(플레이어 바라보기 / 원래 방향)를 향해 보간합니다.
        Quaternion modelTargetRotation = (isFacingPlayer && playerTransform != null)
            ? FaceRotationTowards(modelTarget, playerTransform.position)
            : originalModelRotation;
        Vector3 modelTargetPosition = isFacingPlayer ? modelTarget.position : originalModelPosition;

        modelTarget.rotation = Quaternion.RotateTowards(modelTarget.rotation, modelTargetRotation, talkRotationSpeed * Time.deltaTime);
        modelTarget.position = Vector3.MoveTowards(modelTarget.position, modelTargetPosition, positionReturnSpeed * Time.deltaTime);
    }

    /// <summary>origin 기준으로 targetPosition 쪽(수평 방향만)을 바라보는 회전값을 계산합니다.
    /// 같은 위치라 방향을 구할 수 없으면(거의 없겠지만) origin의 지금 회전을 그대로 돌려줍니다.</summary>
    private static Quaternion FaceRotationTowards(Transform origin, Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - origin.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return origin.rotation;
        return Quaternion.LookRotation(direction);
    }

    // ------------------------------------------------------------------
    // IInteractable 구현
    // ------------------------------------------------------------------

    public string InteractionName => npcName;

    public Vector3 InteractionPosition => transform.position;

    /// <summary>상호작용 키(F)를 눌러 이 NPC가 선택되어 있을 때 InteractionDetector가 호출합니다.
    /// talkScript가 비어있거나 씬에 TalkManager가 없으면 경고만 남기고 아무 것도 하지 않습니다.
    /// 대화 중엔 TalkManager가 커서를 풀어서(Cursor.lockState = None) InteractionDetector의 F키
    /// 입력 자체가 막히므로, 대화 도중 다른 NPC와 다시 상호작용하는 일은 정상적인 흐름에서는
    /// 일어나지 않습니다.</summary>
    public void Interact(GameObject interactor)
    {
        if (talkScript == null)
        {
            Debug.LogWarning($"[NPCTalker] '{name}'에 Talk Script가 연결되어 있지 않습니다.", this);
            return;
        }

        if (TalkManager.Instance == null)
        {
            Debug.LogWarning("[NPCTalker] 씬에 TalkManager가 없어서 대화를 시작할 수 없습니다.", this);
            return;
        }

        playerTransform = interactor != null ? interactor.transform : null;
        isFacingPlayer = true;

        // TalkManager.StartTalk() → GoToPosition(0) → ApplyCamera()가 바로 이어서(같은 프레임,
        // 같은 호출 안에서) anchorTransform의 "현재" 회전값을 읽어 카메라 각도를 계산합니다. 그래서
        // StartTalk()를 부르기 전에 여기서 미리 Camera Anchor만 플레이어 쪽으로 즉시(스냅)
        // 회전시켜, 카메라가 계산에 쓰는 회전이 처음부터 최종 값이 되도록 합니다. Model Transform은
        // 여기서 건드리지 않고 Update()의 부드러운 보간에만 맡겨둡니다.
        if (playerTransform != null)
        {
            anchorTransform.rotation = FaceRotationTowards(anchorTransform, playerTransform.position);
        }

        // talks[0]의 OnTalkChanged도 이 구독으로 받아야 하므로, StartTalk()보다 먼저 구독합니다.
        SubscribeToTalk();

        TalkManager.Instance.StartTalk(talkScript, anchorTransform);
    }
}