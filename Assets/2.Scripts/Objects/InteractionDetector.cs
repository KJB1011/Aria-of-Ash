// ============================================================================
// InteractionDetector.cs
// ----------------------------------------------------------------------------
// 원신처럼, 플레이어 주변의 작은 범위(detectRange) 안에 상호작용 가능한 대상(IInteractable)이
// 들어오면 그 목록을 계속 들고 있다가, 상호작용 키를 누르면 그중 "선택된" 대상 하나와
// 상호작용합니다. 대상이 2개 이상이면 마우스 휠로 선택을 바꿀 수 있습니다.
//
// [UI 연동 - UI 자체는 직접 만드시면 됩니다]
//   이 스크립트는 UI를 그리지 않습니다. 대신 아래 public 프로퍼티로 "지금 범위 안에 뭐가 있고,
//   그중 뭐가 선택돼 있는지"를 그대로 노출하니, UI 스크립트에서 이 컴포넌트를 참조해서 매 프레임
//   (또는 값이 바뀔 때) 읽어가서 표시하면 됩니다.
//     - NearbyInteractables : 범위 안의 모든 상호작용 대상 목록 (가까운 순으로 정렬됨)
//     - SelectedIndex       : NearbyInteractables 안에서 지금 선택된 인덱스
//     - SelectedInteractable: 지금 선택된 대상 (없으면 null)
//   각 대상의 표시 이름은 IInteractable.InteractionName으로 가져오면 됩니다.
//
// [씬 준비]
//   1) Player 오브젝트에 이 스크립트를 추가하세요.
//   2) Interactable Mask에 상호작용 가능한 오브젝트들이 속한 레이어를 지정하세요.
//      (예: "Interactable"이라는 새 레이어를 만들어서 LootPickup 프리팹에 지정)
//   3) 상호작용 키는 기본적으로 키보드 F / 게임패드 West 버튼(Xbox 기준 X, PlayStation 기준 □)에
//      바인딩되어 있습니다. 다른 키로 바꾸고 싶으면 Awake()의 바인딩 문자열을 수정하세요.
//
// [마우스 휠 - 카메라 줌과의 우선순위]
//   범위 안에 상호작용 대상이 2개 이상일 때는(IsCyclingActive == true) 마우스 휠을 굴리면 선택
//   대상이 바뀝니다. 이때는 CameraController 쪽에서 이 컴포넌트의 IsCyclingActive를 보고
//   같은 프레임의 줌 입력을 건너뛰도록 되어 있습니다 - 그래서 목록을 고르는 동안 카메라가 같이
//   줌인/아웃되는 일이 없습니다. 대상이 1개 이하면 평소처럼 휠이 카메라 줌으로 그대로 사용됩니다.
//
// [UI가 열려있을 때(커서가 풀려있을 때)]
//   HandleCycleInput()(휠로 상호작용 대상 고르기)과 HandleInteractInput()(F키)은 둘 다
//   Cursor.lockState가 Locked가 아니면 그 프레임엔 아무 것도 하지 않습니다 - CameraController의
//   HandleZoom()과 같은 이유입니다. 인벤토리 등 UI가 열려있는 동안 F를 누르거나 휠을 굴려도
//   뒤에서 아이템을 줍거나 상호작용 대상이 바뀌지 않습니다.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

public class InteractionDetector : MonoBehaviour
{
    [Header("탐지")]
    [Tooltip("이 범위 안에 있는 상호작용 가능한 대상을 전부 감지합니다. 원신처럼 좁게 잡는 걸 권장합니다.")]
    public float detectRange = 2f;
    [Tooltip("상호작용 가능한 대상이 속한 레이어. LootPickup 등 상호작용 오브젝트들을 이 레이어로 지정하세요.")]
    public LayerMask interactableMask;
    [Tooltip("몇 초 간격으로 주변을 다시 스캔할지.")]
    public float scanInterval = 0.1f;
    [Tooltip("한 번에 감지할 수 있는 최대 콜라이더 수 (버퍼 크기).")]
    public int maxDetections = 16;

    /// <summary>범위 안의 모든 상호작용 대상입니다. 가까운 순으로 정렬되어 있습니다.</summary>
    public IReadOnlyList<IInteractable> NearbyInteractables => nearbyInteractables;
    /// <summary>NearbyInteractables 안에서 지금 선택된 인덱스입니다. 목록이 비어있으면 의미가 없습니다.</summary>
    public int SelectedIndex => selectedIndex;
    /// <summary>지금 선택된 대상입니다. 범위 안에 아무것도 없으면 null입니다.</summary>
    public IInteractable SelectedInteractable => nearbyInteractables.Count > 0 ? nearbyInteractables[selectedIndex] : null;
    /// <summary>대상이 2개 이상이라 마우스 휠로 선택을 바꿀 수 있는 상태인지. 카메라의 줌 스크립트가
    /// 이 값을 보고 같은 프레임의 줌 입력을 건너뜁니다.</summary>
    public bool IsCyclingActive => nearbyInteractables.Count > 1;

    private readonly List<IInteractable> nearbyInteractables = new List<IInteractable>();
    private int selectedIndex;
    private float scanTimer;
    private Collider[] scanBuffer;

    private InputAction interactAction;
    private InputAction cycleScroll;

    private void Awake()
    {
        scanBuffer = new Collider[Mathf.Max(1, maxDetections)];

        interactAction = new InputAction("Interact", InputActionType.Button, "<Keyboard>/f");
        interactAction.AddBinding("<Gamepad>/buttonWest");

        cycleScroll = new InputAction("InteractionCycle", InputActionType.Value, "<Mouse>/scroll");
    }

    private void OnEnable()
    {
        interactAction.Enable();
        cycleScroll.Enable();
    }

    private void OnDisable()
    {
        interactAction.Disable();
        cycleScroll.Disable();
    }

    private void Update()
    {
        scanTimer -= Time.deltaTime;
        if (scanTimer <= 0f)
        {
            scanTimer = scanInterval;
            Rescan();
        }

        HandleCycleInput();
        HandleInteractInput();
    }

    private void Rescan()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, detectRange, scanBuffer, interactableMask);

        IInteractable previousSelected = SelectedInteractable;

        nearbyInteractables.Clear();
        for (int i = 0; i < count; i++)
        {
            Collider col = scanBuffer[i];
            if (col == null) continue;

            IInteractable interactable = col.GetComponentInParent<IInteractable>();
            if (interactable == null) continue;
            if (!IsAlive(interactable)) continue;
            if (nearbyInteractables.Contains(interactable)) continue; // 콜라이더가 여러 개라도 대상은 한 번만 넣습니다.

            nearbyInteractables.Add(interactable);
        }

        nearbyInteractables.Sort((a, b) =>
        {
            float sqrDistA = (a.InteractionPosition - transform.position).sqrMagnitude;
            float sqrDistB = (b.InteractionPosition - transform.position).sqrMagnitude;
            return sqrDistA.CompareTo(sqrDistB);
        });

        // 이전에 선택했던 대상이 이번 스캔에도 범위 안에 남아있으면 계속 그 대상을 선택 상태로
        // 유지합니다 (그렇지 않으면 스캔될 때마다 선택이 0번으로 튀어서 휠로 고르던 게 리셋됩니다).
        // 없으면(대상을 잃었거나 첫 스캔이면) 가장 가까운 대상(0번)을 선택합니다.
        if (previousSelected != null && IsAlive(previousSelected) && nearbyInteractables.Contains(previousSelected))
        {
            selectedIndex = nearbyInteractables.IndexOf(previousSelected);
        }
        else
        {
            selectedIndex = 0;
        }
    }

    private void HandleCycleInput()
    {
        // 커서가 풀려있을 때(인벤토리 등 UI가 열려있을 때)는 휠 입력을 상호작용 선택에 쓰지
        // 않습니다 - CameraController.HandleZoom()과 같은 이유입니다(UI 스크롤 뷰 위에서
        // 휠을 굴렸는데 그 아래 상호작용 선택까지 같이 바뀌어버리는 걸 막습니다).
        if (Cursor.lockState != CursorLockMode.Locked) return;

        if (!IsCyclingActive) return; // 대상이 1개 이하면 휠을 상호작용 선택에 쓰지 않고 카메라 줌에 그대로 넘깁니다.

        float scrollY = cycleScroll.ReadValue<Vector2>().y;
        if (Mathf.Abs(scrollY) < 0.01f) return;

        int direction = scrollY > 0f ? -1 : 1;
        selectedIndex = (selectedIndex + direction + nearbyInteractables.Count) % nearbyInteractables.Count;
    }

    private void HandleInteractInput()
    {
        // UI가 열려있는 동안(커서가 풀려있는 동안)은 F키 상호작용도 막습니다 - 인벤토리 등을 보는
        // 중에 F를 눌렀는데 뒤에서 아이템을 주워버리는 일이 없도록 합니다.
        if (Cursor.lockState != CursorLockMode.Locked) return;

        if (!interactAction.WasPressedThisFrame()) return;

        IInteractable target = SelectedInteractable;
        if (target == null) return; // 범위 안에 상호작용할 대상이 없으면 그냥 무시합니다 (정상 상태).

        target.Interact(gameObject);
        Rescan(); // 상호작용 직후(특히 대상이 파괴되는 경우) 목록을 바로 갱신해서 다음 스캔까지 stale 상태로 남지 않게 합니다.
    }

    /// <summary>interactable이 UnityEngine.Object 기반(MonoBehaviour 등)이면서 이미 Destroy()된
    /// 상태인지를 확인합니다. "as Object" 캐스팅을 거치면 Unity가 오버로드한 == 연산자를 통해
    /// Destroy() 직후(그 프레임 동안 C# 래퍼가 아직 GC되지 않은 상태)에도 안전하게 null로 판정됩니다.</summary>
    private static bool IsAlive(IInteractable interactable)
    {
        return (interactable as Object) != null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, detectRange);
    }
}