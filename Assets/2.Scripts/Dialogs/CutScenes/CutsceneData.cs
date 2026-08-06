// ============================================================================
// CutsceneData.cs
// ----------------------------------------------------------------------------
// 컷씬 하나를 정의하는 ScriptableObject입니다. TalkScript/QuestData와 같은 방식으로 애셋으로 만들어
// 자유롭게 여러 개 제작할 수 있습니다 - steps 배열에 원하는 연출을 순서대로 나열해두면, 그 순서
// 그대로 CutsceneManager가 재생합니다.
//
// [애셋은 씬 오브젝트를 직접 참조할 수 없습니다 - 대신 문자열 키로 연결]
//   TalkScript.onTalkStart와 같은 이유로, 이 애셋은 특정 씬에 있는 카메라/웨이포인트/NPC를 직접
//   참조할 수 없습니다(애셋이 Hierarchy 오브젝트를 직접 참조하는 걸 유니티가 허용하지 않기
//   때문입니다 - TalkScript.cs 상단 주석 참고). 그래서 카메라/웨이포인트 그룹/NPC를 가리킬 때는 실제
//   오브젝트 대신 문자열 키(예: "Establishing", "MerchantApproach", "Merchant")를 적어두고, 실제
//   연결은 각 씬에 있는 CutsceneManager가 그 키와 같은 이름으로 등록해둔 진짜 카메라/웨이포인트/NPC를
//   찾아서 대신 처리합니다(VFXManager.Play(이름)가 Resources/VFX의 프리팹을 이름으로 찾는 것과 같은
//   방식) - CutsceneManager.cs 참고.
//
// [스텝(Step) 종류]
//   - FadeOut / FadeIn: 화면을 duration초에 걸쳐 까맣게/다시 보이게 합니다(GameManager가 담당).
//   - Wait: 아무 것도 안 하고 duration초 동안 대기합니다(카메라를 그대로 유지하고 싶을 때 등).
//   - SetHudVisible: HP/MP 등 인게임 HUD를 즉시 켜거나 끕니다.
//   - ActivateCamera: cameraKey와 같은 이름으로 CutsceneManager에 등록된 카메라를 활성화합니다(이전에
//     활성화했던 카메라가 있으면 그건 자동으로 내려갑니다) - 활성화만 하고 곧바로 다음 스텝으로
//     넘어가므로, 이 카메라를 잠깐이라도 유지하고 싶으면 바로 뒤에 Wait 스텝을 추가하세요.
//   - WalkPlayerToWaypoints: waypointGroupKey와 같은 이름으로 등록된 지점들을 플레이어가 순서대로 자동으로
//     걸어갑니다. [중요] 도착할 때까지 기다리지 않고 걷기 시작만 시킨 뒤 그 즉시 다음 스텝으로
//     넘어갑니다(백그라운드에서 계속 걸어감) - 그동안 다른 연출(카메라 전환 등)과 타이밍을 맞추고
//     싶으면 바로 뒤에 Wait 스텝을 넣어 원하는 만큼 대기 시간을 직접 지정하세요. 걷는 속도는
//     PlayerController의 cutsceneWalkSpeed(인스펙터에서 조절)를 그대로 사용합니다 - 이동 중엔 IsMove가
//     아니라 IsWalk 애니메이터 bool로 걷기 모션을 재생합니다(평소 이동과 구분되는 이벤트 전용 모션이기
//     때문입니다).
//   - StartDialogue: npcKey와 같은 이름으로 등록된 NPCTalker.Interact()를 그대로 호출합니다 - 그
//     NPC에 이미 연결해둔 TalkScript/퀘스트 상태별 대사 분기 등 기존 상호작용 로직이 그대로
//     적용됩니다(새 대화 시스템을 따로 만들지 않고 기존 것을 재사용합니다).
//   - TeleportPlayer: teleportPointKey와 같은 이름으로 등록된 위치/회전으로 플레이어를 그 자리에서
//     즉시 순간이동시킵니다(걷는 모션 없음, 시간이 걸리지 않고 그 즉시 다음 스텝으로 넘어갑니다).
//     화면이 까맣게 가려진 동안(FadeOut ~ FadeIn 사이)에 넣어서 "화면이 까매진 사이에 플레이어가
//     다른 위치로 옮겨져 있는" 연출에 사용하세요 - WalkPlayerToWaypoints처럼 실제로 걷는 모습을
//     보여줄 필요가 없을 때(먼 거리를 자연스럽게 다 걷게 하기엔 너무 오래 걸릴 때 등)에 적합합니다.
//   - FacePlayerAndNpc: npcKey와 같은 이름으로 등록된 NPC와 플레이어가 서로를 바라보게(눈 마주침)
//     돌려세웁니다. duration이 0이면 그 자리에서 즉시(스냅) 마주보고 바로 다음 스텝으로 넘어가며,
//     0보다 크면 그 시간(초) 동안 부드럽게 서로를 향해 돌아가고 그동안은 다음 스텝으로 넘어가지
//     않습니다(FadeOut/FadeIn과 같은 "duration만큼 대기" 방식). 보통 눈 마주침 클로즈업 카메라
//     (ActivateCamera)와 짝지어 쓰고, 바로 뒤에 StartDialogue로 이어지는 구성을 권장합니다 - 그
//     순간부터는 대화 시스템 자체의 "플레이어 바라보기"가 자연스럽게 이어받습니다.
//   - SetTitleCardVisible: titleCardKey와 같은 이름으로 CutsceneManager에 등록된 "타이틀 카드"
//     오브젝트를 켜거나 끕니다(지역 이름 로고, 보스 등장 이름 카드 등 화면에 잠깐 띄웠다 내리는 연출
//     전부 이 스텝 하나로 처리합니다) - TriggerEvent와 마찬가지로 여러 개를 등록해두고 titleCardKey로
//     원하는 걸 골라 쓰는 범용 슬롯 구조입니다. 등록된 오브젝트에 CanvasGroup이 있으면(단순 로고
//     이미지 카드) duration초에 걸쳐 부드럽게 페이드인/아웃하고(FadeOut/FadeIn처럼 그 시간만큼 다음
//     스텝으로 넘어가지 않고 기다립니다), CanvasGroup이 없으면(Animator/Particle System 등 자체
//     연출을 가진 오브젝트 - 글씨에 이펙트가 들어간 보스 이름 카드 등) duration을 무시하고 즉시
//     SetActive로 켜고 끄며, 오브젝트 자신의 OnEnable 등에서 인트로 연출이 알아서 재생되도록
//     구성하면 됩니다(그 경우 이 스텝은 곧바로 다음 스텝으로 넘어갑니다). titleCardSpriteOverride를
//     채워두면(CanvasGroup 카드에 한해, 켤 때만 적용) 등록된 오브젝트의 Image 컴포넌트 스프라이트를
//     그때그때 바꿔서 하나의 카드를 여러 지역 로고 등으로 재사용할 수도 있습니다 - 비워두면 지금
//     설정된 스프라이트를 그대로 씁니다. 보통 화면에 보이는 동안 잠깐 띄웠다가(show) 얼마간
//     유지한(Wait) 뒤 다시 내리는(hide) 순서로 사용하세요. 등록 방법은 CutsceneManager.cs의
//     [씬 준비] 항목을 참고하세요.
//   - TriggerEvent: eventKey와 같은 이름으로 CutsceneManager에 등록된 UnityEvent를 그대로
//     Invoke()합니다 - 그 즉시(yield 없이) 다음 스텝으로 넘어갑니다. "한 번 실행되고 끝나는" 연출용
//     범용 스텝입니다(보스 애니메이터 트리거 재생, 연출용 공격 재생, 오브젝트 SetActive 등) - 새로운
//     이런 종류의 기능이 필요할 때마다 StepType/Step 필드/switch case를 새로 추가할 필요 없이,
//     CutsceneManager의 Trigger Events 리스트에 이름(키)과 UnityEvent(호출하고 싶은 아무 public
//     메서드나 인스펙터에서 연결)만 등록하면 됩니다 - 자세한 등록 방법은 CutsceneManager.cs의
//     [씬 준비] 항목을 참고하세요. 재생이 끝날 때까지 기다리고 싶으면(모션/연출 시간만큼) 바로 뒤에
//     Wait 스텝을 원하는 시간만큼 넣으세요.
//   - GrantQuest: questToGrant에 직접 연결해둔 QuestData를 QuestManager.Instance.AddQuest()로
//     즉시 지급합니다 - 그 즉시(yield 없이) 다음 스텝으로 넘어갑니다. [TriggerEvent와 다른 점] 다른
//     스텝들과 달리 이 스텝은 문자열 키로 CutsceneManager에 등록해서 쓰지 않고, QuestData 애셋을
//     이 스텝 필드에 직접 연결합니다 - TalkScript.Choice.questToGrant/questToTurnIn과 똑같이 둘 다
//     ScriptableObject 애셋이라 애셋끼리는 서로 직접 참조해도 안전하기 때문입니다(파일 상단 [애셋은
//     씬 오브젝트를 직접 참조할 수 없습니다] 참고 - 그건 애셋이 "씬" 오브젝트를 참조할 수 없다는
//     제약이지, 애셋-애셋 참조는 원래부터 문제가 없습니다). 이렇게 해두면 이 컷씬 애셋 하나만으로
//     어떤 씬에서 재생되든(그 씬의 CutsceneManager에 등록 작업을 미리 해두지 않아도) 항상 같은
//     퀘스트를 지급할 수 있어서, TriggerEvent 방식보다 오히려 더 간단하고 이식성이 좋습니다 - 그래서
//     TriggerEvent로 새 스텝을 만들지 않고 예외적으로 전용 스텝을 추가했습니다.
//   - SetFogDensity: RenderSettings.fogDensity(Lighting → Environment → Fog)를 지금 값에서
//     targetFogDensity까지 duration초에 걸쳐 부드럽게 변화시킵니다(FadeOut/FadeIn과 같은 "duration만큼
//     대기" 방식 - 다 바뀔 때까지 다음 스텝으로 넘어가지 않습니다). duration이 0이면 대기 없이 즉시
//     그 값으로 바뀌고 바로 다음 스텝으로 넘어갑니다. WalkPlayerToWaypoints/FacePlayerAndNpc처럼
//     "진행 상태를 추적하며 기다려야 하는" 종류라 TriggerEvent로 만들지 않고 전용 스텝으로
//     추가했습니다 - Density 하나를 Time.timeScale이 0이어도(다른 팝업 등으로 게임이 멈춰있어도)
//     계속 부드럽게 변화시키기 위해 DOTween(.SetUpdate(true))을 사용합니다.
//
// [예시 - 마을 입구 컷씬]
//   FadeOut(0.5) → ActivateCamera("Establishing") → FadeIn(1) →
//   SetTitleCardVisible("LocationTitle", show, 루멘마을 로고 스프라이트, duration 1) → Wait(2) →
//   SetTitleCardVisible("LocationTitle", hide, duration 1) → Wait(1) → SetHudVisible(false) →
//   ActivateCamera("Discovery") → WalkPlayerToWaypoints("MerchantApproach") → Wait(2) →
//   FacePlayerAndNpc("Merchant", duration 0.5) → ActivateCamera("PlayerCloseUp") → Wait(1) →
//   ActivateCamera("MerchantCloseUp") → Wait(1) → SetHudVisible(true) → StartDialogue("Merchant")
//   (CutsceneManager는 순서를 자동으로 맞춰주지 않고 적어둔 그대로 실행하므로, HUD를 언제 끄고 켤지
//   등의 순서는 직접 신경 써서 배치하세요. 위 예시의 WalkPlayerToWaypoints 뒤에 붙은 Wait(2)도
//   마찬가지입니다 - 이제 걷기가 도착을 기다려주지 않으니, 실제로 다 걸어가는 데 걸리는 시간(거리 ÷
//   cutsceneWalkSpeed)만큼을 직접 계산해서 채워줘야 PlayerCloseUp 카메라가 너무 일찍 켜지지 않습니다.)
//
// [예시 - MiddleSlime 등장 컷씬]
//   ActivateCamera("SlimeCloseUp") → TriggerEvent("RemoveBlockingRock") →
//   SetTitleCardVisible("MiddleSlimeName", show, duration 0.5) →
//   TriggerEvent("MiddleSlimeAppear") → Wait(2) → TriggerEvent("MiddleSlimeShockwave") → Wait(1) →
//   SetTitleCardVisible("MiddleSlimeName", hide, duration 0.5) → SetHudVisible(true)
//   (TriggerEvent는 즉시 다음 스텝으로 넘어가므로, 모션/연출이 끝날 때까지 자연스럽게 유지하려면
//   그 뒤에 원하는 만큼 Wait을 직접 넣어주세요. 위 3개의 TriggerEvent 키는 CutsceneManager의
//   Trigger Events 리스트에 각각 등록해두면 됩니다 - 예: "RemoveBlockingRock" → 바위 오브젝트의
//   SetActive(false), "MiddleSlimeAppear" → MiddleSlimeBoss.PlayAppearAnimation(),
//   "MiddleSlimeShockwave" → MiddleSlimeBoss.PlayShockwaveForCutscene(). "MiddleSlimeName"은
//   Title Cards 리스트에 등록해둔, 글씨에 이펙트가 들어간(Animator/Particle System 등) 보스 이름
//   카드 오브젝트입니다 - CanvasGroup이 없으므로 duration과 무관하게 즉시 SetActive되고, 카드
//   자신의 인트로 연출이 알아서 재생됩니다.)
//
// [예시 - 컷씬 중에 퀘스트 지급]
//   FadeOut(0.5) → ActivateCamera("VillageChiefCloseUp") → FadeIn(1) →
//   StartDialogue("VillageChief") → Wait(3) → GrantQuest(촌장의 부탁 QuestData) → Wait(0.5) →
//   SetHudVisible(true) → FadeOut(0.3) → ActivateCamera("Default") → FadeIn(0.5)
//   (GrantQuest는 문자열 키가 아니라 이 스텝의 questToGrant 필드에 QuestData 애셋을 직접
//   드래그해서 연결합니다 - 위 [스텝 종류] GrantQuest 항목 참고.)
//
// [예시 - 중급슬라임 처치 후 마을 정화 컷씬]
//   FadeOut(0.5) → ActivateCamera("VillagePurifyView") → FadeIn(1) →
//   TriggerEvent("PurifySea") → SetFogDensity(target 0.002, duration 3) → Wait(3) →
//   FadeOut(0.5) → ActivateCamera("Default") → FadeIn(1)
//   (이 컷씬은 QuestData.cutsceneOnComplete로 재생되므로 - QuestData.cs 상단 [완료 시 컷씬 재생]
//   참고 - "중급슬라임 처치" 퀘스트 애셋의 Cutscene On Complete 필드에 이 컷씬 애셋을 직접
//   연결하기만 하면 됩니다(별도 트리거 오브젝트가 필요 없습니다). "PurifySea"는 CutsceneManager의
//   Trigger Events 리스트에 등록해두는 키로, SeaPurifyTransition.BeginPurifyTransition()을 연결해두면
//   호출되는 즉시 오염된 바다 → 깨끗한 바다로 알파 크로스페이드가 시작됩니다(SeaPurifyTransition.cs
//   참고) - TriggerEvent 자체는 즉시(yield 없이) 다음 스텝으로 넘어가므로, 뒤의 Wait(3)이 그 크로스
//   페이드 시간(SeaPurifyTransition.duration)과 같은 시간만큼 기다려줍니다(WalkPlayerToWaypoints,
//   MiddleSlimeBoss.PlayShockwaveForCutscene() 등과 같은 "TriggerEvent + Wait" 패턴). SetFogDensity도
//   같은 3초 동안 안개가 서서히 옅어지도록 나란히 배치해서, 바다와 안개가 함께 정화되는 느낌을
//   줍니다.)
//
// [애셋 만들기]
//   Project 창에서 우클릭 → Create → Cutscene > Cutscene Data 로 새 컷씬 애셋을 만드세요.
// ============================================================================

using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Cutscene_New", menuName = "Cutscene/Cutscene Data")]
public class CutsceneData : ScriptableObject
{
    public enum StepType
    {
        FadeOut,
        FadeIn,
        Wait,
        SetHudVisible,
        ActivateCamera,
        WalkPlayerToWaypoints,
        StartDialogue,
        TeleportPlayer,
        FacePlayerAndNpc,
        SetTitleCardVisible,
        TriggerEvent,
        GrantQuest,
        SetFogDensity,
    }

    [Serializable]
    public class Step
    {
        [Tooltip("이 스텝의 종류입니다. 아래 필드들은 이 종류에 해당하는 것만 채우면 되고, 나머지는 " +
                  "무시됩니다.")]
        public StepType type;

        [Tooltip("에디터에서 이 스텝을 구분하기 쉽도록 붙이는 메모입니다 - 실행에는 전혀 영향을 주지 " +
                  "않습니다. 예: \"화면 페이드아웃\"")]
        public string label;

        [Header("FadeOut / FadeIn / Wait / FacePlayerAndNpc / SetTitleCardVisible / SetFogDensity 전용 - 시간(초)")]
        [Tooltip("FadeOut/FadeIn/Wait에서는 그대로 지속 시간입니다. FacePlayerAndNpc에서는 서로를 " +
                  "바라보는 회전에 걸리는 시간으로 쓰이며 0이면 즉시(스냅) 마주봅니다. " +
                  "SetTitleCardVisible에서는(등록된 카드에 CanvasGroup이 있는 경우에 한해) 페이드인/ " +
                  "아웃되는 시간입니다 - CanvasGroup이 없는 카드(자체 연출 오브젝트)라면 무시됩니다. " +
                  "SetFogDensity에서는 지금 안개 Density에서 targetFogDensity까지 변화하는 데 걸리는 " +
                  "시간이며, 0이면 대기 없이 즉시 그 값으로 바뀝니다.")]
        public float duration = 1f;

        [Header("SetHudVisible 전용")]
        public bool hudVisible = true;

        [Header("SetTitleCardVisible 전용")]
        [Tooltip("CutsceneManager의 Title Cards 리스트에 등록된 타이틀 카드 키입니다. 예: " +
                  "\"LocationTitle\", \"MiddleSlimeName\".")]
        public string titleCardKey;
        [Tooltip("켜면(true) 표시하고, 끄면(false) 다시 숨깁니다.")]
        public bool titleCardVisible = true;
        [Tooltip("표시(Title Card Visible 켜짐)할 때만 적용됩니다 - 등록된 카드 오브젝트에 Image " +
                  "컴포넌트가 있으면(자식 포함) 이 스프라이트로 바꿔서 보여줍니다. 비워두면 스프라이트를 " +
                  "바꾸지 않고 지금 설정되어 있는 그대로 보여줍니다(CanvasGroup이 없는, 자체 연출을 가진 " +
                  "카드에는 애초에 적용할 Image가 없을 수 있으니 그런 카드는 비워두세요). 예: 같은 " +
                  "\"LocationTitle\" 카드를 지역마다 다른 로고로 재사용하고 싶을 때 사용하세요.")]
        public Sprite titleCardSpriteOverride;

        [Header("ActivateCamera 전용")]
        [Tooltip("CutsceneManager의 Cameras 리스트에 등록된 카메라 키입니다. 대소문자를 구분합니다.")]
        public string cameraKey;

        [Header("WalkPlayerToWaypoints 전용")]
        [Tooltip("CutsceneManager의 Waypoint Groups 리스트에 등록된 웨이포인트 그룹 키입니다.")]
        public string waypointGroupKey;
        [Tooltip("한 지점에 이만큼(미터) 가까워지면 도착으로 보고 다음 지점으로 넘어갑니다. (걷는 속도 " +
                  "자체는 여기가 아니라 PlayerController.cutsceneWalkSpeed에서 조절합니다.)")]
        public float arriveDistance = 0.2f;

        [Header("StartDialogue / FacePlayerAndNpc 전용")]
        [Tooltip("CutsceneManager의 Npcs 리스트에 등록된 NPC 키입니다. StartDialogue에서는 그 " +
                  "NPCTalker.Interact()를 그대로 호출하고, FacePlayerAndNpc에서는 이 NPC와 플레이어를 " +
                  "서로 마주보게 돌립니다.")]
        public string npcKey;

        [Header("TeleportPlayer 전용")]
        [Tooltip("CutsceneManager의 Teleport Points 리스트에 등록된 위치 키입니다. 그 지점의 위치/회전으로 " +
                  "플레이어를 즉시 순간이동시킵니다(걷는 모션 없이 그 자리에서 바로 바뀝니다) - 화면이 " +
                  "까맣게 가려진 FadeOut ~ FadeIn 사이에 넣어서 쓰세요.")]
        public string teleportPointKey;

        [Header("TriggerEvent 전용")]
        [Tooltip("CutsceneManager의 Trigger Events 리스트에 등록된 이벤트 키입니다. 그 UnityEvent를 " +
                  "그대로 Invoke()합니다 - 무엇을 실행할지는 이 애셋이 아니라 CutsceneManager 쪽 " +
                  "인스펙터에서 UnityEvent에 연결해둔 대로 결정됩니다(예: 보스 애니메이터 트리거 재생, " +
                  "오브젝트 SetActive 등). 대소문자를 구분합니다.")]
        public string eventKey;

        [Header("GrantQuest 전용")]
        [Tooltip("이 컷씬이 재생되는 도중 QuestManager.Instance.AddQuest()로 지급할 퀘스트입니다. " +
                  "TriggerEvent처럼 CutsceneManager에 문자열 키로 등록해두는 방식이 아니라, " +
                  "TalkScript.Choice.questToGrant와 똑같이 이 애셋에 QuestData를 직접 연결합니다 - 둘 다 " +
                  "ScriptableObject 애셋이라 안전하며, 어느 씬에서 재생되든 별도의 씬별 등록 작업 없이 " +
                  "항상 같은 퀘스트를 지급할 수 있습니다(파일 상단 [스텝 종류] GrantQuest 항목 참고). " +
                  "비워두면 경고만 남기고 아무 것도 지급하지 않습니다.")]
        public QuestData questToGrant;

        [Header("SetFogDensity 전용")]
        [Tooltip("RenderSettings.fogDensity(Lighting → Environment → Fog)가 이 값까지 duration초에 걸쳐 " +
                  "부드럽게 변화합니다. 예: 오염된 마을을 정화하며 안개를 0.005 → 0.002로 옅어지게 " +
                  "만들 때 0.002를 넣으세요.")]
        public float targetFogDensity = 0.01f;
    }

    [Header("컷씬 전용 배경음악 (선택)")]
    [Tooltip("이 컷씬이 재생되는 동안 틀 배경음악입니다(Resources/BGM/ 아래 클립 이름과 일치해야 함). " +
              "비워두면 지금 재생 중이던 배경음악(필드 곡이든 전투 곡이든)이 그대로 계속 재생됩니다. " +
              "채워두면 컷씬이 시작되는 순간 이 곡으로 전환되고, 컷씬이 끝나면(끝까지 재생되거나 " +
              "CutsceneManager.StopCutscene()으로 중간에 끊기거나 상관없이) 컷씬 시작 전에 재생 중이던 " +
              "곡으로 자동으로 되돌아갑니다 - SoundManager.SetFieldBGM()이 아니라 CutsceneManager가 직접 " +
              "SoundManager.PlayBGM()을 호출하는 방식이라, 전투 중에 컷씬이 시작돼도 이 곡이 확실하게 " +
              "우선합니다(자세한 내용은 CutsceneManager.cs 상단 [컷씬 전용 배경음악] 참고).")]
    public string bgmName;
    [Tooltip("bgmName으로 전환/복귀될 때의 크로스페이드 시간(초). 시작/종료 양쪽에 똑같이 적용됩니다.")]
    public float bgmFadeDuration = 1f;

    [Tooltip("이 컷씬을 구성하는 스텝들입니다. 순서대로 하나씩 실행됩니다(CutsceneManager.cs 참고).")]
    public Step[] steps = new Step[0];
}