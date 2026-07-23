// ============================================================================
// GameObjectPool.cs
// ----------------------------------------------------------------------------
// 특정 프리팹 하나를 대상으로 하는 범용 오브젝트 풀입니다. Instantiate/Destroy를 반복하는 대신
// 다 쓴 인스턴스를 비활성화해서 보관해뒀다가 다음 요청에 그대로 재사용합니다.
// VFXManager가 이펙트 재생에 쓰고 있지만, GameObject/Transform 외에는 아무것도 가정하지
// 않았기 때문에 나중에 HUD(데미지 텍스트, 아이템 슬롯, 알림 팝업 등)를 만들 때도 그대로
// 재사용할 수 있습니다.
//
// [사용법]
//   GameObjectPool pool = new GameObjectPool(myPrefab, poolRootTransform, prewarmCount: 5);
//   GameObject instance = pool.Get(position, rotation);   // 필요할 때 꺼내 쓰고
//   pool.Release(instance);                                // 다 썼으면 반납 (Destroy 대신)
//
// [IPoolable]
//   인스턴스(또는 그 자식)에 IPoolable을 구현한 컴포넌트가 있으면 Get()/Release() 시점에
//   자동으로 OnGetFromPool()/OnReleaseToPool()을 호출해줍니다. 필수는 아니며, 없으면 그냥
//   SetActive만 토글됩니다.
//
// [주의]
//   - Get()으로 꺼낸 인스턴스는 언젠가 Release()로 반납해야 풀이 재사용됩니다. 반납 대신 직접
//     Object.Destroy를 호출하면 풀 내부 상태와 실제 씬 상태가 어긋날 수 있으니 피해주세요.
//   - 풀이 비어있을 때 Get()을 호출하면 새로 Instantiate해서 채웁니다(자동 확장). maxSize를
//     넘어서는 반납은 그냥 Destroy됩니다 - 순간적으로 아주 많이 스폰됐다가 줄어드는 상황에서도
//     메모리가 무한정 쌓이지 않게 하기 위함입니다.
//   - 이 클래스 자체는 "언제" Release를 호출할지(타이머 등)는 알지 못합니다. 그 스케줄링은
//     이 풀을 사용하는 쪽(VFXManager 등)의 책임입니다.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

public class GameObjectPool
{
    private readonly GameObject prefab;
    private readonly Transform poolRoot;
    private readonly int maxSize;
    private readonly Stack<GameObject> inactivePool = new Stack<GameObject>();

    /// <summary>현재 이 풀이 만들어낸(대기 중이든 사용 중이든) 전체 인스턴스 수입니다. 디버깅/모니터링용입니다.</summary>
    public int TotalCreatedCount { get; private set; }

    /// <summary>현재 대기 중(미사용)인 인스턴스 수입니다.</summary>
    public int InactiveCount => inactivePool.Count;

    /// <param name="prefab">이 풀이 관리할 프리팹.</param>
    /// <param name="poolRoot">대기 중인(비활성) 인스턴스를 보관할 부모. 계층 창 정리용이며 비워둬도 동작합니다.</param>
    /// <param name="prewarmCount">생성 시점에 미리 만들어서 대기시켜둘 개수. 처음 사용하는 순간의 Instantiate 비용을 없애고 싶을 때 사용하세요.</param>
    /// <param name="maxSize">대기 풀에 보관할 수 있는 최대 개수. 초과분은 반납 시 Destroy됩니다.</param>
    public GameObjectPool(GameObject prefab, Transform poolRoot = null, int prewarmCount = 0, int maxSize = 100)
    {
        this.prefab = prefab;
        this.poolRoot = poolRoot;
        this.maxSize = maxSize;

        for (int i = 0; i < prewarmCount; i++)
        {
            ReturnToInactivePool(CreateNew());
        }
    }

    /// <summary>풀에서 인스턴스를 꺼내 활성화하고 위치/회전/부모를 지정합니다. 대기 중인 인스턴스가 없으면 새로 만듭니다.
    /// parent를 비워두면(null) 월드 루트가 아니라 이 풀의 poolRoot 아래에 그대로 둡니다 - poolRoot는 보통
    /// VFXManager처럼 DontDestroyOnLoad로 유지되는 부모의 자식이라, parent=null로 SetParent(null)을 했다면
    /// 오브젝트가 그 DontDestroyOnLoad 계층에서 빠져나와 "현재 활성 씬"으로 옮겨가 버립니다 - 이후 씬이
    /// 전환/리로드되면 자동 반납 타이머가 끝나기도 전에 이 인스턴스가 파괴돼서, 나중에 타이머가 실행될 때
    /// MissingReferenceException이 발생하는 원인이 됩니다. 위치/회전은 SetPositionAndRotation으로 직접
    /// 지정하므로 부모를 poolRoot로 유지해도 월드 좌표에는 영향이 없습니다.</summary>
    public GameObject Get(Vector3 position, Quaternion rotation, Transform parent = null)
    {
        GameObject instance = inactivePool.Count > 0 ? inactivePool.Pop() : CreateNew();

        Transform t = instance.transform;
        t.SetParent(parent != null ? parent : poolRoot, false);
        t.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);

        NotifyPoolable(instance, isGet: true);

        return instance;
    }

    /// <summary>다 쓴 인스턴스를 반납합니다. 비활성화 후 풀 부모 아래로 되돌려 보관합니다.
    /// 대기 풀이 이미 maxSize만큼 차있으면 대신 Destroy합니다.</summary>
    public void Release(GameObject instance)
    {
        if (instance == null) return;

        NotifyPoolable(instance, isGet: false);
        instance.SetActive(false);

        if (inactivePool.Count >= maxSize)
        {
            Object.Destroy(instance);
            TotalCreatedCount--;
            return;
        }

        ReturnToInactivePool(instance);
    }

    /// <summary>대기 중인 인스턴스를 전부 Destroy하고 풀을 비웁니다 (씬 전환/정리용). 이미 꺼내가서
    /// 사용 중인 인스턴스에는 영향을 주지 않습니다 - 그건 각자 Release()로 반납되어야 합니다.</summary>
    public void Clear()
    {
        while (inactivePool.Count > 0)
        {
            GameObject instance = inactivePool.Pop();
            if (instance != null) Object.Destroy(instance);
            TotalCreatedCount--;
        }
    }

    private GameObject CreateNew()
    {
        GameObject instance = Object.Instantiate(prefab, poolRoot);
        instance.SetActive(false);
        TotalCreatedCount++;
        return instance;
    }

    private void ReturnToInactivePool(GameObject instance)
    {
        instance.transform.SetParent(poolRoot, false);
        inactivePool.Push(instance);
    }

    private static void NotifyPoolable(GameObject instance, bool isGet)
    {
        // 자식까지 훑는 이유: 이펙트/HUD 프리팹은 루트가 아니라 자식 오브젝트에 실제 로직
        // 컴포넌트가 붙어있는 경우가 많기 때문입니다.
        IPoolable[] poolables = instance.GetComponentsInChildren<IPoolable>(true);
        foreach (IPoolable poolable in poolables)
        {
            if (isGet) poolable.OnGetFromPool();
            else poolable.OnReleaseToPool();
        }
    }
}