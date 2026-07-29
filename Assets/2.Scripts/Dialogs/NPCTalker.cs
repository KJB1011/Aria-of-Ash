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
// [컷씬에서 대화 시작 "전"에 미리 마주보기]
//   CutsceneManager의 FacePlayerAndNpc 스텝처럼, 실제 대화(Interact())가 시작되기 전에 미리 NPC가
//   플레이어 쪽을 바라보게 하고 싶으면 CutsceneSetFaceTarget()을 호출하세요(예: 대화 직전 눈 마주침
//   클로즈업 연출). isFacingPlayer(대화 중 바라보기)와는 별개의 상태(cutsceneForcingFace)로 관리되어,
//   TalkManager.IsTalking이 아직 false인 동안에도(대화 시작 전에도) 원하는 방향을 계속 유지합니다 -
//   그대로 두면 아래 [대화 중 ~] 로직이 매 프레임 "대화 중이 아니니 원래 방향으로" 되돌리려 해서
//   충돌하기 때문입니다. 보통 바로 뒤에 StartDialogue로 이어지는 구성으로 쓰고, 실제 대화가 시작되면
//   isFacingPlayer가 자연스럽게 이어받습니다.
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
// [대사 시작 index 결정 - 4단계 우선순위 (ResolveStartTalkIndex() 참고)]
//   relatedQuests(배열)와 questCompletionOverrides를 같이 쓸 때 헷갈리기 쉬워서, 순서를 명확히
//   정리합니다. 매 상호작용마다 아래 4단계를 순서대로 검사해서, 맨 처음으로 조건에 맞는 것 하나만
//   씁니다:
//     1단계) "지금 당장 처리해야 할 용무" 우선 - relatedQuests를 배열 순서대로 훑어서 진행
//            중(Active)이거나 완료 보고를 기다리는 중(ReadyToTurnIn)인 첫 번째 항목을 즉시
//            사용합니다. 이미 완료된 항목은 여기서 건너뜁니다.
//     2단계) 1단계에 아무 것도 안 걸렸으면(진행 중인 게 하나도 없으면), questCompletionOverrides를
//            배열 순서대로 확인해서 완료된 퀘스트를 가진 첫 번째 항목의 talkIndex를 씁니다 - "사전
//            퀘스트(A)가 막 완료되어 다음 퀘스트(B)를 새로 제안해야 하는 순간"이 정확히 여기서
//            잡힙니다.
//     3단계) 2단계에서도 새로 제안할 게 없었으면, relatedQuests를 이번엔 "배열 역순으로"(마지막
//            항목부터 거꾸로) 훑어서 이미 완료된 첫 번째 항목의 마무리 대사(completedTalkIndex)를
//            씁니다. relatedQuests는 보통 진행 순서(가장 먼저 받는 퀘스트가 [0])대로 등록해두므로,
//            역순으로 찾아야 "가장 나중 단계까지 완료된" 항목이 먼저 걸립니다 - 정순으로 찾으면
//            체인을 끝까지 다 깨도 항상 맨 처음 퀘스트의 완료 대사로 돌아가 버립니다.
//     4단계) 그마저도 없으면(전부 아직 안 받은 완전히 처음 상태) relatedQuests[0]의
//            notStartedTalkIndex(맨 처음 기본 대사)를 씁니다 - 그래서 relatedQuests[0]에는 항상
//            체인에서 가장 먼저 받는 퀘스트를 넣어야 합니다.
//   [예시] relatedQuests = [A, B] (진행 순서 그대로 - A가 먼저, B가 나중), questCompletionOverrides =
//   [{quest: A, talkIndex: "B를 제안하는 대사"}] 로 설정한 A→B 체인 NPC라면:
//     - A를 안 받았을 때: 1단계 통과(진행 중인 게 없음), 2단계도 통과(A가 아직 완료 안 됨), 3단계도
//       통과(완료된 것도 없음) → 4단계로 relatedQuests[0]인 A의 notStartedTalkIndex.
//     - A 진행 중: 1단계에서 A가 Active로 걸려 A의 inProgressTalkIndex.
//     - A 완료, B 아직 안 받음: 1단계는 아무 것도 안 걸림(둘 다 진행 중이 아님) → 2단계에서
//       questCompletionOverrides의 A 완료 조건이 걸려 "B를 제안하는 대사".
//     - B 진행 중(A는 당연히 완료 상태): 1단계에서 B가 Active로 걸려 B의 inProgressTalkIndex(A는
//       확인하지도 않음 - 배열 순서상 A가 먼저 검사되지만 A는 이미 완료라 1단계 조건에 안 걸림).
//     - B 완료: 1단계 통과, 2단계 통과(B 완료를 걸어둔 override가 없다면), 3단계에서 relatedQuests를
//       "역순으로"(B → A 순서로) 훑어 B가 먼저 걸려 B의 completedTalkIndex(정순이었다면 A가 먼저
//       걸려 A의 완료 대사로 잘못 되돌아갔을 것입니다).
//   relatedQuests와 무관하게 그냥 "다른 퀘스트를 깨면 특별한 대사"만 보여주고 싶은 NPC(예:
//   relatedQuests를 비워둔 마을 주민)에도 questCompletionOverrides를 그대로 쓸 수 있습니다.
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
//   8) (선택) "이 NPC에게 말 걸기"를 목표로 하는 퀘스트를 만들고 싶다면, Npc Id에 고유한 문자열을
//      정해서 적고(MonsterStats.monsterId와 같은 방식), QuestData.Objective의 Type을 TalkToNpc로,
//      Target Npc Id를 여기 적은 값과 똑같이 맞춰주세요.
// ============================================================================

using UnityEngine;

[RequireComponent(typeof(Collider))]
public class NPCTalker : MonoBehaviour, IInteractable
{
    [System.Serializable]
    public class QuestCompletionOverride
    {
        [Tooltip("이 퀘스트가 완료된 상태인지 확인합니다.")]
        public QuestData quest;
        [Tooltip("완료된 상태라면(그리고 이 배열에서 더 앞에 있는 다른 항목이 먼저 완료 판정을 받지 " +
                  "않았다면) 이 Talks.index로 대화를 시작합니다.")]
        public int talkIndex;
    }

    [System.Serializable]
    public class RelatedQuestEntry
    {
        [Tooltip("이 항목이 추적할 퀘스트입니다.")]
        public QuestData quest;
        [Tooltip("quest를 아직 받지 않았고(그리고 relatedQuests 배열에서 이보다 앞선 다른 항목도 아직 " +
                  "안 받았고) questCompletionOverrides에도 걸리는 게 없을 때 시작할 Talks.index입니다. " +
                  "사전 퀘스트를 완료한 뒤 이 quest를 제안하는 대사를 따로 보여주고 싶다면, 그건 여기가 " +
                  "아니라 questCompletionOverrides에 등록하세요.")]
        public int notStartedTalkIndex;
        [Tooltip("quest를 받아서 진행 중이지만 아직 목표를 다 채우지 못했을 때 시작할 Talks.index입니다.")]
        public int inProgressTalkIndex;
        [Tooltip("quest의 목표를 다 채워 완료 보고를 기다리는 중일 때 시작할 Talks.index입니다(보통 이 " +
                  "Talks의 선택지에 Choice.questToTurnIn으로 quest를 연결해 여기서 보고받으세요).")]
        public int readyToTurnInTalkIndex;
        [Tooltip("quest가 이미 완료됐을 때 시작할 Talks.index입니다.")]
        public int completedTalkIndex;
    }

    [Header("상호작용 표시")]
    [Tooltip("상호작용 목록 UI에 표시할 이름입니다. 예: \"마을 주민\"")]
    public string npcName = "NPC";

    [Header("퀘스트 연동 - 이 NPC와 상호작용 시 QuestManager.ReportTalkToNpc()로 보고할 ID")]
    [Tooltip("QuestData.Objective(TalkToNpc).targetNpcId와 같은 값으로 맞추세요(MonsterStats.monsterId와 " +
              "같은 방식입니다). 이 NPC와 상호작용(F키)할 때마다 이 ID로 QuestManager.ReportTalkToNpc()가 " +
              "호출되어, \"이 NPC에게 말 걸기\" 목표가 있는 퀘스트가 있으면 카운트됩니다. 비워두면(빈 " +
              "문자열) 아무 것도 보고하지 않습니다 - \"이 NPC에게 말 걸기\" 퀘스트 목표로 쓸 계획이 없다면 " +
              "비워둬도 안전합니다.")]
    public string npcId;

    [Header("대화")]
    [Tooltip("상호작용하면 재생할 TalkScript입니다.")]
    public TalkScript talkScript;

    [Header("퀘스트 연동 (선택사항 - 비워두면 항상 talks[0]부터 시작 = 기존과 동일)")]
    [Tooltip("이 NPC가 추적/제공하는 퀘스트들입니다. 배열 순서가 곧 우선순위입니다 - 여러 개 중 이미 " +
              "받은(진행 중/완료 보고 대기/완료) 항목이 여러 개 있으면 배열에서 더 앞쪽에 있는 항목을 " +
              "우선합니다. 퀘스트 체인(A를 깬 뒤 B를 받는 식)이 있다면, 나중 단계 퀘스트(B)를 배열 " +
              "앞쪽에, 이전 단계 퀘스트(A)를 뒤쪽에 두는 걸 권장합니다 - 그래야 B를 이미 받은 뒤에는 A " +
              "관련 대사로 되돌아가지 않습니다. 비워두면 퀘스트 상태를 전혀 확인하지 않고 항상 " +
              "talks[0]부터 시작합니다(기존과 동일하게 동작).")]
    public RelatedQuestEntry[] relatedQuests = new RelatedQuestEntry[0];

    [Header("사전 퀘스트 완료 여부로 대사 분기 (선택사항 - relatedQuests를 아직 하나도 안 받았을 때만 확인)")]
    [Tooltip("relatedQuests 중 어느 것도 아직 받지 않은 상태에서만 확인합니다(하나라도 이미 받았다면 이 " +
              "배열은 무시하고 곧바로 relatedQuests 기반 index로 넘어갑니다). 다른 퀘스트(보통 사전 " +
              "퀘스트)가 완료됐을 때 특정 Talks.index로 대화를 시작하고 싶으면 등록하세요 - 예: 퀘스트 " +
              "A를 깨야 이 NPC가 퀘스트 B를 제안하는 경우, quest에 A를, talkIndex에 B를 제안하는 " +
              "Talks.index를 넣으세요. 배열 순서대로 확인해서 완료된 퀘스트를 가진 첫 번째 항목을 " +
              "사용하며, 아무 것도 완료되지 않았으면 relatedQuests[0].notStartedTalkIndex(맨 처음 기본 " +
              "대사)로 넘어갑니다.")]
    public QuestCompletionOverride[] questCompletionOverrides = new QuestCompletionOverride[0];

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

    // 컷씬이 대화 시작 전에 미리 마주보기를 요청했는지 여부입니다(CutsceneSetFaceTarget() 참고).
    // isFacingPlayer와 별개의 상태입니다 - isFacingPlayer는 TalkManager.IsTalking에 연동되어 대화가
    // 시작되기 전에는(아직 false인 동안에는) Update()가 계속 원래 방향으로 되돌리려 하기 때문에,
    // 컷씬에서 대화 시작 "전"에 미리 마주보게 하려면 그 로직과 별도로 관리해야 합니다.
    private bool cutsceneForcingFace;
    private Vector3 cutsceneFaceTarget;

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

        // Model Transform은 매 프레임 부드럽게 목표(플레이어 바라보기 / 컷씬이 지정한 방향 / 원래 방향)를
        // 향해 보간합니다. 우선순위는 대화 중(isFacingPlayer) > 컷씬이 미리 요청한 방향
        // (cutsceneForcingFace) > 원래 방향 순입니다 - 대화가 실제로 시작되면(Interact()) 그 즉시
        // isFacingPlayer가 자연스럽게 이어받으므로, cutsceneForcingFace를 따로 꺼줄 필요가 없습니다.
        bool isFacingSomething = isFacingPlayer || cutsceneForcingFace;
        Quaternion modelTargetRotation;
        if (isFacingPlayer && playerTransform != null)
        {
            modelTargetRotation = FaceRotationTowards(modelTarget, playerTransform.position);
        }
        else if (cutsceneForcingFace)
        {
            modelTargetRotation = FaceRotationTowards(modelTarget, cutsceneFaceTarget);
        }
        else
        {
            modelTargetRotation = originalModelRotation;
        }
        Vector3 modelTargetPosition = isFacingSomething ? modelTarget.position : originalModelPosition;

        modelTarget.rotation = Quaternion.RotateTowards(modelTarget.rotation, modelTargetRotation, talkRotationSpeed * Time.deltaTime);
        modelTarget.position = Vector3.MoveTowards(modelTarget.position, modelTargetPosition, positionReturnSpeed * Time.deltaTime);
    }

    /// <summary>대화가 실제로 시작되기 전, 컷씬 등에서 미리 NPC가 worldPosition(보통 플레이어 위치) 쪽을
    /// 바라보게 하고 싶을 때 호출하세요(예: 대화 직전 눈 마주침 클로즈업 연출 - CutsceneManager의
    /// FacePlayerAndNpc 스텝 참고). 한 번만 호출하면 되고, 그 뒤로는 이 NPC의 Update()가 매 프레임
    /// 알아서 talkRotationSpeed로 계속 그 방향을 향해 부드럽게 돌립니다(instant가 true면 그 자리에서
    /// 즉시 스냅). 보통 바로 뒤에 StartDialogue로 이어지는 구성으로 사용하세요 - Interact()가 호출되는
    /// 순간부터는 isFacingPlayer가 자연스럽게 이어받습니다.</summary>
    public void CutsceneSetFaceTarget(Vector3 worldPosition, bool instant)
    {
        cutsceneForcingFace = true;
        cutsceneFaceTarget = worldPosition;

        if (instant)
        {
            modelTarget.rotation = FaceRotationTowards(modelTarget, worldPosition);
        }
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

    /// <summary>이번 대화가 시작할 Talks.index를 결정합니다. 4단계 우선순위(파일 상단 [여러 퀘스트를
    /// 추적]/[다른 퀘스트의 완료 여부로 대사 분기] 참고):
    ///   1) "지금 당장 처리해야 할 용무"가 최우선입니다 - relatedQuests를 배열 순서대로 훑어서,
    ///      진행 중(Active)이거나 완료 보고를 기다리는 중(ReadyToTurnIn)인 첫 번째 항목을 즉시
    ///      반환합니다. 이미 완료된 항목은 여기서 건너뜁니다(더 이상 "당장 처리할 용무"가
    ///      아니므로) - 이게 이전 버전과 다른 핵심 차이입니다.
    ///   2) 1단계에서 아무 것도 안 걸렸다면(진행 중인 relatedQuests가 하나도 없다면),
    ///      questCompletionOverrides를 배열 순서대로 확인해서 완료된 퀘스트를 가진 첫 번째 항목의
    ///      talkIndex를 반환합니다 - "사전 퀘스트(A)가 막 완료되어 다음 퀘스트(B)를 새로 제안해야
    ///      하는 순간"이 정확히 여기서 잡힙니다.
    ///   3) 2단계에서도 새로 제안할 게 없었다면, relatedQuests를 이번엔 "배열 역순으로"(마지막
    ///      항목부터 거꾸로) 훑어서 이미 완료된 첫 번째 항목의 completedTalkIndex를 반환합니다(더
    ///      이상 진행 중인 것도, 새로 제안할 것도 없는 "다 끝난" 상태의 마무리 대사). 역순으로 찾는
    ///      이유는 relatedQuests가 보통 진행 순서대로 등록되어 있어서, 정순으로 찾으면 체인을 끝까지
    ///      다 깨도 항상 맨 처음 퀘스트의 완료 대사로 돌아가 버리기 때문입니다.
    ///   4) 그마저도 없으면(전부 아직 안 받은 상태) relatedQuests가 비어있지 않은 경우
    ///      relatedQuests[0].notStartedTalkIndex(맨 처음 기본 대사)를, relatedQuests가 비어있거나
    ///      씬에 QuestManager가 없으면 -1을 반환해서 TalkManager.StartTalk()가 기존과 동일하게
    ///      배열 맨 처음(talks[0])부터 시작하게 합니다.</summary>
    private int ResolveStartTalkIndex()
    {
        // 1단계: 진행 중이거나 완료 보고를 기다리는 relatedQuests가 있으면 항상 최우선입니다.
        if (QuestManager.Instance != null && relatedQuests != null)
        {
            foreach (RelatedQuestEntry entry in relatedQuests)
            {
                if (entry.quest == null) continue;

                if (QuestManager.Instance.IsQuestReadyToTurnIn(entry.quest)) return entry.readyToTurnInTalkIndex;
                if (QuestManager.Instance.IsQuestActive(entry.quest)) return entry.inProgressTalkIndex;
            }
        }

        // 2단계: 지금 당장 진행 중인 relatedQuests가 없습니다 - 사전 퀘스트가 막 완료되어 새로
        // 제안할 게 있는지 확인합니다(예: A가 막 완료되어 B를 제안해야 하는 순간).
        if (QuestManager.Instance != null && questCompletionOverrides != null)
        {
            foreach (QuestCompletionOverride overrideEntry in questCompletionOverrides)
            {
                if (overrideEntry.quest != null && QuestManager.Instance.IsQuestCompleted(overrideEntry.quest))
                {
                    return overrideEntry.talkIndex;
                }
            }
        }

        // 3단계: 새로 제안할 것도 없습니다 - relatedQuests 중 완료된 게 있으면(체인이 여기서 끝난
        // 경우 등) 마무리 대사를 보여줘야 하는데, 이때는 배열을 "뒤에서부터" 훑습니다 - relatedQuests를
        // 보통 진행 순서(A, B, C...)대로 등록해두므로, 뒤에서부터 찾아야 "가장 나중 단계까지 완료된"
        // 항목이 먼저 걸립니다(안 그러면 체인을 끝까지 다 깨도 맨 처음 단계의 완료 대사로 돌아가
        // 버립니다).
        if (QuestManager.Instance != null && relatedQuests != null)
        {
            for (int i = relatedQuests.Length - 1; i >= 0; i--)
            {
                RelatedQuestEntry entry = relatedQuests[i];
                if (entry.quest != null && QuestManager.Instance.IsQuestCompleted(entry.quest))
                {
                    return entry.completedTalkIndex;
                }
            }
        }

        // 4단계: 정말 아무 것도 해당하지 않으면(전부 아직 안 받은 상태) 맨 처음 기본 대사입니다.
        if (relatedQuests == null || relatedQuests.Length == 0 || QuestManager.Instance == null) return -1;

        return relatedQuests[0].notStartedTalkIndex;
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

        // 반드시 ReportTalkToNpc()보다 먼저 결정합니다 - 이 상호작용 자체가 "이 NPC에게 말 걸기" 목표를
        // 완료시켜버릴 수도 있는데, 그 경우에도 지금 보여줄 대사는 퀘스트가 막 완료되기 "직전" 상태
        // 기준으로 결정되어야 자연스럽습니다(완료되자마자 곧바로 "완료된 뒤"의 대사로 바뀌어버리면
        // 지금 막 시작하는 이 대화 자체와 안 맞습니다).
        int startTalkIndex = ResolveStartTalkIndex();

        if (!string.IsNullOrEmpty(npcId))
        {
            QuestManager.Instance?.ReportTalkToNpc(npcId);
        }

        TalkManager.Instance.StartTalk(talkScript, anchorTransform, startTalkIndex);
    }
}