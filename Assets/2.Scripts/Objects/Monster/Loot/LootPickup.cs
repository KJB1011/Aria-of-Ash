// ============================================================================
// LootPickup.cs
// ----------------------------------------------------------------------------
// 필드에 떨어진 전리품 하나(월드 오브젝트)입니다. LootDropper가 몬스터 사망 위치에서
// Instantiate한 뒤 Setup()으로 어떤 아이템인지 알려주고, Launch()로 목표 지점(scatter된
// 착지 위치)까지 원신처럼 통통 튀어나가는 연출을 시작시킵니다.
//
// [애니메이션 흐름]
//   1) 팝(Pop) - 시작 위치(보통 몬스터 사망 위치)에서 목표 위치(착지 지점)까지, 사인 곡선으로
//      위로 살짝 솟았다 내려오는 포물선을 그리며 이동합니다 (popDuration 동안).
//   2) 착지 후 - 제자리에서 위아래로 살랑살랑 떠다니는(bob) 동시에 천천히 제자리 회전(spin)하는
//      대기 애니메이션을 계속 반복합니다. 눈에 잘 띄게 하기 위한 연출입니다.
//
// [프리팹 준비]
//   1) 전리품으로 쓸 월드 모델(메쉬/스프라이트 등)을 가진 프리팹에 이 스크립트를 붙이세요.
//   2) Collider가 자동으로 추가됩니다(RequireComponent) - Is Trigger로 자동 설정되어(Awake) 플레이어가
//      부딪혀도 밀리지 않고 그냥 통과하며, InteractionDetector가 이 Collider를 감지해서 상호작용
//      목록에 띄웁니다.
//   3) 이 오브젝트의 레이어를 InteractionDetector의 Interactable Mask에 포함된 레이어로 지정하세요
//      (예: "Interactable"). 레이어가 안 맞으면 범위 안에 있어도 상호작용 목록에 나타나지 않습니다.
//   4) 이 프리팹을 LootItemData의 World Pickup Prefab 필드에 연결하세요.
//
// [상호작용]
//   IInteractable을 구현해서, InteractionDetector(플레이어)가 범위 안에서 감지 → 목록에 표시 →
//   상호작용 키로 선택하면 Interact()가 호출되는 흐름을 그대로 탑니다. Interact()에서
//   PlayerInventory.Instance.AddItem()으로 실제로 인벤토리에 넣고, UIIngameLoot로 왼쪽 로그에도
//   표시한 뒤 오브젝트를 파괴합니다.
// ============================================================================

using UnityEngine;

[RequireComponent(typeof(Collider))]
public class LootPickup : MonoBehaviour, IInteractable
{
    [Header("팝 애니메이션 (드롭되는 순간)")]
    [Tooltip("시작 위치에서 착지 위치까지 이동하는 데 걸리는 시간(초).")]
    public float popDuration = 0.5f;
    [Tooltip("이동 도중 사인 곡선으로 솟아오르는 최대 높이(미터).")]
    public float popArcHeight = 1.5f;

    [Header("착지 후 대기 애니메이션")]
    public float bobHeight = 0.15f;
    public float bobSpeed = 2f;
    [Tooltip("초당 회전 각도(도).")]
    public float spinSpeed = 90f;

    private Collider col;
    private LootItemData itemData;
    private int amount;

    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float popTimer;
    private bool isPopping;
    private float landedY;
    private float idleTimer;

    public LootItemData ItemData => itemData;
    public int Amount => amount;

    // ------------------------------------------------------------------
    // IInteractable 구현
    // ------------------------------------------------------------------

    /// <summary>상호작용 목록 UI에 표시할 이름입니다. Setup()이 호출된 뒤에만(즉 LootDropper가
    /// Instantiate 직후 항상 호출해준 뒤에만) 유효합니다 - itemData가 비어있으면 여기서 바로
    /// NullReferenceException이 나서, 어딘가에서 Setup() 호출을 빠뜨렸다는 게 바로 드러납니다.</summary>
    public string InteractionName => $"{itemData.displayName} x{amount}";

    public Vector3 InteractionPosition => transform.position;

    private void Awake()
    {
        col = GetComponent<Collider>();
        col.isTrigger = true; // 플레이어와 물리적으로 부딪히지 않고 통과하도록, 상호작용 판정 전용 콜라이더로 씁니다.
    }

    /// <summary>이 전리품이 어떤 아이템을 몇 개 담고 있는지 설정합니다. LootDropper가 Instantiate 직후 호출합니다.</summary>
    public void Setup(LootItemData itemData, int amount)
    {
        this.itemData = itemData;
        this.amount = amount;
    }

    /// <summary>현재 위치(시작 위치)에서 targetPosition(착지 위치)까지 포물선을 그리며 튀어나가는
    /// 연출을 시작합니다. 도착하면 자동으로 제자리 bob+spin 대기 애니메이션으로 전환됩니다.</summary>
    public void Launch(Vector3 targetPosition)
    {
        startPosition = transform.position;
        this.targetPosition = targetPosition;
        popTimer = 0f;
        isPopping = true;
    }

    private void Update()
    {
        if (isPopping)
        {
            UpdatePop();
        }
        else
        {
            UpdateIdleBob();
        }
    }

    private void UpdatePop()
    {
        popTimer += Time.deltaTime;
        float t = Mathf.Clamp01(popTimer / popDuration);

        Vector3 horizontal = Vector3.Lerp(startPosition, targetPosition, t);
        float arc = Mathf.Sin(t * Mathf.PI) * popArcHeight;
        transform.position = horizontal + Vector3.up * arc;

        if (t >= 1f)
        {
            isPopping = false;
            landedY = targetPosition.y;
            idleTimer = 0f;
        }
    }

    private void UpdateIdleBob()
    {
        idleTimer += Time.deltaTime;
        float bob = Mathf.Sin(idleTimer * bobSpeed) * bobHeight;

        Vector3 pos = targetPosition;
        pos.y = landedY + bob;
        transform.position = pos;

        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
    }

    /// <summary>IInteractable 구현. InteractionDetector가 플레이어의 상호작용 키 입력을 받아 이 대상이
    /// 선택되어 있을 때 호출합니다. PlayerInventory에 실제로 아이템을 추가하고, 화면 왼쪽 전리품
    /// 로그(UIIngameLoot)에도 같이 표시한 뒤 오브젝트를 파괴해서 "주움"을 표현합니다.</summary>
    public void Interact(GameObject interactor)
    {
        PlayerInventory.Instance.AddItem(itemData, amount);
        UIIngameLoot.Instance.AddLoot(itemData.icon, InteractionName);
        Destroy(gameObject);
    }
}