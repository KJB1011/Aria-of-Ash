// ============================================================================
// RewardOrb.cs
// ----------------------------------------------------------------------------
// 몬스터를 처치하면 나오는 경험치/골드 오브젝트입니다. LootPickup(전리품)과 달리 상호작용
// 키로 줍는 게 아니라, 잠깐 튀어나왔다가(팝) 착지한 뒤 스스로 플레이어를 향해 날아가서(시킹)
// 자동으로 흡수됩니다. 흡수되는 순간 실제 보상(경험치/골드)이 적용되고, 전리품을 주울 때처럼
// 화면 왼쪽 로그(UIIngameLoot)에도 표시됩니다.
//
// [오브젝트 풀링 - 별도 매니저 없이 이 클래스 안에서 static으로 관리]
//   LootDropper가 더 이상 Instantiate를 직접 호출하지 않고, 대신 이 클래스의 static 팩토리
//   메서드 RewardOrb.Spawn(prefab, amount, position)을 호출합니다. 내부적으로 프리팹(Exp Orb/Gold
//   Orb 각각)별로 GameObjectPool을 하나씩 관리하다가, 흡수된 뒤(Absorb())에는 Destroy 대신 그
//   풀로 되돌아갑니다. 몬스터가 죽을 때마다 여러 개씩(expOrbCount/goldOrbCount) 쏟아지기 때문에
//   LootPickup보다도 스폰 빈도가 높습니다 - LootPickup.cs/MonsterHealthBar.cs와 같은 이유, 같은
//   패턴(별도 매니저 컴포넌트 없이 static Dictionary + 평범한 빈 부모 오브젝트 하나)입니다. 풀의
//   부모(poolRoot)는 DontDestroyOnLoad로 표시하지 않았습니다 - 인게임 플레이 중에는 이 씬이
//   재로드되지 않는다는 전제입니다.
//
// [프리팹 준비 - 경험치용/골드용 각각 하나씩]
//   1) Type을 Experience 또는 Gold로 지정하세요.
//   2) Icon과 Display Name Prefix를 채우세요 (예: Experience → 이름 "경험치", Gold → 이름 "골드").
//      화면 왼쪽 로그에는 "경험치 x12"처럼 표시됩니다.
//   3) Collider가 자동으로 추가됩니다(RequireComponent) - Is Trigger로 자동 설정되긴 하지만,
//      지금은 실제로 트리거 판정을 쓰지 않고 거리 계산(absorbDistance)만으로 흡수 여부를
//      판단합니다 (나중에 다른 용도로 재사용할 수 있도록 그냥 켜둔 것뿐입니다).
//   4) 이 두 프리팹을 LootDropper의 Exp Orb Prefab / Gold Orb Prefab 필드에 연결하세요.
//   5) Pickup Sfx Name에 흡수될 때 재생할 효과음 이름을 넣으세요(Resources/SFX/ 아래 클립 이름).
//      Exp Orb 프리팹과 Gold Orb 프리팹이 서로 다른 컴포넌트 인스턴스이므로, 경험치와 골드의
//      흡수음을 서로 다르게 설정할 수 있습니다. 비워두면 소리 없이 조용히 흡수됩니다.
//
// [동작 흐름]
//   1) Launch()로 팝(포물선) 애니메이션 시작 - LootPickup.Launch()와 같은 방식입니다.
//   2) 착지 후 seekDelay초 동안 가만히 있습니다.
//   3) 그 뒤로는 매 프레임 플레이어 쪽으로 날아갑니다. 시간이 지날수록(seekAcceleration)
//      점점 빨라져서, 플레이어가 멀리 있어도 결국 따라잡습니다.
//   4) 플레이어와의 거리가 absorbDistance 이하가 되면 흡수됩니다 - 보상 적용 + 로그 표시 + 풀 반납.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

public enum RewardOrbType
{
    Experience,
    Gold,
}

[RequireComponent(typeof(Collider))]
public class RewardOrb : MonoBehaviour
{
    private const int PrewarmCountPerPrefab = 5;
    private const int MaxPoolSizePerPrefab = 50;

    // 프리팹(에셋)별로 풀을 하나씩 관리합니다. Exp Orb/Gold Orb가 서로 다른 프리팹이면 자동으로
    // 각각 별도의 풀이 만들어집니다.
    private static readonly Dictionary<GameObject, GameObjectPool> pools = new Dictionary<GameObject, GameObjectPool>();
    private static Transform poolRoot;

    [Header("보상 종류")]
    public RewardOrbType type = RewardOrbType.Experience;
    public Sprite icon;
    [Tooltip("전리품 로그에 표시될 이름입니다. 예: \"경험치\" → \"경험치 x12\"로 표시됩니다.")]
    public string displayNamePrefix = "경험치";

    [Header("팝 애니메이션 (드롭되는 순간)")]
    public float popDuration = 0.4f;
    public float popArcHeight = 1f;

    [Header("흡수 (플레이어 추적)")]
    [Tooltip("착지 후 이 시간(초) 동안은 가만히 있다가 플레이어 쪽으로 날아가기 시작합니다.")]
    public float seekDelay = 0.3f;
    [Tooltip("플레이어를 향해 날아가는 시작 속도(미터/초).")]
    public float seekSpeed = 6f;
    [Tooltip("시간이 지날수록 속도가 이만큼(미터/초²) 점점 빨라집니다 - 플레이어가 멀리 있어도 결국 따라잡습니다.")]
    public float seekAcceleration = 12f;
    [Tooltip("플레이어와 이 거리 안으로 들어오면 흡수됩니다.")]
    public float absorbDistance = 0.6f;

    [Header("사운드")]
    [Tooltip("흡수(Absorb)되는 순간 재생할 효과음 이름 (Resources/SFX/ 아래 클립 이름과 일치해야 함). " +
              "비워두면 소리 없이 조용히 흡수됩니다. Exp Orb/Gold Orb 프리팹마다 다르게 설정하세요.")]
    public string pickupSfxName;

    private int amount;
    private Transform playerTransform;
    private GameObject sourcePrefab; // 반납할 때 어느 풀로 돌려줘야 하는지 기억해둡니다 (Spawn()이 설정).

    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float popTimer;
    private bool isPopping = true;
    private float seekTimer;
    private float currentSeekSpeed;

    // ------------------------------------------------------------------
    // 풀에서 빌려오기 - LootDropper가 이 메서드를 통해서만 생성합니다.
    // ------------------------------------------------------------------

    /// <summary>prefab 풀에서 인스턴스를 빌려와 position에 배치하고, 보상 수치(Setup)까지 설정해서
    /// 돌려줍니다. LootDropper.SpawnOrbs()에서 Instantiate 대신 이 메서드를 호출하세요. 반환된
    /// 인스턴스에 Launch()를 호출해 팝 연출을 시작시켜야 합니다.</summary>
    public static RewardOrb Spawn(GameObject prefab, int amount, Vector3 position)
    {
        GameObject instance = GetOrCreatePool(prefab).Get(position, Quaternion.identity);

        // prefab에는 반드시 RewardOrb가 붙어있어야 합니다 - 없으면 여기서 바로
        // NullReferenceException이 나서, 프리팹 연결을 빠뜨렸다는 게 바로 드러납니다.
        RewardOrb orb = instance.GetComponent<RewardOrb>();
        orb.sourcePrefab = prefab;
        orb.Setup(amount);
        return orb;
    }

    private static GameObjectPool GetOrCreatePool(GameObject prefab)
    {
        if (poolRoot == null)
        {
            GameObject rootObject = new GameObject("Pool_RewardOrb");
            poolRoot = rootObject.transform;
        }

        if (!pools.TryGetValue(prefab, out GameObjectPool pool))
        {
            pool = new GameObjectPool(prefab, poolRoot, PrewarmCountPerPrefab, MaxPoolSizePerPrefab);
            pools[prefab] = pool;
        }

        return pool;
    }

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    /// <summary>이 오브젝트가 지급할 보상 수치를 설정합니다. Spawn()이 내부적으로 호출합니다.</summary>
    public void Setup(int amount)
    {
        this.amount = amount;
        playerTransform = PlayerStats.Instance.transform;
    }

    /// <summary>현재 위치(시작 위치)에서 targetPosition(착지 위치)까지 포물선을 그리며 튀어나가는
    /// 연출을 시작합니다. LootPickup.Launch()와 같은 방식입니다.</summary>
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
            UpdateSeek();
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
            seekTimer = seekDelay;
            currentSeekSpeed = seekSpeed;
        }
    }

    private void UpdateSeek()
    {
        if (seekTimer > 0f)
        {
            seekTimer -= Time.deltaTime;
            return;
        }

        currentSeekSpeed += seekAcceleration * Time.deltaTime;

        Vector3 toPlayer = playerTransform.position - transform.position;
        float distance = toPlayer.magnitude;

        if (distance <= absorbDistance)
        {
            Absorb();
            return;
        }

        transform.position += toPlayer.normalized * currentSeekSpeed * Time.deltaTime;
    }

    private void Absorb()
    {
        if (type == RewardOrbType.Experience)
        {
            PlayerStats.Instance.AddExperience(amount);
        }
        else
        {
            PlayerCurrency.Instance.AddGold(amount);
        }

        UIIngameLoot.Instance.AddLoot(icon, $"{displayNamePrefix} x{amount}");
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