// ============================================================================
// DamageNumberManager.cs
// ----------------------------------------------------------------------------
// 몬스터/플레이어가 데미지를 입을 때마다 그 자리에 숫자를 띄워주는 데미지 텍스트 HUD입니다.
// VFXManager와 똑같은 이유로 오브젝트 풀링(GameObjectPool)을 사용합니다 - 전투 중에는
// 데미지 숫자가 아주 짧은 시간에 반복적으로 생겼다 사라지기 때문에, 매번 Instantiate/Destroy
// 하면 그때마다 GC 비용이 발생해서 프레임 드랍(스파이크)의 원인이 됩니다.
//
// [씬/프로젝트 준비]
//   1) Project 창에서 "Assets/Resources/HUD" 폴더를 만드세요 (Resources 폴더는 정확히
//      이 이름이어야 유니티가 인식합니다).
//   2) 그 안에 데미지 숫자 프리팹을 하나 만들어 넣으세요 (기본 이름: "DamageNumber",
//      Assets/Resources/HUD/DamageNumber.prefab). 자세한 프리팹 구성 방법은
//      DamageNumberPopup.cs 상단 주석을 참고하세요.
//   3) 씬에 미리 배치해둘 필요 없습니다 - 아무 스크립트에서나 DamageNumberManager.Instance를
//      처음 호출하는 순간 자동으로 생성되고, 씬이 바뀌어도 파괴되지 않습니다(DontDestroyOnLoad).
//      풀 크기 등을 인스펙터에서 직접 조절하고 싶다면 빈 오브젝트를 만들어 이 스크립트를
//      미리 붙여 씬에 배치해도 동일하게 동작합니다.
//
// [사용 예시]
//   DamageNumberManager.Instance.Show(damage, hitPosition, isCrit: true, DamageNumberTeam.Enemy);
//   DamageNumberManager.Instance.Show(swingDamage, hitPosition, isCrit: false, DamageNumberTeam.Player);
//
// [GameObjectPool / IPoolable]
//   실제 풀링 로직은 VFXManager가 쓰는 것과 동일한 범용 GameObjectPool을 그대로 재사용합니다
//   (GameObjectPool.cs, IPoolable.cs 참고 - 애초에 "나중에 HUD도 이걸로 풀링할 것"을 염두에 두고
//   설계해둔 클래스입니다). 개별 숫자 하나(DamageNumberPopup)가 자기 애니메이션(상승/페이드)까지
//   스스로 처리하고 끝나면 스스로 ReturnToPool()을 호출해서 반납하기 때문에, 파티클처럼 재생
//   길이를 자동 계산해줄 필요가 없어 VFXManager보다 훨씬 단순한 구조입니다.
// ============================================================================

using UnityEngine;

/// <summary>데미지를 입은 쪽이 몬스터(Enemy)인지 플레이어(Player)인지에 따라 DamageNumberPopup이
/// 색을 다르게 씁니다. 치명타는 지금은 플레이어가 몬스터를 때릴 때만 발생할 수 있어서(PlayerStats
/// 참고), Enemy 쪽에서만 실질적으로 색이 갈립니다.</summary>
public enum DamageNumberTeam
{
    Enemy,
    Player,
}

public class DamageNumberManager : MonoBehaviour
{
    private const string ResourceFolder = "HUD";

    [Header("설정")]
    [Tooltip("Resources/HUD/ 아래에 있는 데미지 숫자 프리팹 이름.")]
    public string prefabName = "DamageNumber";
    [Tooltip("미리 만들어서 대기시켜둘 인스턴스 개수. 전투 중 처음 데미지 숫자가 뜰 때 생기는 순간적인 끊김(hitch)을 막아줍니다.")]
    public int prewarmCount = 20;
    [Tooltip("대기 풀에 보관할 수 있는 최대 개수. 초과분은 반납 시 Destroy됩니다.")]
    public int maxPoolSize = 200;
    [Tooltip("켜두면 Show()/반납 등 동작을 콘솔에 로그로 남깁니다.")]
    public bool debugLog = false;

    private static DamageNumberManager instance;
    public static DamageNumberManager Instance
    {
        get
        {
            if (instance == null)
            {
                // 씬에 이미 배치해둔 인스턴스가 있으면 그걸 쓰고, 없으면 새로 만듭니다.
                instance = FindFirstObjectByType<DamageNumberManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("DamageNumberManager");
                    instance = go.AddComponent<DamageNumberManager>();
                }
            }
            return instance;
        }
    }

    private GameObject prefab;
    private GameObjectPool pool;
    private Transform poolRoot;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            // 씬 전환 등으로 인해 두 번째 DamageNumberManager가 생기면 기존 것을 유지하고 새로 생긴 걸 제거합니다.
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        poolRoot = new GameObject("Pool_DamageNumber").transform;
        poolRoot.SetParent(transform, false);
    }

    /// <summary>월드 좌표 position 자리에 데미지 숫자를 띄웁니다. isCrit이 true면 더 크고 다른 색으로,
    /// team이 Player면(=몬스터가 아니라 플레이어가 맞은 경우) 경고성 색으로 표시됩니다.</summary>
    public void Show(float amount, Vector3 position, bool isCrit, DamageNumberTeam team)
    {
        GameObjectPool p = GetOrCreatePool();
        if (p == null) return;

        GameObject spawned = p.Get(position, Quaternion.identity);
        DamageNumberPopup popup = spawned.GetComponent<DamageNumberPopup>();
        if (popup == null)
        {
            Debug.LogWarning($"[DamageNumberManager] '{prefabName}' 프리팹에 DamageNumberPopup 컴포넌트가 없습니다.", spawned);
            pool.Release(spawned);
            return;
        }

        popup.Play(amount, isCrit, team);

        if (debugLog) Debug.Log($"[DamageNumberManager] {amount:0} 데미지 표시 (team={team}, crit={isCrit}, position={position})", spawned);
    }

    /// <summary>DamageNumberPopup이 자기 애니메이션을 끝내고 스스로 반납할 때 호출합니다.
    /// 다른 곳에서 직접 호출할 일은 없습니다.</summary>
    public void ReturnToPool(GameObject instance)
    {
        pool?.Release(instance);
    }

    private GameObjectPool GetOrCreatePool()
    {
        if (pool != null) return pool;

        if (prefab == null)
        {
            prefab = Resources.Load<GameObject>($"{ResourceFolder}/{prefabName}");
            if (prefab == null)
            {
                Debug.LogWarning($"[DamageNumberManager] 'Resources/{ResourceFolder}/{prefabName}' 프리팹을 찾을 수 없습니다. " +
                                  "파일 이름과 경로(Assets/Resources/HUD/ 바로 아래)를 확인해주세요.");
                return null;
            }
        }

        pool = new GameObjectPool(prefab, poolRoot, prewarmCount, maxPoolSize);
        return pool;
    }
}