// ============================================================================
// VFXManager.cs
// ----------------------------------------------------------------------------
// 프로젝트의 모든 이펙트(파티클, 히트 이펙트, 폭발, 사망 이펙트 등)를 한 곳에서 재생/관리하는
// 싱글턴 매니저입니다. VFX 프리팹을 "Resources/VFX" 폴더 아래에 모아두면 이름(문자열)만으로
// 어디서든 재생할 수 있고, 내부적으로는 GameObjectPool(오브젝트 풀링)을 이용해 매번
// Instantiate/Destroy하지 않고 인스턴스를 재사용합니다.
//
// [왜 오브젝트 풀링을 쓰나]
//   전투 중에는 같은 이펙트(피격 스파크, 폭발 등)가 아주 짧은 시간에 반복적으로 재생됩니다.
//   매번 Instantiate/Destroy를 하면 그때마다 메모리 할당/해제와 GC 비용이 발생해서 프레임
//   드랍(스파이크)의 원인이 됩니다. 이펙트 이름별로 GameObjectPool을 하나씩 두고, 다 쓴
//   인스턴스는 Destroy하는 대신 비활성화해서 보관했다가 다음 재생 때 그대로 재사용합니다.
//
// [GameObjectPool / IPoolable]
//   실제 풀링 로직은 범용 클래스인 GameObjectPool.cs에 들어있습니다. VFX 전용 개념(파티클
//   재생 길이 계산, ParticleSystem 초기화 등)은 이 VFXManager가 담당하고, GameObjectPool
//   자체는 GameObject/Transform 외에는 아무것도 몰라서 나중에 HUD(데미지 텍스트, 알림 팝업
//   등)를 풀링할 때도 그대로 재사용할 수 있습니다. (GameObjectPool.cs, IPoolable.cs 참고)
//
// [씬/프로젝트 준비]
//   1) Project 창에서 "Assets/Resources/VFX" 폴더를 만드세요 (Resources 폴더는 정확히
//      이 이름이어야 유니티가 인식합니다). 그 안에 이펙트 프리팹들을 넣어두세요.
//      예) Assets/Resources/VFX/Hit_Fire.prefab, Assets/Resources/VFX/Explosion.prefab
//   2) 씬에 미리 배치해둘 필요 없습니다 - 아무 스크립트에서나 VFXManager.Instance를 처음
//      호출하는 순간 자동으로 생성되고, 씬이 바뀌어도 파괴되지 않습니다(DontDestroyOnLoad).
//      특정 설정(풀 크기 등)을 인스펙터에서 직접 조절하고 싶다면 빈 오브젝트를 만들어 이
//      스크립트를 미리 붙여 씬에 배치해도 동일하게 동작합니다.
//   3) 각 이펙트 프리팹은 보통 ParticleSystem을 담고 있고, Loop이 꺼져 있으면(한 번 재생하고
//      끝) 재생 길이를 자동으로 계산해서 그 시간이 지나면 알아서 풀로 반납합니다. Loop이 켜진
//      이펙트(예: 계속 타오르는 화염 오라)는 자동 계산이 불가능하므로 Play() 호출 시 duration을
//      직접 지정해주세요.
//   4) 재사용 시 파티클이 깨끗하게 다시 재생되도록, Get() 시점에 자동으로 Clear() 후 Play()를
//      호출하고 Release() 시점에 Stop(StopEmittingAndClear)을 호출합니다. 프리팹의
//      "Play On Awake" 설정과 무관하게 항상 이렇게 재시작되니 별도로 신경 쓰지 않아도 됩니다.
//
// [사용 예시]
//   VFXManager.Instance.Play("Hit_Fire", hitPoint);                         // 위치만 지정
//   VFXManager.Instance.Play("Explosion", transform.position, rotation);    // 위치 + 회전
//   VFXManager.Instance.Play("Buff_Aura", transform.position, 5f);          // 5초 후 강제 반납 (duration 직접 지정)
//   VFXManager.Instance.PlayAttached("Buff_Aura", playerTransform);         // 플레이어를 계속 따라다니는 이펙트
//
// [기존 코드에 적용하려면]
//   예를 들어 FireballProjectile.Explode()의
//     if (hitEffectPrefab != null) Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
//   부분을
//     VFXManager.Instance.Play("Fireball_Explosion", transform.position);
//   로 바꾸면 인스펙터에 프리팹을 연결할 필요 없이 이름만으로 재생되고, 자동으로 풀 반납까지
//   됩니다. 원하시면 이어서 기존 스크립트들(ProjectileBase, FireballProjectile, MonsterFSM 등)도
//   이 방식으로 옮겨드릴 수 있습니다.
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VFXManager : MonoBehaviour
{
    private const string ResourceFolder = "VFX";

    [Header("설정")]
    [Tooltip("재생 길이를 자동으로 계산할 수 없을 때(파티클 시스템이 없거나 Loop인 경우) 사용할 기본 반납 시간(초).")]
    public float defaultDuration = 3f;
    [Tooltip("게임 시작 시 Resources/VFX 폴더 안의 모든 프리팹을 미리 로드하고, 풀도 미리 만들어(prewarm)둡니다. " +
              "전투 중 처음 재생할 때 생기는 순간적인 끊김(hitch)을 막아줍니다.")]
    public bool preloadAllOnAwake = true;
    [Tooltip("이펙트 하나(=이름 하나)당 미리 만들어서 대기시켜둘 인스턴스 개수.")]
    public int prewarmCountPerVFX = 3;
    [Tooltip("이펙트 하나(=이름 하나)의 대기 풀이 보관할 수 있는 최대 개수. 초과분은 반납 시 Destroy됩니다.")]
    public int maxPoolSizePerVFX = 50;
    [Tooltip("켜두면 Play()/반납 등 동작을 콘솔에 로그로 남깁니다.")]
    public bool debugLog = false;

    private static VFXManager instance;
    public static VFXManager Instance
    {
        get
        {
            if (instance == null)
            {
                // 씬에 이미 배치해둔 인스턴스가 있으면 그걸 쓰고, 없으면 새로 만듭니다.
                instance = FindFirstObjectByType<VFXManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("VFXManager");
                    instance = go.AddComponent<VFXManager>();
                }
            }
            return instance;
        }
    }

    // 이름 → 프리팹 원본, 이름 → 그 프리팹을 위한 풀. 두 캐시를 분리해둔 이유는 프리팹 자체를
    // 아직 로드만 하고 풀은 나중에(첫 요청 시) 만드는 경로가 있을 수 있기 때문입니다.
    private readonly Dictionary<string, GameObject> prefabCache = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, GameObjectPool> poolsByName = new Dictionary<string, GameObjectPool>();
    private readonly HashSet<string> missingNameWarned = new HashSet<string>();

    // 인스턴스별로 예약해둔 "자동 반납" 코루틴과, 지금 실제로 사용 중인 인스턴스인지 여부를
    // 추적합니다. 이게 없으면 자동 반납 타이머가 끝나기 전에 누군가 수동으로 먼저 반납했을 때
    // 나중에 타이머가 또 반납을 시도해서, 그 사이 다른 곳에서 이미 재사용 중인 인스턴스를
    // 잘못 반납해버리는(이중 반납) 버그가 생길 수 있습니다.
    private readonly Dictionary<int, Coroutine> pendingReleases = new Dictionary<int, Coroutine>();
    private readonly HashSet<int> activeInstanceIds = new HashSet<int>();

    private Transform poolRoot; // 대기 중인 인스턴스들을 모아둘 부모(계층 창 정리용)

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            // 씬 전환 등으로 인해 두 번째 VFXManager가 생기면 기존 것을 유지하고 새로 생긴 걸 제거합니다.
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        poolRoot = new GameObject("Pools").transform;
        poolRoot.SetParent(transform, false);

        if (preloadAllOnAwake)
        {
            PreloadAll();
        }
    }

    // ------------------------------------------------------------------
    // 외부에서 호출하는 재생 API
    // ------------------------------------------------------------------

    /// <summary>위치만 지정해서 재생합니다. 회전은 기본값(Quaternion.identity), 반납 시점은 자동 계산됩니다.</summary>
    public GameObject Play(string vfxName, Vector3 position)
    {
        return Play(vfxName, position, Quaternion.identity, -1f, null);
    }

    /// <summary>위치와 회전을 지정해서 재생합니다. 반납 시점은 자동 계산됩니다.</summary>
    public GameObject Play(string vfxName, Vector3 position, Quaternion rotation)
    {
        return Play(vfxName, position, rotation, -1f, null);
    }

    /// <summary>위치를 지정하고, 반납까지 걸리는 시간을 직접 지정합니다. Loop 파티클(계속 타는 오라 등)처럼
    /// 자동으로 길이를 계산할 수 없는 이펙트에 사용하세요.</summary>
    public GameObject Play(string vfxName, Vector3 position, float duration)
    {
        return Play(vfxName, position, Quaternion.identity, duration, null);
    }

    /// <summary>모든 옵션을 다 지정하는 완전한 형태입니다. duration을 음수로 두면 파티클 시스템을 보고
    /// 자동으로 반납 시점을 계산합니다.</summary>
    public GameObject Play(string vfxName, Vector3 position, Quaternion rotation, float duration, Transform parent)
    {
        GameObjectPool pool = GetOrCreatePool(vfxName);
        if (pool == null) return null;

        GameObject spawned = pool.Get(position, rotation, parent);
        BeginUse(vfxName, spawned, duration);

        if (debugLog) Debug.Log($"[VFXManager] '{vfxName}' 재생 (풀 사용, position={position})", spawned);

        return spawned;
    }

    /// <summary>parent를 계속 따라다니는 이펙트를 재생합니다 (버프 오라, 캐릭터에 붙는 이펙트 등).
    /// localPosition/localRotation은 parent 기준 상대 좌표입니다. Loop 이펙트라면 duration을 꼭 지정하세요
    /// (그렇지 않으면 defaultDuration 이후 자동으로 반납됩니다).</summary>
    public GameObject PlayAttached(string vfxName, Transform parent, Vector3 localPosition = default, Quaternion? localRotation = null, float duration = -1f)
    {
        GameObjectPool pool = GetOrCreatePool(vfxName);
        if (pool == null) return null;

        Vector3 worldPosition = parent != null ? parent.position : localPosition;
        Quaternion worldRotation = parent != null ? parent.rotation : Quaternion.identity;

        GameObject spawned = pool.Get(worldPosition, worldRotation, parent);
        spawned.transform.localPosition = localPosition;
        spawned.transform.localRotation = localRotation ?? Quaternion.identity;

        BeginUse(vfxName, spawned, duration);

        if (debugLog) Debug.Log($"[VFXManager] '{vfxName}' 재생 (풀 사용, parent='{parent?.name}'에 부착)", spawned);

        return spawned;
    }

    /// <summary>자동 반납 시간이 되기 전에 직접 풀로 돌려보내고 싶을 때 호출하세요 (예: 대상이 먼저
    /// 사라져서 붙어있던 이펙트도 같이 정리해야 하는 경우). 이미 반납된 인스턴스를 다시 넘기면
    /// 안전하게 무시됩니다.</summary>
    public void ReturnToPool(string vfxName, GameObject instance)
    {
        if (instance == null) return;

        int id = instance.GetInstanceID();
        if (pendingReleases.TryGetValue(id, out Coroutine co))
        {
            StopCoroutine(co);
            pendingReleases.Remove(id);
        }

        Release(vfxName, instance);
    }

    /// <summary>Resources/VFX 폴더 전체를 미리 로드하고, 이펙트별 풀도 함께 미리 만들어(prewarm)둡니다.
    /// preloadAllOnAwake가 꺼져있다면 로딩 화면 등 원하는 타이밍에 직접 호출하세요.</summary>
    public void PreloadAll()
    {
        GameObject[] all = Resources.LoadAll<GameObject>(ResourceFolder);
        foreach (GameObject prefab in all)
        {
            if (prefabCache.ContainsKey(prefab.name)) continue;

            prefabCache[prefab.name] = prefab;
            GetOrCreatePool(prefab.name); // 풀도 바로 만들어서 prewarmCountPerVFX만큼 미리 채워둡니다.
        }

        if (debugLog) Debug.Log($"[VFXManager] Resources/{ResourceFolder} 안의 이펙트 {all.Length}개를 미리 로드/풀링했습니다.");
    }

    // ------------------------------------------------------------------
    // 내부 구현 - 사용/반납 추적
    // ------------------------------------------------------------------

    private void BeginUse(string vfxName, GameObject spawned, float explicitDuration)
    {
        int id = spawned.GetInstanceID();
        activeInstanceIds.Add(id);

        ResetParticlesForGet(spawned);

        float lifetime = explicitDuration >= 0f ? explicitDuration : CalculateAutoLifetime(spawned);
        Coroutine co = StartCoroutine(AutoReleaseRoutine(vfxName, spawned, lifetime));
        pendingReleases[id] = co;
    }

    private IEnumerator AutoReleaseRoutine(string vfxName, GameObject instance, float delay)
    {
        yield return new WaitForSeconds(delay);
        pendingReleases.Remove(instance.GetInstanceID());
        Release(vfxName, instance);
    }

    /// <summary>실제 반납을 수행합니다. activeInstanceIds에서 빠져있다면(=이미 어딘가에서 반납되어
    /// 재사용됐거나, 중복 호출) 아무 것도 하지 않습니다 - 이게 이중 반납을 막는 핵심 안전장치입니다.</summary>
    private void Release(string vfxName, GameObject instance)
    {
        int id = instance.GetInstanceID();
        if (!activeInstanceIds.Remove(id))
        {
            if (debugLog) Debug.LogWarning($"[VFXManager] '{vfxName}' 인스턴스가 이미 반납된 상태에서 또 반납이 시도됐습니다. 무시합니다.", instance);
            return;
        }

        // instance == null은 UnityEngine.Object가 오버로딩한 비교라, 네이티브 오브젝트가 이미 파괴된
        // "가짜 null" 상태도 정확히 잡아냅니다. PlayAttached()로 다른 오브젝트(플레이어 등)에 붙여서
        // 재생한 이펙트는 그 부모가 먼저 파괴되면(사망, 씬 전환 등) 자식인 이 인스턴스도 함께 파괴되는데,
        // 그래도 자동 반납 코루틴은 그대로 남아있다가 타이머가 끝나면 여기로 들어옵니다 - 이미 파괴된
        // 오브젝트에서 GetComponentsInChildren 등을 호출하면 MissingReferenceException이 나므로, 여기서
        // 먼저 걸러내고 풀 반납 없이 조용히 무시합니다(어차피 파괴됐으니 풀에 돌려줄 것도 없습니다).
        if (instance == null)
        {
            if (debugLog) Debug.LogWarning($"[VFXManager] '{vfxName}' 인스턴스가 반납 시점에 이미 파괴되어 있었습니다(부모 파괴/씬 전환 등). 무시합니다.");
            return;
        }

        StopParticlesForRelease(instance);

        if (poolsByName.TryGetValue(vfxName, out GameObjectPool pool))
        {
            pool.Release(instance);
        }
        else
        {
            // 이론상 도달하지 않지만(풀이 없다면 애초에 Get도 불가능), 안전하게 그냥 파괴합니다.
            Destroy(instance);
        }
    }

    // ------------------------------------------------------------------
    // 내부 구현 - 프리팹/풀 조회
    // ------------------------------------------------------------------

    private GameObjectPool GetOrCreatePool(string vfxName)
    {
        if (poolsByName.TryGetValue(vfxName, out GameObjectPool existing))
        {
            return existing;
        }

        GameObject prefab = GetPrefab(vfxName);
        if (prefab == null) return null;

        Transform poolParent = new GameObject($"Pool_{vfxName}").transform;
        poolParent.SetParent(poolRoot, false);

        GameObjectPool pool = new GameObjectPool(prefab, poolParent, prewarmCountPerVFX, maxPoolSizePerVFX);
        poolsByName[vfxName] = pool;
        return pool;
    }

    private GameObject GetPrefab(string vfxName)
    {
        if (string.IsNullOrEmpty(vfxName))
        {
            Debug.LogWarning("[VFXManager] 빈 이름으로 Play()를 호출했습니다.");
            return null;
        }

        if (prefabCache.TryGetValue(vfxName, out GameObject cached))
        {
            return cached;
        }

        GameObject loaded = Resources.Load<GameObject>($"{ResourceFolder}/{vfxName}");
        if (loaded == null)
        {
            // 같은 이름으로 반복 호출될 때(예: 매 프레임 히트) 콘솔이 경고로 도배되지 않도록 한 번만 띄웁니다.
            if (missingNameWarned.Add(vfxName))
            {
                Debug.LogWarning($"[VFXManager] 'Resources/{ResourceFolder}/{vfxName}' 프리팹을 찾을 수 없습니다. " +
                                  "파일 이름과 경로(Assets/Resources/VFX/ 바로 아래)를 확인해주세요.");
            }
            return null;
        }

        prefabCache[vfxName] = loaded;
        return loaded;
    }

    // ------------------------------------------------------------------
    // 내부 구현 - 파티클 재시작/정지, 재생 길이 자동 계산
    // ------------------------------------------------------------------

    /// <summary>풀에서 꺼내 재생을 시작할 때, 프리팹의 Play On Awake 설정과 무관하게 항상 깨끗한
    /// 상태에서 다시 재생되도록 강제로 Clear 후 Play를 호출합니다.</summary>
    private static void ResetParticlesForGet(GameObject instance)
    {
        ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem ps in systems)
        {
            ps.Clear(true);
            ps.Play(true);
        }
    }

    /// <summary>풀로 반납하기 직전, 남아있는 파티클을 즉시 정지/정리해서 다음 재사용 때 이전 잔상이
    /// 섞여 보이지 않도록 합니다.</summary>
    private static void StopParticlesForRelease(GameObject instance)
    {
        ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem ps in systems)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    /// <summary>duration을 직접 지정하지 않았을 때, 파티클 시스템들을 분석해서 반납 시점을 자동으로
    /// 계산합니다. Loop 파티클이 섞여 있으면 계산이 불가능하므로 경고 후 defaultDuration을 씁니다.</summary>
    private float CalculateAutoLifetime(GameObject instance)
    {
        ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        if (systems.Length == 0)
        {
            // 파티클이 아예 없는 이펙트(예: 애니메이션 클립이나 단발성 스프라이트)라면 자동 계산이
            // 불가능하므로 기본 길이를 사용합니다.
            return defaultDuration;
        }

        float longest = 0f;
        bool hasLoop = false;

        foreach (ParticleSystem ps in systems)
        {
            ParticleSystem.MainModule main = ps.main;
            if (main.loop)
            {
                hasLoop = true;
                continue;
            }

            float startDelay = GetCurveMaxValue(main.startDelay);
            float lifetime = startDelay + main.duration + GetCurveMaxValue(main.startLifetime);
            if (lifetime > longest) longest = lifetime;
        }

        if (hasLoop)
        {
            Debug.Log($"[VFXManager] '{instance.name}'에 Loop 재생되는 ParticleSystem이 포함돼 있어 " +
                              "반납 시점을 자동으로 계산할 수 없습니다. Play() 호출 시 duration을 직접 지정해주세요. " +
                              $"우선 기본값({defaultDuration}초) 후 반납합니다.");
            return defaultDuration;
        }

        return longest > 0f ? longest : defaultDuration;
    }

    /// <summary>MinMaxCurve의 모드(상수/랜덤 두 상수/커브)에 따라 있을 수 있는 최댓값을 뽑아냅니다.</summary>
    private static float GetCurveMaxValue(ParticleSystem.MinMaxCurve curve)
    {
        switch (curve.mode)
        {
            case ParticleSystemCurveMode.Constant:
                return curve.constant;
            case ParticleSystemCurveMode.TwoConstants:
                return curve.constantMax;
            case ParticleSystemCurveMode.Curve:
            case ParticleSystemCurveMode.TwoCurves:
                return curve.curveMultiplier;
            default:
                return curve.constant;
        }
    }
}