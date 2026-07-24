// ============================================================================
// CutsceneManager.cs
// ----------------------------------------------------------------------------
// 씬에 하나만 두는 컷씬 재생 엔진입니다. CutsceneData(steps 배열)를 받아 그 안의 스텝을 순서대로
// 실행합니다 - 실제 컷씬 "내용"은 전부 CutsceneData 애셋 쪽에 있고, 이 스크립트는 그 스텝들이
// 참조하는 문자열 키(카메라/웨이포인트 그룹/NPC)를 이 씬의 진짜 오브젝트로 연결해주는 역할과, 페이드/
// HUD/플레이어 이동 등 실제 실행을 담당합니다(CutsceneData.cs 상단 주석도 함께 참고하세요).
//
// [새 컷씬을 추가하려면 - TalkScript/QuestData와 같은 방식]
//   1) Project 창에서 새 CutsceneData 애셋을 만들고 steps를 원하는 대로 채우세요.
//   2) 그 컷씬이 참조하는 카메라/웨이포인트 그룹/NPC 키가 이 씬의 Cameras/Waypoint Groups/Npcs
//      리스트에 이미 등록되어 있는지 확인하세요(없으면 새로 등록하세요 - 카메라/웨이포인트/NPC
//      자체는 여러 CutsceneData가 키만 다르게 참조하며 공유할 수 있습니다).
//   3) CutsceneZoneTrigger(또는 다른 트리거 - 퀘스트 진행 등에서 직접 CutsceneManager.Instance.Play()를
//      호출해도 됩니다)의 Cutscene Data 필드에 새로 만든 애셋을 연결하세요.
//   이 스크립트나 다른 코드를 전혀 건드리지 않고 새 컷씬을 계속 추가할 수 있습니다.
//
// [카메라 Priority]
//   ActivateCamera 스텝이 실행될 때마다 그 전에 활성화했던 카메라(있다면)는 idleCameraPriority로
//   내리고, 새로 지정한 카메라만 cutsceneCameraPriority로 올립니다 - 한 번에 하나만 활성화된다는
//   전제입니다. 컷씬의 모든 스텝이 끝나면 마지막으로 활성화되어 있던 카메라도 자동으로 내려서
//   게임플레이 카메라로 돌아갑니다.
//
// [카메라 준비]
//   Establishing Shot처럼 스스로 움직이는 카메라는 Body에 CinemachineSplineDolly를(Spline을 미리
//   그려두고 그 Spline을 연결 - Automatic Dolly로 속도를 지정하면 활성화되는 즉시 알아서 흘러갑니다),
//   플레이어를 따라가는 카메라는 Follow를 플레이어 Transform으로, 눈 마주침 클로즈업처럼 고정 앵글이
//   필요한 카메라는 Body/Aim을 Do Nothing으로 두고 원하는 위치/회전에 직접 배치하세요 - 이 스크립트는
//   Priority(그리고 아래 [Dolly 위치 초기화]에 설명한 CameraPosition)만 조절할 뿐 그 외 transform은
//   전혀 건드리지 않습니다(카메라 움직임/프레이밍은 전부 Cinemachine 자체 기능이나 씬에서의 배치로
//   해결합니다).
//
// [Dolly 위치 초기화]
//   ActivateCamera 스텝이 CinemachineSplineDolly가 붙어있는 카메라를 활성화시킬 때마다, 그
//   CameraPosition(Spline 위의 진행도)을 자동으로 0(경로의 맨 처음)으로 리셋합니다(ActivateCamera()
//   내부의 ResetSplineDollyPositionIfAny() 참고). 리셋해주지 않으면 에디터에서 미리 재생해봤거나
//   Automatic Dolly로 이미 끝까지 진행된 상태가 남아있어서, 다음에 이 카메라가 다시 활성화될 때
//   경로 중간/끝에서 시작해버리는 문제가 생깁니다 - 그래서 이 스크립트가 대신 항상 처음으로
//   되돌려주고, 애셋/씬 쪽에서는 따로 신경 쓸 필요가 없습니다.
//
// [Custom Blends 권장]
//   화면에 그대로 보이는 카메라 전환(예: 이동 카메라 → 클로즈업 카메라)은 UltSkillEffector.cs에
//   설명된 것과 같은 방식으로 CinemachineBlenderSettings(Custom Blends)에 원하는 블렌드를
//   등록해두는 걸 권장합니다. 화면이 까만 상태(FadeOut 도중)에서 전환되는 카메라는 블렌드가 안
//   보이므로 신경 쓰지 않아도 됩니다.
//
// [씬 준비]
//   1) 빈 오브젝트에 이 스크립트를 붙이세요. 씬에 정확히 하나만 있어야 합니다.
//   2) Cameras에 이 씬에서 쓸 카메라들을(Key + Camera) 하나씩 등록하세요.
//   3) Waypoint Groups에 플레이어가 자동으로 걸어갈 지점들을(Key + Waypoints) 등록하세요.
//   4) Npcs에 컷씬이 끝나고 바로 대화를 시작할 수 있는 NPCTalker들을(Key + Npc Talker) 등록하세요.
//   5) Teleport Points에 플레이어를 순간이동시킬 지점들을(Key + Point) 등록하세요 - 빈 오브젝트를
//      원하는 위치/회전에 놓아두고 그 Transform을 연결하면 됩니다.
//   6) SetLocationTitleVisible 스텝을 쓰려면 Location Title Canvas Group을 준비하세요 - Canvas
//      하위에 빈 오브젝트를 만들고 CanvasGroup을 붙인 뒤 Location Title Canvas Group 필드에
//      연결하고, 그 안에 Image를 만들어 Location Title Image 필드에 연결하세요(다른 UI보다 위에
//      그려지도록 Sort Order를 적당히 높게 잡으세요). 어떤 스프라이트를 보여줄지는 Image 쪽에 미리
//      정해두는 게 아니라 각 CutsceneData 스텝의 Location Title Sprite 필드에서 그때그때 지정합니다
//      (지역마다 다른 로고를 같은 Image 하나로 재사용). 이 스크립트는 CanvasGroup의 알파만 조절하므로
//      Image 하위에 다른 장식(배경 등)을 더 넣어도 함께 페이드인/아웃됩니다.
// ============================================================================

using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;

public class CutsceneManager : MonoBehaviour
{
    [Serializable]
    public class CameraEntry
    {
        public string key;
        public CinemachineCamera camera;
    }

    [Serializable]
    public class WaypointGroupEntry
    {
        public string key;
        public Transform[] waypoints;
    }

    [Serializable]
    public class NpcEntry
    {
        public string key;
        public NPCTalker npcTalker;
    }

    [Serializable]
    public class TeleportPointEntry
    {
        public string key;
        public Transform point;
    }

    /// <summary>씬에 하나만 있는 컴포넌트라, 다른 스크립트(CutsceneZoneTrigger 등)에서 여기로 바로 접근합니다.</summary>
    public static CutsceneManager Instance { get; private set; }

    /// <summary>지금 씬 어딘가에서 컷씬이 재생 중인지 여부입니다. UICanvas.IsUIOpen이 이 값을 함께
    /// 확인해서, 컷씬 중에는 인벤토리 등 다른 UI를 열거나 Escape로 끼어들 수 없게 막습니다
    /// (TalkManager.IsTalking과 같은 방식 - 플레이어 자신의 입력 차단은 PlayerController.
    /// BeginCutsceneControl()이 따로 처리합니다).</summary>
    public static bool IsAnyCutscenePlaying { get; private set; }

    [Header("카메라 Priority")]
    [Tooltip("컷씬 중 활성 카메라에게 부여할 Priority입니다. 게임플레이 카메라, TalkManager " +
              "(talkingCameraPriority)보다도 높아야 합니다.")]
    public int cutsceneCameraPriority = 30;
    [Tooltip("컷씬 중이 아닐 때(그리고 시작 전) 등록된 카메라들의 Priority입니다. 게임플레이 카메라의 " +
              "Priority보다 반드시 낮아야 합니다(동률이면 Cinemachine이 전환하지 않습니다).")]
    public int idleCameraPriority = -10;

    [Header("등록 - 카메라 (CutsceneData.Step.cameraKey가 이 Key를 참조합니다)")]
    public CameraEntry[] cameras = new CameraEntry[0];
    [Header("등록 - 웨이포인트 그룹 (CutsceneData.Step.waypointGroupKey가 이 Key를 참조합니다)")]
    public WaypointGroupEntry[] waypointGroups = new WaypointGroupEntry[0];
    [Header("등록 - NPC (CutsceneData.Step.npcKey가 이 Key를 참조합니다)")]
    public NpcEntry[] npcs = new NpcEntry[0];
    [Header("등록 - 순간이동 지점 (CutsceneData.Step.teleportPointKey가 이 Key를 참조합니다)")]
    public TeleportPointEntry[] teleportPoints = new TeleportPointEntry[0];

    [Header("지역 이름 타이틀 (SetLocationTitleVisible 전용)")]
    [Tooltip("화면에 지역 이름을 잠깐 띄우는 타이틀 카드의 CanvasGroup입니다 - SetLocationTitleVisible " +
              "스텝이 이 알파를 0(안 보임)~1(다 보임) 사이로 페이드시킵니다. 파일 상단 [씬 준비] 6번 참고.")]
    public CanvasGroup locationTitleCanvasGroup;
    [Tooltip("지역 이름 로고/타이틀 이미지를 표시할 Image입니다 - locationTitleCanvasGroup 하위에 두세요. " +
              "각 컷씬 스텝(CutsceneData.Step.locationTitleSprite)에서 지정한 스프라이트로 매번 바뀝니다. " +
              "비워두면 이미지 없이 CanvasGroup 안의 다른 내용만 페이드됩니다.")]
    public Image locationTitleImage;

    private bool isPlaying;
    private CinemachineCamera activeCamera;
    private Coroutine activeWalkCoroutine;
    private Tween locationTitleFadeTween;

    private void Awake()
    {
        Instance = this;

        foreach (CameraEntry entry in cameras)
        {
            if (entry.camera != null) entry.camera.Priority = idleCameraPriority;
        }

        if (locationTitleCanvasGroup != null)
        {
            locationTitleCanvasGroup.alpha = 0f;
            locationTitleCanvasGroup.interactable = false;
            locationTitleCanvasGroup.blocksRaycasts = false;
        }
    }

    /// <summary>data를 처음부터 재생합니다. data가 비어있거나(steps 없음), 이미 다른 컷씬이 재생
    /// 중이거나, PlayerController/GameManager를 찾지 못하면(씬 준비가 안 된 상태) 경고만 남기고
    /// 아무 것도 하지 않습니다.</summary>
    public void Play(CutsceneData data)
    {
        if (isPlaying) return;

        if (data == null || data.steps == null || data.steps.Length == 0)
        {
            Debug.LogWarning("[CutsceneManager] steps가 비어있는 CutsceneData는 재생할 수 없습니다.", data);
            return;
        }

        PlayerController player = ResolvePlayerController();
        if (player == null)
        {
            Debug.LogWarning("[CutsceneManager] PlayerController를 찾지 못해 컷씬을 재생할 수 없습니다.", this);
            return;
        }

        if (GameManager.Instance == null)
        {
            Debug.LogWarning("[CutsceneManager] GameManager를 찾지 못해 컷씬을 재생할 수 없습니다(화면 페이드를 담당합니다).", this);
            return;
        }

        StartCoroutine(PlayRoutine(data, player));
    }

    private static PlayerController ResolvePlayerController()
    {
        if (PlayerStats.Instance == null) return null;
        return PlayerStats.Instance.GetComponent<PlayerController>();
    }

    private IEnumerator PlayRoutine(CutsceneData data, PlayerController player)
    {
        isPlaying = true;
        IsAnyCutscenePlaying = true;

        // 트리거되는 순간 즉시 조작을 넘겨받습니다(TalkManager 등과 같은 인터럽트 세이프티넷 타이밍) -
        // 첫 스텝(보통 FadeOut)이 끝나기 전까지 남아있던 이동 입력 등이 어색하게 섞여 들어가지
        // 않도록, 실제 스텝 실행보다 앞서 조작을 끊어둡니다.
        player.BeginCutsceneControl();
        player.CutsceneMove(Vector3.zero);

        foreach (CutsceneData.Step step in data.steps)
        {
            yield return RunStep(step, player);
        }

        if (activeCamera != null)
        {
            activeCamera.Priority = idleCameraPriority;
            activeCamera = null;
        }

        // WalkPlayerToWaypoints는 더 이상 도착할 때까지 기다리지 않고(아래 StartWalkingToWaypoints 참고)
        // 백그라운드로 계속 걸어가므로, 컷씬이 끝나는 시점에 아직 도착하지 못한 채 남아있을 수 있습니다 -
        // 그대로 두면 EndCutsceneControl() 이후에도(IsCutsceneControlled가 꺼져 CutsceneMove()는
        // 조용히 무시되지만) 이 코루틴 자체는 도착 판정을 하지 못해 영원히 계속 돌아가므로, 여기서
        // 확실하게 멈춰줍니다.
        if (activeWalkCoroutine != null)
        {
            StopCoroutine(activeWalkCoroutine);
            activeWalkCoroutine = null;
        }

        player.EndCutsceneControl();

        isPlaying = false;
        IsAnyCutscenePlaying = false;
    }

    private IEnumerator RunStep(CutsceneData.Step step, PlayerController player)
    {
        switch (step.type)
        {
            case CutsceneData.StepType.FadeOut:
                yield return GameManager.Instance.FadeOut(step.duration).WaitForCompletion();
                break;

            case CutsceneData.StepType.FadeIn:
                yield return GameManager.Instance.FadeIn(step.duration).WaitForCompletion();
                break;

            case CutsceneData.StepType.Wait:
                yield return new WaitForSeconds(step.duration);
                break;

            case CutsceneData.StepType.SetHudVisible:
                UICanvas.Instance?.Ingame.SetVisible(step.hudVisible);
                break;

            case CutsceneData.StepType.ActivateCamera:
                ActivateCamera(step.cameraKey);
                break;

            case CutsceneData.StepType.WalkPlayerToWaypoints:
                StartWalkingToWaypoints(step, player);
                break;

            case CutsceneData.StepType.StartDialogue:
                NPCTalker npc = FindNpc(step.npcKey);
                if (npc != null)
                {
                    npc.Interact(player.gameObject);
                }
                else
                {
                    Debug.LogWarning($"[CutsceneManager] NPC 키 '{step.npcKey}'를 찾지 못했습니다 - Npcs 리스트에 등록되어 있는지 확인하세요.", this);
                }
                break;

            case CutsceneData.StepType.TeleportPlayer:
                TeleportPlayer(step, player);
                break;

            case CutsceneData.StepType.FacePlayerAndNpc:
                yield return FacePlayerAndNpc(step, player);
                break;

            case CutsceneData.StepType.SetLocationTitleVisible:
                yield return SetLocationTitleVisible(step);
                break;
        }
    }

    /// <summary>화면에 지역 이름 로고/타이틀 이미지를 잠깐 띄우는 카드(locationTitleCanvasGroup)를 켜거나
    /// 끕니다. 보여줄 때는 step.locationTitleSprite를 locationTitleImage에 반영한 뒤 페이드인합니다.
    /// GameManager.FadeOut/FadeIn과 같은 방식(CanvasGroup + DOTween, .SetUpdate(true)라 다른 팝업이
    /// Time.timeScale을 0으로 만들어도 계속 페이드됩니다)이고, FadeOut/FadeIn 스텝과 같은 "duration초
    /// 동안 대기" 패턴이라 완전히 페이드인/아웃될 때까지 다음 스텝으로 넘어가지 않습니다.
    /// locationTitleCanvasGroup이 연결되어 있지 않으면 경고만 남기고 아무 것도 하지 않습니다.</summary>
    private IEnumerator SetLocationTitleVisible(CutsceneData.Step step)
    {
        if (locationTitleCanvasGroup == null)
        {
            Debug.LogWarning("[CutsceneManager] Location Title Canvas Group이 연결되어 있지 않아 지역 이름 타이틀을 표시할 수 없습니다.", this);
            yield break;
        }

        locationTitleFadeTween?.Kill();

        if (step.showLocationTitle)
        {
            if (locationTitleImage != null) locationTitleImage.sprite = step.locationTitleSprite;
            locationTitleFadeTween = locationTitleCanvasGroup.DOFade(1f, step.duration).SetUpdate(true);
        }
        else
        {
            locationTitleFadeTween = locationTitleCanvasGroup.DOFade(0f, step.duration).SetUpdate(true);
        }

        yield return locationTitleFadeTween.WaitForCompletion();
    }

    /// <summary>플레이어를 teleportPointKey로 등록된 위치/회전으로 즉시 순간이동시킵니다(시간이 걸리지
    /// 않는 즉시 스텝이라 yield 없이 바로 다음 스텝으로 넘어갑니다). 화면이 까맣게 가려진 FadeOut ~
    /// FadeIn 사이에 넣어서 쓰세요 - 실제 이동/회전은 PlayerController.TeleportTo()가 처리합니다
    /// (CharacterController를 잠깐 꺼서 충돌 판정 없이 순간이동시키는 방식 - PlayerController.cs 참고).</summary>
    private void TeleportPlayer(CutsceneData.Step step, PlayerController player)
    {
        Transform point = FindTeleportPoint(step.teleportPointKey);
        if (point == null)
        {
            Debug.LogWarning($"[CutsceneManager] 순간이동 위치 키 '{step.teleportPointKey}'를 찾지 못했습니다 - Teleport Points 리스트에 등록되어 있는지 확인하세요.", this);
            return;
        }

        player.TeleportTo(point.position, point.rotation);
    }

    /// <summary>npcKey로 등록된 NPC와 플레이어가 서로를 바라보게(눈 마주침) 돌려세웁니다. step.duration이
    /// 0이면 그 자리에서 즉시(스냅) 마주보고 이 스텝은 곧바로 끝나며, 0보다 크면 그 시간(초) 동안
    /// 매 프레임 서로를 향해 부드럽게 돌아가는 걸 기다립니다(FadeOut/FadeIn과 같은 "duration만큼
    /// 대기" 패턴). NPC 쪽은 NPCTalker.CutsceneSetFaceTarget()을 한 번만 호출하면 그 뒤로는
    /// NPCTalker 자신의 Update()가 알아서 계속 회전을 진행하고, 플레이어 쪽은 매 프레임
    /// PlayerController.CutsceneFaceTowards()를 직접 호출해줘야 합니다(PlayerController는 컷씬
    /// 중 스스로 갱신되는 Update() 루프가 없기 때문입니다 - CutsceneMove()와 같은 이유). 마지막에는
    /// 정확히 정렬되도록 양쪽 다 Instant로 한 번 더 스냅해서 끝냅니다.</summary>
    private IEnumerator FacePlayerAndNpc(CutsceneData.Step step, PlayerController player)
    {
        NPCTalker npc = FindNpc(step.npcKey);
        if (npc == null)
        {
            Debug.LogWarning($"[CutsceneManager] NPC 키 '{step.npcKey}'를 찾지 못했습니다 - Npcs 리스트에 등록되어 있는지 확인하세요.", this);
            yield break;
        }

        bool instant = step.duration <= 0f;
        npc.CutsceneSetFaceTarget(player.transform.position, instant);

        if (!instant)
        {
            float timer = 0f;
            while (timer < step.duration)
            {
                player.CutsceneFaceTowards(npc.transform.position);
                timer += Time.deltaTime;
                yield return null;
            }
        }

        // 마무리 스냅: SmoothDampAngle 특성상 부드러운 회전은 duration이 끝나도 목표에 완전히 딱
        // 맞아떨어지지 않을 수 있어서, 눈 마주침 연출의 정확성을 위해 마지막에 확실히 정렬합니다.
        player.CutsceneFaceTowardsInstant(npc.transform.position);
        npc.CutsceneSetFaceTarget(player.transform.position, true);
    }

    private void ActivateCamera(string key)
    {
        CinemachineCamera cam = FindCamera(key);
        if (cam == null)
        {
            Debug.LogWarning($"[CutsceneManager] 카메라 키 '{key}'를 찾지 못했습니다 - Cameras 리스트에 등록되어 있는지 확인하세요.", this);
            return;
        }

        if (activeCamera != null) activeCamera.Priority = idleCameraPriority;

        ResetSplineDollyPositionIfAny(cam);

        cam.Priority = cutsceneCameraPriority;
        activeCamera = cam;
    }

    /// <summary>cam의 Body가 CinemachineSplineDolly(Establishing Shot처럼 Spline을 따라 카메라 스스로
    /// 움직이는 방식)라면, 활성화되는 시점마다 Camera Position을 0(경로의 맨 처음)으로 되돌립니다.
    /// 리셋하지 않으면 이전 재생에서(또는 에디터에서 테스트하며 Automatic Dolly로 이미 진행된 상태)
    /// 남아있던 위치에서 이어서 움직여버려, 컷씬을 다시 재생해도 카메라가 경로 중간이나 끝에서
    /// 시작하는 이상한 상황이 생깁니다. Dolly가 아닌(Do Nothing/Position Composer 등) 카메라는 이
    /// 컴포넌트가 아예 없으니 조용히 아무 것도 하지 않고 넘어갑니다.</summary>
    private void ResetSplineDollyPositionIfAny(CinemachineCamera cam)
    {
        CinemachineSplineDolly dolly = cam.GetComponent<CinemachineSplineDolly>();
        if (dolly != null) dolly.CameraPosition = 0f;
    }

    /// <summary>WalkPlayerToWaypoints 스텝은 더 이상 도착할 때까지 다음 스텝을 막지 않습니다 - 걷기
    /// 시작만 시키고(백그라운드 코루틴으로 계속 진행) RunStep은 그 즉시(yield 없이) 다음 스텝으로
    /// 넘어갑니다. 그동안 걷는 시간을 다른 연출(카메라 전환, 대사 등)과 맞추고 싶으면 뒤에 Wait
    /// 스텝을 넣어 직접 타이밍을 맞추세요 - 예: WalkPlayerToWaypoints → Wait(2) → ActivateCamera(...).
    /// 만약 이전에 시작해서 아직 걷고 있던 코루틴이 있으면(같은 컷씬에 WalkPlayerToWaypoints가 두 번
    /// 이상 있는데 충분히 기다리지 않고 다음 걸 시작한 경우) 먼저 멈추고 새로 시작합니다 - 두 걷기가
    /// 동시에 플레이어를 서로 다른 방향으로 이끄는 충돌을 막기 위해서입니다.</summary>
    private void StartWalkingToWaypoints(CutsceneData.Step step, PlayerController player)
    {
        if (activeWalkCoroutine != null) StopCoroutine(activeWalkCoroutine);
        activeWalkCoroutine = StartCoroutine(WalkPlayerThroughWaypoints(step, player));
    }

    /// <summary>웨이포인트 그룹을 순서대로(비어있는 슬롯은 건너뜁니다) 플레이어가 자동으로 걸어가게
    /// 합니다. 매 프레임 PlayerController.CutsceneMove()를 호출해서 실제 걷는 애니메이션/이동이
    /// 자연스럽게 재생됩니다 - 걷는 속도는 PlayerController.cutsceneWalkSpeed(인스펙터에서 조절)를
    /// 그대로 사용하고, 애니메이터는 IsMove가 아니라 IsWalk bool로 걷기 모션을 재생합니다(둘 다
    /// CutsceneMove() 안에서 처리되므로 이 스크립트는 신경 쓸 필요 없습니다). StartWalkingToWaypoints()가
    /// StartCoroutine()으로 백그라운드에 띄우는 코루틴이라, 이 함수 자체는 도착할 때까지 아무도
    /// 기다리지 않습니다(RunStep이 바로 다음 스텝으로 넘어갑니다).</summary>
    private IEnumerator WalkPlayerThroughWaypoints(CutsceneData.Step step, PlayerController player)
    {
        Transform[] waypoints = FindWaypointGroup(step.waypointGroupKey);
        if (waypoints == null)
        {
            Debug.LogWarning($"[CutsceneManager] 웨이포인트 그룹 키 '{step.waypointGroupKey}'를 찾지 못했습니다 - Waypoint Groups 리스트에 등록되어 있는지 확인하세요.", this);
            yield break;
        }

        foreach (Transform waypoint in waypoints)
        {
            if (waypoint == null) continue;

            while (true)
            {
                Vector3 toWaypoint = waypoint.position - player.transform.position;
                toWaypoint.y = 0f;

                if (toWaypoint.magnitude <= step.arriveDistance) break;

                player.CutsceneMove(toWaypoint);
                yield return null;
            }
        }

        player.CutsceneMove(Vector3.zero); // 다 걸었으면 제자리에 멈춰 Idle로 표시합니다.
    }

    private CinemachineCamera FindCamera(string key)
    {
        foreach (CameraEntry entry in cameras)
        {
            if (entry.key == key) return entry.camera;
        }
        return null;
    }

    private Transform[] FindWaypointGroup(string key)
    {
        foreach (WaypointGroupEntry entry in waypointGroups)
        {
            if (entry.key == key) return entry.waypoints;
        }
        return null;
    }

    private NPCTalker FindNpc(string key)
    {
        foreach (NpcEntry entry in npcs)
        {
            if (entry.key == key) return entry.npcTalker;
        }
        return null;
    }

    private Transform FindTeleportPoint(string key)
    {
        foreach (TeleportPointEntry entry in teleportPoints)
        {
            if (entry.key == key) return entry.point;
        }
        return null;
    }
}