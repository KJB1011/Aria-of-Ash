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
//     걸어갑니다(전부 도착할 때까지 다음 스텝으로 넘어가지 않습니다). 걷는 속도는 PlayerController의
//     cutsceneWalkSpeed(인스펙터에서 조절)를 그대로 사용합니다 - 이동 중엔 IsMove가 아니라 IsWalk
//     애니메이터 bool로 걷기 모션을 재생합니다(평소 이동과 구분되는 이벤트 전용 모션이기 때문입니다).
//   - StartDialogue: npcKey와 같은 이름으로 등록된 NPCTalker.Interact()를 그대로 호출합니다 - 그
//     NPC에 이미 연결해둔 TalkScript/퀘스트 상태별 대사 분기 등 기존 상호작용 로직이 그대로
//     적용됩니다(새 대화 시스템을 따로 만들지 않고 기존 것을 재사용합니다).
//   - TeleportPlayer: teleportPointKey와 같은 이름으로 등록된 위치/회전으로 플레이어를 그 자리에서
//     즉시 순간이동시킵니다(걷는 모션 없음, 시간이 걸리지 않고 그 즉시 다음 스텝으로 넘어갑니다).
//     화면이 까맣게 가려진 동안(FadeOut ~ FadeIn 사이)에 넣어서 "화면이 까매진 사이에 플레이어가
//     다른 위치로 옮겨져 있는" 연출에 사용하세요 - WalkPlayerToWaypoints처럼 실제로 걷는 모습을
//     보여줄 필요가 없을 때(먼 거리를 자연스럽게 다 걷게 하기엔 너무 오래 걸릴 때 등)에 적합합니다.
//
// [예시 - 마을 입구 컷씬]
//   FadeOut(0.5) → ActivateCamera("Establishing") → FadeIn(1) → Wait(3) → SetHudVisible(false) →
//   ActivateCamera("Discovery") → WalkPlayerToWaypoints("MerchantApproach") → Wait(0.5) →
//   ActivateCamera("PlayerCloseUp") → Wait(1) → ActivateCamera("MerchantCloseUp") → Wait(1) →
//   SetHudVisible(true) → StartDialogue("Merchant")
//   (CutsceneManager는 순서를 자동으로 맞춰주지 않고 적어둔 그대로 실행하므로, HUD를 언제 끄고 켤지
//   등의 순서는 직접 신경 써서 배치하세요.)
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

        [Header("FadeOut / FadeIn / Wait 전용 - 시간(초)")]
        public float duration = 1f;

        [Header("SetHudVisible 전용")]
        public bool hudVisible = true;

        [Header("ActivateCamera 전용")]
        [Tooltip("CutsceneManager의 Cameras 리스트에 등록된 카메라 키입니다. 대소문자를 구분합니다.")]
        public string cameraKey;

        [Header("WalkPlayerToWaypoints 전용")]
        [Tooltip("CutsceneManager의 Waypoint Groups 리스트에 등록된 웨이포인트 그룹 키입니다.")]
        public string waypointGroupKey;
        [Tooltip("한 지점에 이만큼(미터) 가까워지면 도착으로 보고 다음 지점으로 넘어갑니다. (걷는 속도 " +
                  "자체는 여기가 아니라 PlayerController.cutsceneWalkSpeed에서 조절합니다.)")]
        public float arriveDistance = 0.2f;

        [Header("StartDialogue 전용")]
        [Tooltip("CutsceneManager의 Npcs 리스트에 등록된 NPC 키입니다. 그 NPCTalker.Interact()를 그대로 " +
                  "호출합니다.")]
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