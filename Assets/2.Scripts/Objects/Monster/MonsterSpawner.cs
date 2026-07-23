// ============================================================================
// MonsterSpawner.cs
// ----------------------------------------------------------------------------
// 특정 몬스터 프리팹 하나를 일정 시간마다 자동으로 스폰하는 팩토리입니다. 씬에 빈 오브젝트를
// 하나 놓고 이 스크립트를 붙이면, 그 위치를 기준으로 spawnInterval마다 monsterPrefab을
// Instantiate합니다.
//
// [스폰 위치]
//   spawnRadius가 0이면 항상 이 오브젝트의 정확한 위치에 스폰합니다. 0보다 크면 그 반경 안의
//   무작위 지점에 스폰합니다 - 몬스터는 NavMeshAgent 기반(MonsterFSM 참고)이라 NavMesh 밖에서
//   스폰되면 문제가 생기므로, 무작위로 고른 지점 주변에서 NavMesh.SamplePosition으로 가장
//   가까운 유효한 NavMesh 위치를 찾아 그 자리에 스폰합니다(navMeshSampleDistance 범위 안에서
//   못 찾으면 원래 지점에 그대로 스폰하고 경고를 남깁니다 - NavMesh Bake 범위를 확인하세요).
//   몬스터는 자기 자신의 Start()에서 스폰된 그 위치를 자신의 배회/리쉬 기준점(spawnPosition)으로
//   그대로 사용합니다(MonsterFSM.cs 참고) - 스포너가 따로 신경 쓸 필요는 없습니다.
//
// [동시 생존 제한 - maxAliveCount]
//   이 스포너가 만든 몬스터 중 아직 파괴되지 않고 살아있는 수를 세어서, maxAliveCount에
//   도달하면 주기가 되어도 스폰을 건너뜁니다(몬스터가 죽어서 오브젝트가 파괴되면 다시 스폰할
//   자리가 생깁니다). 몬스터가 죽었는지 여부는 MonsterFSM.Die()가 dieDelay 후 스스로
//   Object.Destroy(gameObject)를 호출하는 것에 기대어, 스폰 목록에서 파괴된(null이 된) 항목을
//   그때그때 걸러내는 방식으로 판단합니다 - 별도의 사망 이벤트 연동이 필요 없습니다.
//   0 이하로 두면 제한 없이 계속 스폰합니다.
//
// [켜고 끄기 - isSpawningEnabled]
//   false로 두면(인스펙터에서 직접, 또는 다른 스크립트가 코드로) 타이머가 아예 진행되지 않아
//   스폰이 완전히 멈춥니다 - 특정 조건(보스 처치, 이벤트 트리거 등)에서 스폰을 멈추고 싶을 때
//   다른 스크립트에서 이 필드를 false로 바꿔주면 됩니다.
//
// [씬 준비]
//   1) 몬스터를 스폰하고 싶은 위치에 빈 오브젝트를 만들고 이 스크립트를 붙이세요.
//   2) Monster Prefab에 스폰할 몬스터 프리팹(MonsterFSM을 상속한 스크립트가 붙어있는 것,
//      예: Slime/WoodGolem)을 연결하세요.
//   3) Spawn Interval(초), Max Alive Count(동시 생존 제한), Spawn Radius(무작위 스폰 반경) 등을
//      필요에 맞게 조절하세요.
//   4) NavMesh가 이 오브젝트 주변에도 Bake되어 있어야 스폰된 몬스터가 정상적으로 움직입니다.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class MonsterSpawner : MonoBehaviour
{
    [Header("스폰할 몬스터")]
    [Tooltip("일정 시간마다 스폰할 몬스터 프리팹입니다. MonsterFSM을 상속한 스크립트가 붙어있어야 합니다.")]
    public GameObject monsterPrefab;

    [Header("스폰 주기")]
    [Tooltip("몬스터를 스폰하는 주기(초)입니다.")]
    public float spawnInterval = 10f;
    [Tooltip("켜두면 씬 시작 즉시 한 마리를 먼저 스폰하고, 그 다음부터 spawnInterval마다 스폰합니다. " +
              "꺼두면 시작하고 spawnInterval초가 지나야 첫 스폰이 일어납니다.")]
    public bool spawnImmediatelyOnStart = true;

    [Header("동시 생존 제한")]
    [Tooltip("이 스포너가 만든 몬스터 중 살아있는(아직 파괴되지 않은) 수가 이 값에 도달하면, 주기가 " +
              "되어도 스폰을 건너뜁니다. 0 이하로 두면 제한 없이 계속 스폰합니다.")]
    public int maxAliveCount = 3;

    [Header("스폰 위치")]
    [Tooltip("이 오브젝트 위치를 중심으로 이 반경(미터) 안의 무작위 지점에 스폰합니다. 0이면 항상 " +
              "정확히 이 오브젝트의 위치에 스폰합니다.")]
    public float spawnRadius = 0f;
    [Tooltip("무작위로 고른 지점 주변에서 유효한 NavMesh 위치를 찾는 최대 거리(미터). 몬스터는 " +
              "NavMeshAgent 기반이라 NavMesh 밖에서 스폰되면 정상적으로 움직이지 못합니다 - 이 값을 " +
              "넉넉히 잡아두면 안전합니다.")]
    public float navMeshSampleDistance = 2f;

    [Header("켜고 끄기")]
    [Tooltip("false로 두면 타이머가 멈춰서 스폰이 완전히 중단됩니다. 다른 스크립트에서 이 필드를 " +
              "바꿔서 스폰을 껐다 켰다 할 수 있습니다.")]
    public bool isSpawningEnabled = true;

    // 이 스포너가 만든 몬스터 인스턴스입니다. 죽어서 파괴되면(MonsterFSM.Die()가 dieDelay 후 스스로
    // Destroy) 자동으로 null이 되므로, 스폰 시도 시점마다 걸러내서 "지금 살아있는 수"를 셉니다.
    private readonly List<GameObject> spawnedMonsters = new List<GameObject>();

    private float timer;

    private void Start()
    {
        timer = 0f;

        if (spawnImmediatelyOnStart)
        {
            TrySpawn();
        }
    }

    private void Update()
    {
        if (!isSpawningEnabled) return;

        timer += Time.deltaTime;
        if (timer < spawnInterval) return;

        timer = 0f;
        TrySpawn();
    }

    /// <summary>지금 살아있는 수가 maxAliveCount 미만이면(또는 제한이 없으면) 몬스터 한 마리를
    /// 스폰합니다. monsterPrefab이 비어있으면 아무 것도 하지 않습니다.</summary>
    private void TrySpawn()
    {
        if (monsterPrefab == null) return;

        PruneDestroyedMonsters();
        if (maxAliveCount > 0 && spawnedMonsters.Count >= maxAliveCount) return;

        Vector3 spawnPosition = GetSpawnPosition();
        GameObject instance = Instantiate(monsterPrefab, spawnPosition, transform.rotation);
        spawnedMonsters.Add(instance);
    }

    /// <summary>spawnRadius 안의 무작위 지점을 고르고(0이면 이 오브젝트 위치 그대로), 그 지점
    /// 주변에서 navMeshSampleDistance 안의 가장 가까운 유효한 NavMesh 위치를 찾아 반환합니다.
    /// 유효한 NavMesh를 못 찾으면 원래 고른(레이캐스트 전) 지점을 그대로 반환하고 경고를 남깁니다.</summary>
    private Vector3 GetSpawnPosition()
    {
        Vector3 desiredPosition = transform.position;

        if (spawnRadius > 0f)
        {
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            desiredPosition += new Vector3(offset.x, 0f, offset.y);
        }

        if (NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
        {
            return hit.position;
        }

        Debug.LogWarning($"[MonsterSpawner] {name}: 스폰 위치 근처(반경 {navMeshSampleDistance}m)에서 유효한 " +
                          "NavMesh를 찾지 못해 원래 지점에 그대로 스폰합니다 - NavMesh Bake 범위를 확인해주세요.", this);
        return desiredPosition;
    }

    /// <summary>파괴된(죽어서 Destroy된) 몬스터를 목록에서 걸러냅니다.</summary>
    private void PruneDestroyedMonsters()
    {
        spawnedMonsters.RemoveAll(monster => monster == null);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}