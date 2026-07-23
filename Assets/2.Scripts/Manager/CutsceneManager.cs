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
//   Establishing Shot처럼 스스로 움직이는 카메라는 Body에 Cinemachine Dolly Cart를, 플레이어를
//   따라가는 카메라는 Follow를 플레이어 Transform으로, 눈 마주침 클로즈업처럼 고정 앵글이 필요한
//   카메라는 Body/Aim을 Do Nothing으로 두고 원하는 위치/회전에 직접 배치하세요 - 이 스크립트는
//   Priority만 조절할 뿐 transform은 전혀 건드리지 않습니다(카메라 움직임/프레이밍은 전부 Cinemachine
//   자체 기능이나 씬에서의 배치로 해결합니다).
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
// ============================================================================

using DG.Tweening;
using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

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

    private bool isPlaying;
    private CinemachineCamera activeCamera;

    private void Awake()
    {
        Instance = this;

        foreach (CameraEntry entry in cameras)
        {
            if (entry.camera != null) entry.camera.Priority = idleCameraPriority;
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
                yield return WalkPlayerThroughWaypoints(step, player);
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
        }
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

    private void ActivateCamera(string key)
    {
        CinemachineCamera cam = FindCamera(key);
        if (cam == null)
        {
            Debug.LogWarning($"[CutsceneManager] 카메라 키 '{key}'를 찾지 못했습니다 - Cameras 리스트에 등록되어 있는지 확인하세요.", this);
            return;
        }

        if (activeCamera != null) activeCamera.Priority = idleCameraPriority;
        cam.Priority = cutsceneCameraPriority;
        activeCamera = cam;
    }

    /// <summary>웨이포인트 그룹을 순서대로(비어있는 슬롯은 건너뜁니다) 플레이어가 자동으로 걸어가게
    /// 합니다. 매 프레임 PlayerController.CutsceneMove()를 호출해서 실제 걷는 애니메이션/이동이
    /// 자연스럽게 재생됩니다 - 걷는 속도는 PlayerController.cutsceneWalkSpeed(인스펙터에서 조절)를
    /// 그대로 사용하고, 애니메이터는 IsMove가 아니라 IsWalk bool로 걷기 모션을 재생합니다(둘 다
    /// CutsceneMove() 안에서 처리되므로 이 스크립트는 신경 쓸 필요 없습니다).</summary>
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