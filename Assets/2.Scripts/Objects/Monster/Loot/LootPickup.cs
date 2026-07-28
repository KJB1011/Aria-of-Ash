// ============================================================================
// LootPickup.cs
// ----------------------------------------------------------------------------
// 필드에 떨어진 전리품 하나(월드 오브젝트)입니다. LootDropper가 몬스터 사망 위치에서
// LootPickup.Spawn()으로 빌려온 뒤 Launch()로 목표 지점(scatter된 착지 위치)까지 원신처럼
// 통통 튀어나가는 연출을 시작시킵니다.
//
// [애니메이션 흐름]
//   1) 팝(Pop) - 시작 위치(보통 몬스터 사망 위치)에서 목표 위치(착지 지점)까지, 사인 곡선으로
//      위로 살짝 솟았다 내려오는 포물선을 그리며 이동합니다 (popDuration 동안).
//   2) 착지 후 - 제자리에서 위아래로 살랑살랑 떠다니는(bob) 동시에 천천히 제자리 회전(spin)하는
//      대기 애니메이션을 계속 반복합니다. 눈에 잘 띄게 하기 위한 연출입니다.
//
// [오브젝트 풀링 - 별도 매니저 없이 이 클래스 안에서 static으로 관리]
//   LootDropper가 더 이상 Instantiate를 직접 호출하지 않고, 대신 이 클래스의 static 팩토리
//   메서드 LootPickup.Spawn(itemData, amount, position)을 호출합니다. 내부적으로 worldPickupPrefab
//   (에셋)별로 GameObjectPool을 하나씩 관리하다가(여러 아이템이 같은 프리팹을 공유하면 풀도
//   같이 공유됩니다), 다 주운 뒤(Interact())에는 Destroy 대신 그 풀로 되돌아갑니다. 몬스터가
//   많이 죽는 오픈월드에서 전리품마다 매번 Instantiate/Destroy하면 GC 비용이 쌓이기 때문입니다 -
//   MonsterHealthBar.cs/NPCNameplate.cs와 같은 이유, 같은 패턴(별도 매니저 컴포넌트 없이 static
//   Dictionary + 평범한 빈 부모 오브젝트 하나)입니다. 풀의 부모(poolRoot)는 DontDestroyOnLoad로
//   표시하지 않았습니다 - 인게임 플레이 중에는 이 씬이 재로드되지 않는다는 전제입니다.
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
//   표시한 뒤 풀로 반납합니다.
//
// [줍는 소리]
//   pickupSfxName에 이름을 넣어두면(Resources/SFX/ 아래 클립 이름) Interact()로 주운 순간 그
//   위치에서 SoundManager.Instance.PlaySFX()로 재생합니다. 비워두면 소리 없이 조용히 줍습니다.
//   이 프리팹을 여러 아이템(LootItemData.worldPickupPrefab)이 공유한다면 소리도 같이 공유됩니다 -
//   아이템마다 다른 줍는 소리를 쓰고 싶다면 아이템별로 다른 프리팹을 만들어 각자 설정하세요.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class LootPickup : MonoBehaviour, IInteractable
{
    private const int PrewarmCountPerPrefab = 3;
    private const int MaxPoolSizePerPrefab = 30;

    // worldPickupPrefab(에셋)별로 풀을 하나씩 관리합니다. static이라 씬 전체에서 프리팹 종류
    // 수만큼만 존재하고, 그 프리팹을 쓰는 모든 아이템/모든 LootDropper가 공유합니다.
    private static readonly Dictionary<GameObject, GameObjectPool> pools = new Dictionary<GameObject, GameObjectPool>();
    private static Transform poolRoot;

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

    [Header("사운드")]
    [Tooltip("주웠을 때(Interact) 재생할 효과음 이름 (Resources/SFX/ 아래 클립 이름과 일치해야 함). " +
              "비워두면 소리 없이 조용히 줍습니다.")]
    public string pickupSfxName;

    private Collider col;
    private LootItemData itemData;
    private int amount;
    private GameObject sourcePrefab; // 반납할 때 어느 풀로 돌려줘야 하는지 기억해둡니다 (Spawn()이 설정).

    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float popTimer;
    private bool isPopping;
    private float landedY;
    private float idleTimer;

    public LootItemData ItemData => itemData;
    public int Amount => amount;

    // ------------------------------------------------------------------
    // 풀에서 빌려오기 - LootDropper가 이 메서드를 통해서만 생성합니다.
    // ------------------------------------------------------------------

    /// <summary>itemData.worldPickupPrefab 풀에서 인스턴스를 빌려와 position에 배치하고, 어떤
    /// 아이템을 몇 개 담고 있는지(Setup)까지 설정해서 돌려줍니다. LootDropper.DropLoot()에서
    /// Instantiate 대신 이 메서드를 호출하세요. 반환된 인스턴스에 Launch()를 호출해 팝 연출을
    /// 시작시켜야 합니다.</summary>
    public static LootPickup Spawn(LootItemData itemData, int amount, Vector3 position)
    {
        GameObject prefab = itemData.worldPickupPrefab;
        GameObject instance = GetOrCreatePool(prefab).Get(position, Quaternion.identity);

        // worldPickupPrefab에는 반드시 LootPickup이 붙어있어야 합니다 - 없으면 여기서 바로
        // NullReferenceException이 나서, 어떤 아이템 설정이 잘못됐는지 바로 드러납니다.
        LootPickup pickup = instance.GetComponent<LootPickup>();
        pickup.sourcePrefab = prefab;
        pickup.Setup(itemData, amount);
        return pickup;
    }

    private static GameObjectPool GetOrCreatePool(GameObject prefab)
    {
        if (poolRoot == null)
        {
            GameObject rootObject = new GameObject("Pool_LootPickup");
            poolRoot = rootObject.transform;
        }

        if (!pools.TryGetValue(prefab, out GameObjectPool pool))
        {
            pool = new GameObjectPool(prefab, poolRoot, PrewarmCountPerPrefab, MaxPoolSizePerPrefab);
            pools[prefab] = pool;
        }

        return pool;
    }

    // ------------------------------------------------------------------
    // IInteractable 구현
    // ------------------------------------------------------------------

    /// <summary>상호작용 목록 UI에 표시할 이름입니다. Setup()이 호출된 뒤에만(즉 Spawn() 직후 항상
    /// 호출된 뒤에만) 유효합니다 - itemData가 비어있으면 여기서 바로 NullReferenceException이 나서,
    /// 어딘가에서 Setup() 호출을 빠뜨렸다는 게 바로 드러납니다.</summary>
    public string InteractionName => $"{itemData.displayName} x{amount}";

    public Vector3 InteractionPosition => transform.position;

    private void Awake()
    {
        col = GetComponent<Collider>();
        col.isTrigger = true; // 플레이어와 물리적으로 부딪히지 않고 통과하도록, 상호작용 판정 전용 콜라이더로 씁니다.
    }

    /// <summary>이 전리품이 어떤 아이템을 몇 개 담고 있는지 설정합니다. Spawn()이 내부적으로 호출합니다.</summary>
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
    /// 로그(UIIngameLoot)에도 같이 표시한 뒤 풀로 반납해서 "주움"을 표현합니다.</summary>
    public void Interact(GameObject interactor)
    {
        PlayerInventory.Instance.AddItem(itemData, amount);
        UIIngameLoot.Instance.AddLoot(itemData.icon, InteractionName);
        PlayPickupSfx();
        ReleaseToPool();
    }

    private void PlayPickupSfx()
    {
        if (string.IsNullOrEmpty(pickupSfxName)) return;
        SoundManager.Instance.PlaySFX(pickupSfxName, transform.position);
    }

    /// <summary>Spawn()으로 빌려온 풀로 되돌립니다. Spawn()을 거치지 않고 씬에 직접 배치되는 등
    /// sourcePrefab을 모르는 경우(정상적인 흐름에서는 발생하지 않습니다)에는 안전하게 그냥 파괴합니다.</summary>
    private void ReleaseToPool()
    {
        if (sourcePrefab == null)
        {
            Destroy(gameObject);
            return;
        }

        GetOrCreatePool(sourcePrefab).Release(gameObject);
    }
}