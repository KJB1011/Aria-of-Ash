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
//   - SetLocationTitleVisible: showLocationTitle이 true면 locationTitleSprite로 화면에 지역 이름
//     로고/타이틀 이미지를(CutsceneManager에 내장된 Location Title Canvas Group + Image) duration초에
//     걸쳐 페이드인하고, false면 지금 떠 있는 타이틀을 duration초에 걸쳐 페이드아웃합니다 -
//     FadeOut/FadeIn처럼 그 시간만큼 다음 스텝으로 넘어가지 않고 기다립니다. 보통 Establishing
//     Shot(마을을 훑는 오프닝 샷)이 화면에 보이는 동안 잠깐 띄웠다가(show) 얼마간 유지한(Wait) 뒤
//     다시 내리는(hide) 순서로 사용하세요.
//
// [예시 - 마을 입구 컷씬]
//   FadeOut(0.5) → ActivateCamera("Establishing") → FadeIn(1) →
//   SetLocationTitleVisible(show, 루멘마을 로고 스프라이트, duration 1) → Wait(2) →
//   SetLocationTitleVisible(hide, duration 1) → Wait(1) → SetHudVisible(false) →
//   ActivateCamera("Discovery") → WalkPlayerToWaypoints("MerchantApproach") → Wait(2) →
//   FacePlayerAndNpc("Merchant", duration 0.5) → ActivateCamera("PlayerCloseUp") → Wait(1) →
//   ActivateCamera("MerchantCloseUp") → Wait(1) → SetHudVisible(true) → StartDialogue("Merchant")
//   (CutsceneManager는 순서를 자동으로 맞춰주지 않고 적어둔 그대로 실행하므로, HUD를 언제 끄고 켤지
//   등의 순서는 직접 신경 써서 배치하세요. 위 예시의 WalkPlayerToWaypoints 뒤에 붙은 Wait(2)도
//   마찬가지입니다 - 이제 걷기가 도착을 기다려주지 않으니, 실제로 다 걸어가는 데 걸리는 시간(거리 ÷
//   cutsceneWalkSpeed)만큼을 직접 계산해서 채워줘야 PlayerCloseUp 카메라가 너무 일찍 켜지지 않습니다.)
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
        SetLocationTitleVisible,
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

        [Header("FadeOut / FadeIn / Wait / FacePlayerAndNpc / SetLocationTitleVisible 전용 - 시간(초)")]
        [Tooltip("FadeOut/FadeIn/Wait에서는 그대로 지속 시간입니다. FacePlayerAndNpc에서는 서로를 " +
                  "바라보는 회전에 걸리는 시간으로 쓰이며 0이면 즉시(스냅) 마주봅니다. " +
                  "SetLocationTitleVisible에서는 지역 이름 타이틀이 페이드인/아웃되는 시간입니다.")]
        public float duration = 1f;

        [Header("SetHudVisible 전용")]
        public bool hudVisible = true;

        [Header("SetLocationTitleVisible 전용")]
        [Tooltip("켜면 Location Title Sprite로 화면에 지역 이름 로고/타이틀 이미지를 페이드인해서 " +
                  "보여주고, 끄면 지금 떠 있는 타이틀을 페이드아웃합니다.")]
        public bool showLocationTitle = true;
        [Tooltip("Show Location Title이 켜져 있을 때(보여줄 때)만 사용하는 지역 이름 로고/타이틀 " +
                  "이미지입니다. 예: \"루멘 마을\" 로고 스프라이트.")]
        public Sprite locationTitleSprite;

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
    }

    [Tooltip("이 컷씬을 구성하는 스텝들입니다. 순서대로 하나씩 실행됩니다(CutsceneManager.cs 참고).")]
    public Step[] steps = new Step[0];
}