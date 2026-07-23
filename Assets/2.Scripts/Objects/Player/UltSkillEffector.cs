// ============================================================================
// UltSkillEffector.cs
// ----------------------------------------------------------------------------
// 주인공이 필살기(UltSkill, Q)를 사용할 때 재생되는 카메라 연출을 담당합니다. TalkManager의
// dialogueCamera와 완전히 같은 방식입니다 - Follow/LookAt 없이 이 스크립트가 직접 transform을
// 옮기는 전용 Cinemachine 카메라 3대(정면샷/뒤로 멀어지는 샷/뒤쪽 시점)를 두고, Priority를 올려서
// Cinemachine이 자동으로 그 카메라로 전환하게 만듭니다.
//
// [연출 흐름]
//   1) PlayerController.HandleSkills()가 필살기를 시전하는 순간 PlayFaceShot()을 호출합니다 -
//      faceCam을 anchor 기준 위치로 즉시 옮기고 Priority를 올려서 상반신만 보이는 얼굴 정면샷으로
//      전환합니다.
//   2) UltSkill 애니메이션 클립에 걸어둔 Animation Event(PlayerController.OnUltCameraPullBack())가
//      호출되면 PullBack()이 실행됩니다 - pullBackCam을 anchor 기준 위치로 옮기고 Priority를 올리는
//      동시에 faceCam의 Priority는 내려서, 카메라가 부드럽게 뒤로 멀어지며 주변 풍경까지 보이는
//      샷으로 전환합니다.
//   3) 이어서 또 다른 Animation Event(PlayerController.OnUltCameraSwitchToBack())가 호출되면
//      SwitchToBackShot()이 실행됩니다 - backCam을 anchor 기준 위치로 옮기고 Priority를 올리는
//      동시에 pullBackCam의 Priority는 내려서, 등 뒤 어깨너머로 내려찍는 시점으로 전환합니다.
//   4) 필살기 모션이 끝나면(PlayerController가 isUsingSkill을 끄는 모든 경로 - 정상 종료, 피격,
//      사망 등 - 에서) EndSequence()가 호출되어 세 카메라 모두 Priority를 낮춰서 게임플레이
//      카메라로 돌아갑니다.
//
// [전환마다 다른 블렌드는 Custom Blends로 처리]
//   처음엔 이 스크립트가 CinemachineBrain.DefaultBlend를 런타임에 직접 조작해서(Cut ↔ 부드러운
//   블렌드를 전환마다 토글) 각 전환의 블렌드를 다르게 주려고 했습니다. 하지만 실전 테스트에서 문제가
//   확인됐습니다:
//     - DefaultBlend에 그냥 값을 대입하기만 하면 Brain이 "이전 블렌드 시간"을 캐시해서 새 값이 이번
//       전환에 반영되지 않는 경우가 있었고(Unity Discussions에 보고된 quirk),
//     - 이를 우회하려고 대입 전후로 Brain을 잠깐 껐다 켜는 워크어라운드를 썼더니, 이번엔 Brain이
//       재활성화되면서 내부적으로 추적하던 "블렌드 진행 상태"가 초기화되어, 오히려 다음 전환이 블렌드
//       없이 그냥 Cut처럼 튀어버리는 부작용이 생겼습니다.
//   그래서 런타임에 Brain을 직접 조작하는 방식은 완전히 걷어냈습니다. 대신 Cinemachine이 정확히 이런
//   용도로 제공하는 "Custom Blends"(CinemachineBlenderSettings 에셋)를 씁니다 - 특정 카메라 쌍
//   (From/To)에 대해서만 다른 블렌드를 선언적으로 등록해두는 기능이라, Brain의 내부 상태를 전혀
//   건드리지 않고 원하는 전환들만 정확히 다르게 처리할 수 있습니다. 등록 방법은 아래 [씬 준비] 4번
//   항목을 참고하세요. 이 스크립트는 Priority와 transform만 다루고, Brain을 직접 참조하지도 않습니다.
//
// [카메라 좌표 - anchor 기준 상대값]
//   faceCamLocalPosition/EulerAngles, pullBackCamLocalPosition/EulerAngles,
//   backCamLocalPosition/EulerAngles는 월드 절대 좌표가 아니라 anchor Transform 기준 상대 위치/
//   회전입니다(TalkScript.Talks.cameraLocalPosition과 완전히 같은 방식) - 그래서 플레이어가 씬 어디에
//   있든, 어느 방향을 보고 있든 항상 같은 앵글로 재현됩니다. PlayerController.StartSkill()이 필살기
//   시작 시 FaceNearestTargetIfAny()로 먼저 회전을 끝내둔 뒤에 PlayFaceShot()이 호출되므로, 여기서
//   anchor.rotation을 읽는 시점엔 이미 올바른 방향을 보고 있습니다(NPCTalker가 회전을 먼저 끝낸 뒤
//   카메라를 계산하는 것과 같은 이유의 순서입니다).
//
// [Priority]
//   ultCameraPriority(기본 30)는 TalkManager.talkingCameraPriority(기본 20)보다도 높게 잡아뒀습니다 -
//   이론상 필살기 도중 대화가 겹칠 일은 없어야 하지만, 혹시 겹치더라도 이 연출이 확실히 위에
//   뜨도록 여유를 뒀습니다. idleCameraPriority(기본 -10)는 게임플레이 카메라(보통 Priority 0)보다
//   반드시 낮아야 합니다 - 동률이면 Cinemachine이 전환하지 않습니다(TalkManager.idleCameraPriority와
//   같은 이유).
//
// [씬 준비]
//   1) GameObject > Cinemachine > Camera로 카메라를 3개 만들고(예: "UltFaceCam", "UltPullBackCam",
//      "UltBackCam"), 각각 Body/Aim을 전부 "Do Nothing"으로 두세요(이 스크립트가 transform을 직접
//      옮깁니다). Face Cam / Pull Back Cam / Back Cam 필드에 각각 연결하세요.
//   2) Anchor에 주인공(Player) Transform을 연결하세요(비워두면 Start()에서 PlayerStats.Instance.transform을
//      자동으로 씁니다).
//   3) Play 모드에서 필살기를 써보면서 Face Cam Local Position/Euler Angles(정면에서 상반신을 보는
//      각도), Pull Back Cam Local Position/Euler Angles(뒤로 멀어져서 주변 풍경까지 보이는 각도),
//      Back Cam Local Position/Euler Angles(등 뒤 어깨 너머로 내려찍는 곳을 보는 각도) 값을 바꿔가며
//      원하는 앵글로 맞추세요. Pull Back Cam은 faceCam보다 anchor에서 훨씬 멀리 떨어뜨리고(또는 Lens
//      FOV를 넓혀서) 주변 풍경이 확실히 보이게 하세요.
//   4) [Custom Blends 등록 - 필수] Project 창에서 Create > Cinemachine > Blender Settings로 에셋을
//      하나 만드세요(예: "UltSkillBlends"). 그 에셋을 열어 Custom Blends 리스트에 아래 세 항목을
//      추가하세요:
//        - From: **ANY CAMERA**   →  To: UltFaceCam      →  Style: Cut,          Time: 0
//        - From: UltFaceCam       →  To: UltPullBackCam  →  Style: Ease In Out,  Time: 0.4~0.5 (뒤로
//          확 멀어지는 느낌이 나도록 두 번째 전환보다 살짝 여유 있게)
//        - From: UltPullBackCam   →  To: UltBackCam       →  Style: Ease In Out,  Time: 0.2 (빠르고
//          부드럽게)
//      그 다음 CinemachineBrain(보통 Main Camera에 붙어있음) 컴포넌트를 선택해서 Custom Blends
//      필드에 이 에셋을 드래그해서 연결하세요. 이 세 항목에 해당하지 않는 다른 모든 전환(예:
//      TalkManager의 대화 카메라, 필살기 종료 후 게임플레이 카메라로 복귀)은 그대로 Brain의
//      Default Blend(프로젝트 기본값)를 씁니다 - 이 스크립트가 Brain을 직접 건드리지 않으므로 다른
//      시스템에 영향을 줄 걱정은 없습니다.
//   5) UltSkill 애니메이션 클립에 Animation Event 두 개를 순서대로 추가하세요:
//        - OnUltCameraPullBack(): 정면샷이 잠깐 보여진 직후 프레임 (얼굴 정면샷 → 뒤로 멀어지는 샷)
//        - OnUltCameraSwitchToBack(): 그보다 뒤, 등을 돌려 무기를 들어올리는(실제 내려찍기보다 살짝
//          앞선) 프레임 (뒤로 멀어지는 샷 → 뒤쪽 시점)
//   6) [정면샷/뒤로 멀어지는 샷 동안 특정 레이어 가리기 - 선택] CmCamera(가상 카메라) 자체에는
//      Culling Mask가 없습니다 - 실제로 화면을 그리는 건 Brain이 붙어있는 진짜 Camera 하나뿐이라,
//      "이 가상 카메라일 때만 특정 레이어를 숨기기"는 가상 카메라별 설정으로는 할 수 없습니다. 그래서
//      이 스크립트가 faceCam/pullBackCam이 활성화되는 순간 실제 렌더링 Camera의 cullingMask에서
//      layersToHideDuringFaceShots를 빼고, backCam으로 넘어가거나(SwitchToBackShot) 연출이
//      끝나면(EndSequence) 원래 값으로 복원합니다. Output Camera 필드를 비워두면 Awake()에서
//      Camera.main을 자동으로 씁니다. Layers To Hide During Face Shots에 Monster 레이어를 체크해두면,
//      얼굴 정면샷/뒤로 멀어지는 샷 동안 몬스터가 화면에 끼어들어 컷신을 방해하지 않고, 실제 내려찍는
//      뒤쪽 시점(backCam)에서는 다시 정상적으로 보입니다.
//   7) [정면샷/뒤로 멀어지는 샷 동안 배경 오브젝트 보여주기 - 선택] 진짜 Unity Skybox(RenderSettings.
//      skybox, 무한히 먼 배경) 대신, 캐릭터를 감싸는 작은 돔/구체 오브젝트를 하나 만들어서 이 연출
//      동안만 켜는 방식을 씁니다:
//        a) 큰 구체(또는 반구) 메시를 만들고, 안쪽 면이 카메라에 보이도록 Cull Front(또는 노멀을
//           뒤집은 메시)로 된 Unlit/에미시브 머티리얼을 입히세요. faceCam/pullBackCam 두 카메라의
//           위치가 전부 이 구체 "안쪽"에 들어오도록 반지름을 충분히 크게 잡으세요(예: pullBackCam이
//           anchor에서 5 정도 떨어져 있다면 반지름은 최소 10~15 이상).
//        b) 이 오브젝트를 씬에 배치하고 비활성(Inactive) 상태로 두세요. UltSkillEffector의
//           Backdrop Object 필드에 연결하세요.
//        c) Backdrop Local Position(anchor 기준 상대 좌표, 보통 0 - 발밑)을 조절해서 캐릭터가 항상
//           구체 중심 부근에 오도록 맞추세요.
//      이 스크립트가 faceCam이 켜질 때(PlayFaceShot) backdropObject를 anchor 위치로 옮기고 활성화하고,
//      backCam으로 전환되거나(SwitchToBackShot) 연출이 끝나면(EndSequence) 다시 비활성화합니다 -
//      Layers To Hide During Face Shots와 같은 타이밍이라, 몬스터를 가리는 것과 배경을 보여주는 게
//      항상 같이 켜지고 같이 꺼집니다.
// ============================================================================

using UnityEngine;
using Unity.Cinemachine;

public class UltSkillEffector : MonoBehaviour
{
    /// <summary>씬에 하나만 있는 컴포넌트라, 다른 스크립트(PlayerController)에서 여기로 바로 접근합니다.</summary>
    public static UltSkillEffector Instance { get; private set; }

    [Header("전용 카메라 (Body/Aim을 Do Nothing으로 두세요)")]
    public CinemachineCamera faceCam;
    public CinemachineCamera pullBackCam;
    public CinemachineCamera backCam;

    [Header("기준점")]
    [Tooltip("카메라 좌표 계산의 기준이 되는 Transform입니다. 비워두면 Start()에서 PlayerStats.Instance.transform을 자동으로 씁니다.")]
    public Transform anchor;

    [Header("정면샷 (anchor 기준 상대 좌표)")]
    public Vector3 faceCamLocalPosition = new Vector3(0f, 1.6f, 0.8f);
    public Vector3 faceCamLocalEulerAngles = new Vector3(0f, 180f, 0f);

    [Header("뒤로 멀어지는 샷 (anchor 기준 상대 좌표, 주변 풍경이 보이도록 충분히 멀리/넓게)")]
    public Vector3 pullBackCamLocalPosition = new Vector3(0f, 3f, 4.5f);
    public Vector3 pullBackCamLocalEulerAngles = new Vector3(10f, 180f, 0f);

    [Header("뒤쪽 시점 (anchor 기준 상대 좌표)")]
    public Vector3 backCamLocalPosition = new Vector3(0f, 2.2f, -2.5f);
    public Vector3 backCamLocalEulerAngles = Vector3.zero;

    [Header("Priority")]
    [Tooltip("연출 중 활성 카메라(faceCam/pullBackCam/backCam 중 하나)에게 부여할 Priority입니다. " +
              "게임플레이 카메라(보통 0)와 TalkManager.talkingCameraPriority(기본 20)보다도 높아야 합니다.")]
    public int ultCameraPriority = 30;
    [Tooltip("연출 중이 아닐 때 세 카메라의 Priority입니다. 게임플레이 카메라의 Priority보다 반드시 " +
              "낮아야 합니다(동률이면 Cinemachine이 전환하지 않습니다 - TalkManager.idleCameraPriority와 " +
              "같은 이유).")]
    public int idleCameraPriority = -10;

    [Header("컷신 중 레이어 가리기 (선택)")]
    [Tooltip("실제로 화면을 그리는 진짜 Camera입니다. 비워두면 Awake()에서 Camera.main을 자동으로 씁니다.")]
    public Camera outputCamera;
    [Tooltip("faceCam/pullBackCam이 활성화되어 있는 동안(정면샷 ~ 뒤로 멀어지는 샷) 렌더링에서 제외할 " +
              "레이어입니다. 예: Monster - 컷신 중 몬스터가 화면에 끼어드는 걸 막습니다. backCam으로 " +
              "전환되면(내려찍기 연출) 자동으로 다시 보이게 복원됩니다. 비워두면(Nothing) 아무 레이어도 " +
              "가리지 않습니다.")]
    public LayerMask layersToHideDuringFaceShots;

    [Header("컷신 배경 오브젝트 (선택)")]
    [Tooltip("정면샷 ~ 뒤로 멀어지는 샷 동안 캐릭터 주변에 나타날 배경입니다(작은 돔/구체 등 - 안쪽 " +
              "면이 보이도록 Cull Front 머티리얼 추천). 씬에 미리 배치해두고 비활성 상태로 두세요 - 이 " +
              "스크립트가 필요할 때 anchor 위치로 옮기고 켜고 끕니다. faceCam/pullBackCam 3대 모두 이 " +
              "오브젝트 안쪽에 들어오도록 충분히 크게 만드세요. 비워두면 배경 연출 없이 레이어 가리기만 " +
              "동작합니다.")]
    public GameObject backdropObject;
    [Tooltip("backdropObject를 배치할 anchor 기준 상대 위치입니다. 보통 발밑 높이(0)나 살짝 아래로 잡아서 " +
              "돔이 캐릭터를 완전히 감싸게 하세요.")]
    public Vector3 backdropLocalPosition = Vector3.zero;

    private int originalCullingMask;

    // 레이어 가리기 + 배경 오브젝트가 지금 적용되어 있는지 여부입니다(둘은 항상 같이 켜지고 같이
    // 꺼지므로 하나의 플래그로 관리합니다). HideFaceShotDressing()/RestoreFaceShotDressing()이 중복
    // 적용/복원되지 않도록 막는 데 씁니다.
    private bool dressingActive;

    // 지금 연출이 재생 중인지 여부입니다. PullBack()/SwitchToBackShot()이 PlayFaceShot() 없이(연출이
    // 시작된 적 없는 상태에서) 잘못 호출되는 것을 막는 데 씁니다.
    private bool isSequenceActive;

    private void Awake()
    {
        Instance = this;

        if (outputCamera == null) outputCamera = Camera.main;
        if (outputCamera != null) originalCullingMask = outputCamera.cullingMask;
        if (backdropObject != null) backdropObject.SetActive(false);

        if (faceCam != null) faceCam.Priority = idleCameraPriority;
        if (pullBackCam != null) pullBackCam.Priority = idleCameraPriority;
        if (backCam != null) backCam.Priority = idleCameraPriority;
    }

    // PlayerStats.Instance 참조는 Awake()가 아니라 Start()에서 합니다 - 씬 로드 시점에 존재하는 모든
    // 오브젝트의 Awake()는 어떤 오브젝트의 Start()보다도 먼저 전부 끝나는 게 유니티가 보장하는
    // 순서라서, Start() 시점이면 PlayerStats.Instance가 이미 확실히 설정되어 있습니다.
    private void Start()
    {
        if (anchor == null && PlayerStats.Instance != null)
        {
            anchor = PlayerStats.Instance.transform;
        }
    }

    /// <summary>필살기 시전 순간 호출하세요(PlayerController.HandleSkills() 참고). faceCam을 anchor
    /// 기준 위치로 즉시 옮기고 Priority를 올려서 상반신만 보이는 얼굴 정면샷으로 전환합니다. 실제
    /// "즉시 컷"은 씬에 등록해둔 Custom Blends의 "**ANY CAMERA** → UltFaceCam : Cut" 항목이
    /// 처리합니다(파일 헤더 주석 참고). faceCam/anchor가 연결되어 있지 않으면(씬 준비가 안 된 상태)
    /// 아무 것도 하지 않습니다.</summary>
    public void PlayFaceShot()
    {
        if (faceCam == null || anchor == null) return;

        ApplyCameraTransform(faceCam, faceCamLocalPosition, faceCamLocalEulerAngles);

        // [순서 중요] TalkManager.StartTalk()과 같은 이유로, transform을 먼저 옮긴 뒤에 Priority를
        // 올립니다 - Priority를 먼저 올리면 Cinemachine이 전환을 감지하는 시점에 아직 옛 위치가
        // 남아있어 첫 컷이 어긋난 위치로 보일 수 있습니다.
        faceCam.Priority = ultCameraPriority;
        // 혹시 이전 연출이 덜 끝났다면 나머지 두 카메라를 확실히 내려둡니다.
        if (pullBackCam != null) pullBackCam.Priority = idleCameraPriority;
        if (backCam != null) backCam.Priority = idleCameraPriority;

        isSequenceActive = true;

        ApplyFaceShotDressing();
    }

    /// <summary>UltSkill 애니메이션 클립의 Animation Event(PlayerController.OnUltCameraPullBack())에서
    /// 호출됩니다. pullBackCam을 anchor 기준 위치로 옮기고 Priority를 올리는 동시에 faceCam의
    /// Priority를 내려서, 카메라가 부드럽게 뒤로 멀어지며 주변 풍경까지 보이는 샷으로 전환합니다.
    /// 이때 쓰이는 블렌드는 씬에 등록해둔 Custom Blends의 "UltFaceCam → UltPullBackCam"
    /// 항목입니다(파일 헤더 주석 참고). PlayFaceShot()이 아직 호출되지 않은 상태(연출이 시작된 적
    /// 없음)거나 pullBackCam/anchor가 비어있으면 아무 것도 하지 않습니다.</summary>
    public void PullBack()
    {
        if (!isSequenceActive) return;
        if (pullBackCam == null || anchor == null) return;

        ApplyCameraTransform(pullBackCam, pullBackCamLocalPosition, pullBackCamLocalEulerAngles);

        pullBackCam.Priority = ultCameraPriority;
        if (faceCam != null) faceCam.Priority = idleCameraPriority;
    }

    /// <summary>UltSkill 애니메이션 클립의 Animation Event(PlayerController.OnUltCameraSwitchToBack())에서
    /// 호출됩니다. backCam을 anchor 기준 위치로 옮기고 Priority를 올리는 동시에 pullBackCam(혹시 아직
    /// PullBack()이 호출되지 않은 예외적인 경우를 대비해 faceCam도 함께)의 Priority를 내려서,
    /// Cinemachine이 자동으로 뒤쪽 시점으로 블렌드하게 합니다. 이때 쓰이는 블렌드는 씬에 등록해둔
    /// Custom Blends의 "UltPullBackCam → UltBackCam" 항목입니다(파일 헤더 주석 참고). PlayFaceShot()이
    /// 아직 호출되지 않은 상태(연출이 시작된 적 없음)거나 backCam/anchor가 비어있으면 아무 것도
    /// 하지 않습니다.</summary>
    public void SwitchToBackShot()
    {
        if (!isSequenceActive) return;
        if (backCam == null || anchor == null) return;

        ApplyCameraTransform(backCam, backCamLocalPosition, backCamLocalEulerAngles);

        backCam.Priority = ultCameraPriority;
        if (pullBackCam != null) pullBackCam.Priority = idleCameraPriority;
        if (faceCam != null) faceCam.Priority = idleCameraPriority;

        RestoreFaceShotDressing();
    }

    /// <summary>필살기 모션이 끝나거나(정상 종료) 피격/사망 등으로 중간에 끊길 때 호출하세요
    /// (PlayerController.EndUltCameraIfActive()가 isUsingSkill을 끄는 모든 경로에서 호출합니다).
    /// 세 카메라 모두 Priority를 내려서 게임플레이 카메라로 돌아가게 합니다.</summary>
    public void EndSequence()
    {
        if (faceCam != null) faceCam.Priority = idleCameraPriority;
        if (pullBackCam != null) pullBackCam.Priority = idleCameraPriority;
        if (backCam != null) backCam.Priority = idleCameraPriority;

        isSequenceActive = false;

        // 안전장치: SwitchToBackShot()이 아직 호출되지 않은 채로(예: 정면샷/뒤로 멀어지는 샷 도중
        // 피격/사망) 연출이 중간에 끊겼다면 레이어/배경 오브젝트가 계속 켜진 채로 남아있을 수 있기
        // 때문입니다.
        RestoreFaceShotDressing();
    }

    /// <summary>faceCam/pullBackCam이 활성화되는 순간 호출해서 layersToHideDuringFaceShots를
    /// 렌더링에서 제외하고, backdropObject를 anchor 위치로 옮겨 활성화합니다. outputCamera/
    /// backdropObject 각각 없으면(씬 준비가 안 된 상태) 그 부분만 건너뜁니다.</summary>
    private void ApplyFaceShotDressing()
    {
        if (dressingActive) return;

        if (outputCamera != null)
        {
            outputCamera.cullingMask &= ~layersToHideDuringFaceShots.value;
        }
        if (backdropObject != null && anchor != null)
        {
            backdropObject.transform.position = anchor.TransformPoint(backdropLocalPosition);
            backdropObject.SetActive(true);
        }

        dressingActive = true;
    }

    /// <summary>ApplyFaceShotDressing()으로 가렸던 레이어와 켰던 backdropObject를 원래대로
    /// 복원합니다(SwitchToBackShot()/EndSequence()에서 호출). 애초에 적용된 적이 없으면 아무 것도
    /// 하지 않습니다.</summary>
    private void RestoreFaceShotDressing()
    {
        if (!dressingActive) return;

        if (outputCamera != null)
        {
            outputCamera.cullingMask = originalCullingMask;
        }
        if (backdropObject != null)
        {
            backdropObject.SetActive(false);
        }
        dressingActive = false;
    }

    /// <summary>anchor 기준 상대 좌표를 월드 좌표로 변환해서 cam에 블렌드 없이 즉시 적용합니다
    /// (TalkManager.ApplyCamera와 같은 방식).</summary>
    private void ApplyCameraTransform(CinemachineCamera cam, Vector3 localPosition, Vector3 localEulerAngles)
    {
        Vector3 worldPosition = anchor.TransformPoint(localPosition);
        Quaternion worldRotation = anchor.rotation * Quaternion.Euler(localEulerAngles);
        cam.transform.SetPositionAndRotation(worldPosition, worldRotation);
    }
}