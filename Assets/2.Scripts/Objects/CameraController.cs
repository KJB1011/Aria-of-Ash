// ============================================================================
// CameraController.cs  (Cinemachine 3.1 버전)
// ----------------------------------------------------------------------------
// 원신 같은 3인칭 오빗 카메라 컨트롤러
// 기준: Cinemachine 3.1.x + 새 Input System 패키지
//
// [씬 준비 - 3단계만 하면 됩니다]
//   1) GameObject > Cinemachine > Camera 로 카메라를 하나 만듭니다.
//   2) 인스펙터에서 카메라의 Body(자세 조절) 항목을 "Orbital Follow"로 선택합니다.
//      - Tracking Target: 캐릭터(또는 캐릭터 어깨 높이의 빈 오브젝트)를 연결
//      - Orbit Style: "Three Ring"을 선택하면 원신처럼 위/중간/아래 링을 따로 조절 가능
//      - Radial Axis 항목의 Range를 예) Min 0.6 / Max 1.8 정도로 넉넉하게 넓혀주세요
//        (기본값은 범위가 좁아서 마우스 휠 줌이 거의 안 움직이는 것처럼 보일 수 있어요)
//   3) 빈 오브젝트를 하나 만들고(예: "CameraController") 이 스크립트를 붙인 뒤
//      Orbital Follow 필드에 1)에서 만든 카메라를 드래그해서 연결합니다.
//
// 이제 플레이를 누르면:
//   - 마우스를 움직이면 카메라가 캐릭터 주위를 회전 (목표 각도까지 부드럽게 전환, lookSmoothTime으로
//     속도 조절 - 줌과 완전히 같은 방식입니다)
//   - 마우스 휠로 줌 인/아웃 (목표 값까지 부드럽게 전환, zoomSmoothTime으로 속도 조절)
//     단, 상호작용 가능한 대상이 2개 이상 범위 안에 있어서 InteractionDetector가 휠을 상호작용
//     목록 선택에 쓰고 있는 동안에는(IsCyclingActive) 그 프레임의 줌 입력을 건너뜁니다.
//   - Alt(왼쪽/오른쪽 둘 다) 또는 Esc 키로 마우스 커서 잠금/해제 - 토글 방식이라, 한 번 누르면
//     풀려서 마우스가 자유롭게 움직이고(카메라도 같이 안 돌아감), 그 상태에서 다시 누르면 원래대로
//     잠깁니다.
//
// [UI가 열려있을 때(커서가 풀려있을 때)]
//   HandleLook()(마우스 회전)뿐 아니라 HandleZoom()(마우스 휠 줌)도 Cursor.lockState가
//   Locked가 아니면 그 프레임은 아무 것도 하지 않습니다 - 인벤토리/캐릭터정보/옵션 등 UI가
//   열리면 그 UI가 Cursor.lockState를 None으로 풀어두기 때문에, 이 체크 하나로 모든 UI가
//   열려있는 동안의 회전/줌을 한 번에 막을 수 있습니다. 이게 없으면 인벤토리의 스크롤 뷰나
//   스킬 트리 위에서 휠을 굴렸을 때 UI 스크롤과 카메라 줌이 동시에 반응해버립니다.
//
// [UI가 열려있는 동안엔 Alt 커서 토글도 막습니다]
//   OnToggleCursor()는 UICanvas.Instance.IsUIOpen(인벤토리/캐릭터정보/옵션/퀘스트 팝업 +
//   UINotice/UIYesNo/UITrash/UIExit + 대화/컷씬까지 전부 포함)이 true이면 Alt 입력 자체를
//   무시합니다 - 이 가드가 없으면, UI가 열려서 이미 커서가 풀려있는(None) 상태에서 Alt를 누르는
//   순간 "지금 Locked가 아니니 잠근다"는 토글 로직 때문에 커서가 갑자기 잠기고(보이지 않게 되고)
//   더 이상 그 UI를 마우스로 클릭할 수 없게 되는 문제가 있었습니다. 각 UI는 자기가 열릴 때/닫힐 때
//   커서 상태를 알아서 저장/복원하므로(UIInventory.Open()/Close() 등 참고), 열려있는 동안은
//   이 스크립트가 끼어들 필요가 없습니다.
//
// [lockCursorOnStart - 씬 시작과 동시에 여는 UI가 있다면]
//   Start()에서 UICanvas.Instance.IsUIOpen을 먼저 확인한 뒤에만 커서를 잠급니다. UIControls(조작법
//   안내 패널)처럼 씬이 시작되자마자 스스로 열리는 UI가 있는 경우, 그 UI의 Start()가 이 스크립트의
//   Start()보다 먼저 실행되면(유니티는 서로 다른 스크립트의 Start() 순서를 보장하지 않습니다)
//   이미 커서가 풀려있는 상태이므로 다시 잠그지 않습니다 - 안 그러면 "창은 정상적으로 열렸는데
//   커서는 잠겨있어서 닫기 버튼을 클릭할 수 없는" 문제가 생깁니다.
// ============================================================================

using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class CameraController : MonoBehaviour
{
    [Header("카메라 (씬의 CinemachineCamera에 붙어있는 Orbital Follow)")]
    public CinemachineOrbitalFollow orbitalFollow;

    [Header("상호작용 (휠 우선순위 - 선택 사항)")]
    [Tooltip("비워두면 Start()에서 씬에서 자동으로 찾습니다. 상호작용 대상이 2개 이상 범위 안에 있어 " +
              "휠이 상호작용 목록 선택에 쓰이고 있을 때는(IsCyclingActive) 이 프레임의 줌을 건너뜁니다. " +
              "씬에 InteractionDetector 자체가 없는 경우(예: 카메라만 따로 테스트할 때)를 위해 이 참조는 " +
              "선택 사항으로 남겨뒀습니다 - 없으면 그냥 항상 휠로 줌이 동작합니다.")]
    public InteractionDetector interactionDetector;

    [Header("회전 감도")]
    public float horizontalSensitivity = 0.2f;   // 마우스 좌우
    public float verticalSensitivity = 0.15f;    // 마우스 상하
    public float gamepadSensitivity = 120f;      // 게임패드 오른쪽 스틱
    public bool invertY = false;
    [Tooltip("카메라가 목표 각도까지 얼마나 부드럽게 따라가는지(초)입니다. 마우스 입력 자체는 매 프레임 " +
              "즉시 목표 각도에 반영되니 조작 반응성은 그대로 유지되면서, 실제 카메라가 도는 움직임만 " +
              "매끄러워집니다(HandleZoom()의 zoomSmoothTime과 같은 SmoothDamp 방식). 0이면 예전처럼 " +
              "마우스 델타가 그대로 즉시 반영됩니다. 값이 클수록 더 느긋하고 묵직하게, 작을수록 더 " +
              "즉각적으로 움직입니다 - 0.03~0.1 사이에서 눈으로 보며 조절하는 걸 추천합니다.")]
    public float lookSmoothTime = 0.05f;

    [Header("줌 (마우스 휠)")]
    [Tooltip("휠을 한 번 굴렸을 때 목표 줌 값이 얼마나 바뀌는지")]
    public float zoomStep = 0.08f;
    [Tooltip("목표 줌 값까지 부드럽게 도달하는 데 걸리는 시간(초). 값이 클수록 더 느긋하게 전환됩니다.")]
    public float zoomSmoothTime = 0.2f;

    [Header("커서")]
    public bool lockCursorOnStart = true;

    private InputAction mouseLook;
    private InputAction gamepadLook;
    private InputAction scroll;
    private InputAction toggleCursor;

    // 줌을 부드럽게 전환하기 위한 내부 상태
    private float targetZoomValue;
    private float zoomVelocity;

    // 회전을 부드럽게 전환하기 위한 내부 상태 - 줌과 같은 패턴입니다. targetYaw/targetPitch는 마우스
    // 입력이 들어오는 즉시 갱신되는 "목표 각도"이고, 실제 orbitalFollow.HorizontalAxis/VerticalAxis는
    // 이 목표를 SmoothDamp로 뒤따라갑니다.
    private float targetYaw;
    private float targetPitch;
    private float yawVelocity;
    private float pitchVelocity;

    private void Awake()
    {
        mouseLook = new InputAction(type: InputActionType.Value, binding: "<Mouse>/delta");
        gamepadLook = new InputAction(type: InputActionType.Value, binding: "<Gamepad>/rightStick");
        scroll = new InputAction(type: InputActionType.Value, binding: "<Mouse>/scroll");

        // Alt(좌/우 둘 다) 커서 잠금/해제를 토글합니다. Escape 키는 더 이상 여기서 처리하지 않습니다 -
        // UICanvas가 Escape를 "열려있는 UI 닫기 / 종료 확인창 띄우기" 전용으로 쓰기 시작하면서
        // (UICanvas.cs의 HandleEscapePressed, UIExit.cs 참고), 같은 Escape 입력에 이 커서 토글까지
        // 같이 반응하면 두 로직이 같은 프레임에 커서 상태를 서로 다르게 바꾸려고 경합하는 문제가
        // 생깁니다(예: UI가 커서를 풀어주는 동시에 여기서 다시 잠가버리는 식). 그래서 Alt 키만
        // 남겨뒀습니다 - 메뉴를 열지 않고 잠깐 커서만 풀고 싶을 때는 Alt를 쓰시면 됩니다.
        toggleCursor = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/leftAlt");
        toggleCursor.AddBinding("<Keyboard>/rightAlt");
    }

    private void OnEnable()
    {
        mouseLook.Enable();
        gamepadLook.Enable();
        scroll.Enable();
        toggleCursor.Enable();
        toggleCursor.performed += OnToggleCursor;
    }

    private void OnDisable()
    {
        toggleCursor.performed -= OnToggleCursor;
        mouseLook.Disable();
        gamepadLook.Disable();
        scroll.Disable();
        toggleCursor.Disable();
    }

    private void Start()
    {
        if (orbitalFollow == null)
        {
            Debug.LogError("[CameraController] Orbital Follow가 연결되지 않았습니다. 씬 준비 1~3단계를 확인하세요.");
            enabled = false;
            return;
        }

        // UICanvas.Instance.IsUIOpen을 먼저 확인합니다 - 예를 들어 UIControls(조작법 안내 패널)처럼
        // 씬 시작과 동시에 스스로 여는 UI가 있으면, 그 UI가 이미 Cursor.lockState를 None으로 풀어둔
        // 뒤일 수 있습니다. 유니티는 "모든 오브젝트의 Awake()가 어떤 오브젝트의 Start()보다도 먼저
        // 끝난다"는 것만 보장할 뿐, 서로 다른 스크립트의 Start()끼리 순서는 보장하지 않습니다 - 만약
        // 이 스크립트의 Start()가 그 UI의 Start()보다 나중에 실행되면, 방금 UI가 풀어둔 커서를 여기서
        // 다시 잠가버려서 "창은 정상적으로 열렸는데 커서는 잠겨있는" 문제가 생깁니다. 이 가드는 실행
        // 순서와 무관하게 항상 안전합니다 - 이 스크립트가 먼저 실행되면 아직 아무 UI도 안 열려있으니
        // 정상적으로 잠그고, 나중에 실행되면 이미 열려있는 UI를 보고 잠그지 않습니다.
        if (lockCursorOnStart && (UICanvas.Instance == null || !UICanvas.Instance.IsUIOpen))
        {
            SetCursorLocked(true);
        }

        if (interactionDetector == null)
        {
            interactionDetector = FindFirstObjectByType<InteractionDetector>();
        }

        // 줌의 시작 목표값을 현재 카메라 값으로 맞춰둡니다 (시작하자마자 튀는 것 방지).
        targetZoomValue = orbitalFollow.RadialAxis.Value;

        // 회전도 마찬가지로 시작 목표값을 현재 카메라 각도로 맞춰둡니다 - 안 그러면 시작하자마자
        // 목표값(기본 0)을 향해 카메라가 휙 돌아가버립니다.
        targetYaw = orbitalFollow.HorizontalAxis.Value;
        targetPitch = orbitalFollow.VerticalAxis.Value;
    }

    private void Update()
    {
        HandleLook();
        HandleZoom();
    }

    private void HandleLook()
    {
        // 커서가 풀려있을 때(예: 메뉴 조작 중)는 카메라 회전을 막습니다.
        if (Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 mouseDelta = mouseLook.ReadValue<Vector2>();
        Vector2 padDelta = gamepadLook.ReadValue<Vector2>();

        float yaw = mouseDelta.x * horizontalSensitivity + padDelta.x * gamepadSensitivity * Time.deltaTime;
        float pitch = mouseDelta.y * verticalSensitivity + padDelta.y * gamepadSensitivity * Time.deltaTime;
        if (invertY) pitch = -pitch;

        // 좌우 회전 (HorizontalAxis: 도 단위) - 목표 각도(targetYaw)는 입력이 들어오는 즉시 갱신하고,
        // 실제 적용값(h.Value)만 SmoothDampAngle로 목표를 부드럽게 뒤따라가게 합니다. Angle 버전을
        // 쓰는 이유는 HorizontalAxis가 보통 0~360도를 순환(Wrap)하는 축이라, 예를 들어 350도에서
        // 10도로 넘어갈 때 일반 SmoothDamp라면 350→0→10으로 먼 길을 도는 것처럼 계산될 수 있는데,
        // SmoothDampAngle은 항상 최단 경로(이 경우 -20도만 이동)로 보간해줍니다.
        var h = orbitalFollow.HorizontalAxis;
        targetYaw = h.ClampValue(targetYaw + yaw);
        h.Value = lookSmoothTime > 0f
            ? Mathf.SmoothDampAngle(h.Value, targetYaw, ref yawVelocity, lookSmoothTime)
            : targetYaw;
        orbitalFollow.HorizontalAxis = h;

        // 상하 회전 (VerticalAxis: 도 단위, 마우스를 위로 올리면 위를 보도록 부호 반전). VerticalAxis는
        // 보통 위아래로 제한된(Wrap이 아닌) 범위라 순환을 고려할 필요가 없어 일반 SmoothDamp를 씁니다.
        var v = orbitalFollow.VerticalAxis;
        targetPitch = v.ClampValue(targetPitch - pitch);
        v.Value = lookSmoothTime > 0f
            ? Mathf.SmoothDamp(v.Value, targetPitch, ref pitchVelocity, lookSmoothTime)
            : targetPitch;
        orbitalFollow.VerticalAxis = v;
    }

    private void HandleZoom()
    {
        // 커서가 풀려있을 때(인벤토리/캐릭터정보/옵션 등 UI가 열려서, 또는 Alt/Esc로)는 줌도
        // 막습니다 - HandleLook()과 같은 이유입니다. 이 체크가 없으면 UI 스크롤 뷰(인벤토리 목록,
        // 스킬 트리 등) 위에서 마우스 휠을 굴렸을 때 UI 스크롤과 카메라 줌이 동시에 반응해버립니다
        // (전에 고친 "UI 클릭이 공격으로 새는" 문제와 같은 종류의 버그입니다).
        if (Cursor.lockState != CursorLockMode.Locked) return;

        // 상호작용 목록에서 휠로 항목을 고르고 있는 동안엔 같은 휠 입력이 줌으로 이중 처리되지
        // 않도록 이 프레임은 건너뜁니다.
        if (interactionDetector != null && interactionDetector.IsCyclingActive) return;

        var r = orbitalFollow.RadialAxis;

        // 휠을 굴릴 때마다 "목표 줌 값"만 갱신합니다. 실제 값은 아래에서 서서히 따라갑니다.
        float scrollY = scroll.ReadValue<Vector2>().y;
        if (Mathf.Abs(scrollY) > 0.01f)
        {
            targetZoomValue = r.ClampValue(targetZoomValue - Mathf.Sign(scrollY) * zoomStep);
        }

        // 현재 값을 목표 값으로 부드럽게 보간 (SmoothDamp라 가속/감속이 자연스럽습니다).
        r.Value = Mathf.SmoothDamp(r.Value, targetZoomValue, ref zoomVelocity, zoomSmoothTime);
        orbitalFollow.RadialAxis = r;
    }

    private void OnToggleCursor(InputAction.CallbackContext ctx)
    {
        // UI(인벤토리/캐릭터정보/옵션/퀘스트/알림/확인창/대화/컷씬 등, UICanvas.IsUIOpen 참고)가
        // 열려있는 동안은 Alt로 커서를 다시 잠그지 않습니다 - 그 UI를 더 이상 클릭할 수 없게 되는
        // 문제를 막기 위해서입니다(파일 상단 [UI가 열려있는 동안엔 Alt 커서 토글도 막습니다] 참고).
        if (UICanvas.Instance != null && UICanvas.Instance.IsUIOpen) return;

        SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);
    }

    private void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}