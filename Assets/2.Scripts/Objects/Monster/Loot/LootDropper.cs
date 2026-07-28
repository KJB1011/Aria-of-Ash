// ============================================================================
// LootDropper.cs
// ----------------------------------------------------------------------------
// 몬스터 오브젝트에 붙이는 컴포넌트입니다. 죽는 순간 DropLoot()를 호출해주면,
// Loot Table을 굴려서 나온 전리품들을 죽은 위치 주변에 흩뿌립니다(LootPickup 인스턴스 생성).
// MonsterFSM/MiddleSlimeBoss가 각각 [RequireComponent]로 이 컴포넌트를 자동으로 갖고
// 있으므로, 몬스터 프리팹마다 직접 붙일 필요는 없습니다 - Loot Table 필드만 채워주면 됩니다.
//
// [씬/프리팹 준비]
//   1) 몬스터 프리팹의 Loot Dropper 컴포넌트(MonsterFSM/MiddleSlimeBoss에 자동으로 붙어있음)에서
//      Loot Table 필드에 미리 만들어둔 LootTable 애셋을 연결하세요.
//   2) Loot Table을 비워두면(null) 이 몬스터는 아무것도 드롭하지 않습니다 - 정상적인 설정입니다
//      (드롭 아이템이 없는 몬스터도 있을 수 있으니까요).
//   3) Ground Mask에 바닥 레이어를 지정하면, 흩뿌려지는 지점마다 실제 바닥 높이를 레이캐스트로
//      찾아서 그 위에 정확히 착지시킵니다. 비워두면(Nothing) 레이캐스트를 생략하고 항상 죽은
//      위치와 같은 높이로 착지시킵니다 (평평한 바닥이면 이걸로 충분합니다).
//
// [동작 - 전리품]
//   DropLoot() 호출 시 lootTable.RollDrops()로 이번에 드롭될 (아이템, 개수) 목록을 뽑고,
//   각 아이템마다 LootPickup.Spawn()으로 이 오브젝트 위치(+dropHeight)에서 인스턴스를 빌려온 뒤,
//   scatterRadius 안의 무작위 지점을 착지 목표로 LootPickup.Launch()를 호출해 포물선으로
//   튀어나가는 연출을 시작시킵니다.
//
// [동작 - 경험치/골드]
//   DropRewards() 호출 시 MonsterStats의 expReward/goldReward를 각각 expOrbCount/goldOrbCount개의
//   RewardOrb로 나눠서(최대한 균등하게) 같은 방식(RewardOrb.Spawn())으로 흩뿌립니다. LootPickup과
//   달리 RewardOrb는 상호작용 없이 스스로 플레이어에게 날아가 자동으로 흡수됩니다(RewardOrb.cs
//   참고). 몬스터의 사망 처리에서 DropLoot()와 DropRewards()를 둘 다 호출해주세요.
//
// [오브젝트 풀링 - LootPickup.cs/RewardOrb.cs 쪽에서 처리]
//   전리품/보상 오브젝트를 이제 Instantiate로 직접 만들지 않고, 각각의 static 팩토리 메서드
//   (LootPickup.Spawn()/RewardOrb.Spawn())를 통해 풀에서 빌려옵니다 - 몬스터가 죽을 때마다
//   여러 개씩 쏟아지는 만큼 풀링 효과가 큽니다. 이 스크립트(LootDropper) 입장에서 바뀐 건
//   Instantiate 호출을 Spawn 호출로 바꾼 것뿐이고, 나머지 흐름(위치 계산, Launch 호출)은 동일합니다.
//
// [드롭 위치를 직접 지정하고 싶다면 - DropLoot(Vector3)/DropRewards(Vector3)]
//   기본(파라미터 없는) DropLoot()/DropRewards()는 이 오브젝트(몬스터 루트)의 transform.position을
//   기준으로 흩뿌립니다. 몬스터 루트 위치가 원하는 지점이 아니라면(예: MiddleSlimeBoss처럼 모델이
//   커서 미리 배치해둔 별도의 빈 오브젝트 위치에서 튀어나오게 하고 싶은 경우), 대신
//   DropLoot(customOrigin)/DropRewards(customOrigin)를 호출하세요 - dropHeight는 그 위치를
//   기준으로 그대로 더해집니다.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MonsterStats))]
public class LootDropper : MonoBehaviour
{
    [Header("드롭 테이블")]
    [Tooltip("이 몬스터가 죽었을 때 굴릴 드롭 테이블입니다. 비워두면(null) 이 몬스터는 아무것도 " +
              "드롭하지 않습니다 - 오류가 아니라 정상적인 설정입니다(예: 전리품이 없는 몬스터).")]
    public LootTable lootTable;

    [Header("드롭 연출")]
    [Tooltip("죽은 위치를 중심으로 전리품들이 흩뿌려질 반경(미터).")]
    public float scatterRadius = 1.5f;
    [Tooltip("전리품이 튀어나오기 시작하는 높이 오프셋(미터). 죽은 위치보다 살짝 위에서 시작해서 포물선을 그립니다.")]
    public float dropHeight = 0.5f;
    [Tooltip("착지 지점의 실제 바닥 높이를 찾기 위해 위에서 아래로 레이캐스트할 최대 거리(미터).")]
    public float groundRaycastDistance = 5f;
    [Tooltip("바닥으로 인식할 레이어. 보통 Ground/Terrain 레이어를 지정하세요. 비워두면(Nothing) " +
              "레이캐스트를 하지 않고 항상 죽은 위치와 같은 높이로 착지시킵니다.")]
    public LayerMask groundMask;

    [Header("경험치/골드 보상")]
    [Tooltip("경험치 오브젝트 프리팹입니다. RewardOrb(Type = Experience)가 붙어있어야 합니다.")]
    public GameObject expOrbPrefab;
    [Tooltip("경험치를 이 개수의 오브젝트로 나눠서 드롭합니다. MonsterStats.expReward를 최대한 균등하게 나눕니다.")]
    public int expOrbCount = 2;
    [Tooltip("골드 오브젝트 프리팹입니다. RewardOrb(Type = Gold)가 붙어있어야 합니다.")]
    public GameObject goldOrbPrefab;
    [Tooltip("골드를 이 개수의 오브젝트로 나눠서 드롭합니다. MonsterStats.goldReward를 최대한 균등하게 나눕니다.")]
    public int goldOrbCount = 3;

    private MonsterStats stats;

    private void Awake()
    {
        stats = GetComponent<MonsterStats>();
    }

    /// <summary>드롭 테이블을 굴려서, 나온 전리품들을 이 오브젝트의 현재 위치를 중심으로 흩뿌립니다.
    /// 몬스터의 사망 처리(MonsterFSM.ChangeState의 State.Die, MiddleSlimeBoss.Die())에서, 오브젝트가
    /// 파괴되기 전에(죽은 위치 정보가 아직 유효할 때) 호출하세요. lootTable이 비어있으면 아무것도
    /// 드롭하지 않고 조용히 리턴합니다 - "드롭 없음"은 정상적인 디자인 선택이라 이 경우는 경고를
    /// 띄우지 않습니다. 몬스터 루트가 아닌 다른 위치를 기준으로 흩뿌리고 싶다면 DropLoot(Vector3)를
    /// 대신 호출하세요.</summary>
    public void DropLoot()
    {
        DropLoot(transform.position);
    }

    /// <summary>DropLoot()와 동일하지만, 이 오브젝트의 transform.position 대신 origin을 기준으로
    /// 흩뿌립니다 - MiddleSlimeBoss처럼 미리 배치해둔 별도의 지점에서 전리품이 나오게 하고 싶을 때
    /// 씁니다(MiddleSlimeBoss.Die() 참고).</summary>
    public void DropLoot(Vector3 origin)
    {
        if (lootTable == null) return;

        List<LootDropResult> drops = lootTable.RollDrops();

        foreach (LootDropResult drop in drops)
        {
            Vector3 spawnPosition = origin + Vector3.up * dropHeight;
            LootPickup pickup = LootPickup.Spawn(drop.item, drop.amount, spawnPosition);
            pickup.Launch(GetScatterGroundPosition(origin));
        }
    }

    /// <summary>MonsterStats에 설정된 경험치/골드 보상을 각각 expOrbCount/goldOrbCount개의 RewardOrb로
    /// 나눠서 죽은 위치 주변에 흩뿌립니다. LootPickup과 달리 상호작용 없이 자동으로 플레이어에게
    /// 흡수됩니다(RewardOrb 참고). 몬스터의 사망 처리에서 DropLoot()와 같이 호출해주세요. 보상이
    /// 0이면(expReward/goldReward가 0 이하) 해당 종류는 아무것도 만들지 않습니다 - "보상 없음"도
    /// 정상적인 설정입니다. 몬스터 루트가 아닌 다른 위치를 기준으로 흩뿌리고 싶다면 DropRewards(Vector3)를
    /// 대신 호출하세요.</summary>
    public void DropRewards()
    {
        DropRewards(transform.position);
    }

    /// <summary>DropRewards()와 동일하지만, 이 오브젝트의 transform.position 대신 origin을 기준으로
    /// 흩뿌립니다(DropLoot(Vector3)와 같은 이유).</summary>
    public void DropRewards(Vector3 origin)
    {
        SpawnOrbs(expOrbPrefab, stats.expReward, expOrbCount, origin);
        SpawnOrbs(goldOrbPrefab, stats.goldReward, goldOrbCount, origin);
    }

    /// <summary>totalAmount를 orbCount개의 오브젝트로 최대한 균등하게 나눠서 스폰합니다 (나머지는
    /// 앞쪽 오브젝트들에 하나씩 더 배분합니다 - 예: 총 10을 3개로 나누면 4, 3, 3).</summary>
    private void SpawnOrbs(GameObject prefab, int totalAmount, int orbCount, Vector3 origin)
    {
        if (totalAmount <= 0 || orbCount <= 0) return;

        int baseAmount = totalAmount / orbCount;
        int remainder = totalAmount % orbCount;

        for (int i = 0; i < orbCount; i++)
        {
            int amount = baseAmount + (i < remainder ? 1 : 0);
            if (amount <= 0) continue;

            Vector3 spawnPosition = origin + Vector3.up * dropHeight;
            RewardOrb orb = RewardOrb.Spawn(prefab, amount, spawnPosition);
            orb.Launch(GetScatterGroundPosition(origin));
        }
    }

    /// <summary>origin을 중심으로 scatterRadius 안의 무작위 지점을 고르고, groundMask가 지정되어
    /// 있으면 그 지점에서 실제 바닥 높이를 레이캐스트로 찾아 착지 지점을 정합니다. groundMask가
    /// 없거나 레이캐스트가 아무것도 못 맞히면 origin과 같은 높이를 그대로 사용합니다.</summary>
    private Vector3 GetScatterGroundPosition(Vector3 origin)
    {
        Vector2 randomOffset = Random.insideUnitCircle * scatterRadius;
        Vector3 point = origin + new Vector3(randomOffset.x, 0f, randomOffset.y);

        if (groundMask.value != 0)
        {
            Vector3 rayStart = point + Vector3.up * (groundRaycastDistance * 0.5f);
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, groundRaycastDistance, groundMask))
            {
                return hit.point;
            }
        }

        return point;
    }
}