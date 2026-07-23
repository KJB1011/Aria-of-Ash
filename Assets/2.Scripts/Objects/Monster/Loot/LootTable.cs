// ============================================================================
// LootTable.cs
// ----------------------------------------------------------------------------
// 몬스터 한 종류가 죽었을 때 무엇을 얼마나 떨어뜨릴지 정의하는 ScriptableObject입니다.
// 여러 항목(Entry)을 담고 있고, RollDrops()를 호출하면 각 항목을 "독립적으로" 확률
// 판정해서 실제로 이번에 드롭될 아이템 목록을 돌려줍니다 - 원신처럼 한 마리를 잡아도
// 여러 종류의 전리품이 동시에 떨어질 수 있는 방식입니다 (하나만 뽑는 룰렛 방식이 아님).
//
// [애셋 만들기]
//   Project 창에서 우클릭 → Create → Loot > Loot Table 로 새 드롭 테이블 애셋을 만드세요.
//   예) "LootTable_Slime", "LootTable_MiddleSlimeBoss" 등 몬스터(종류)마다 하나씩 만들어서
//   LootDropper의 Loot Table 필드에 연결하면 됩니다. 같은 테이블을 여러 몬스터가 공유해도 됩니다.
//
// [항목(Entry) 설정]
//   item       : 드롭할 LootItemData.
//   dropChance : 이 항목이 드롭될 확률 (0~1). 0.3이면 30% 확률로 이 항목이 드롭됩니다.
//                항목마다 독립적으로 판정되므로, 항목이 여러 개면 이번 드롭에 전부 나올 수도,
//                하나도 안 나올 수도 있습니다.
//   minAmount/maxAmount : 드롭이 확정되면 이 범위(포함) 안에서 무작위 개수를 정합니다.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>LootTable.RollDrops()의 결과 항목 하나입니다. 어떤 아이템이 몇 개 드롭됐는지를 담습니다.</summary>
public readonly struct LootDropResult
{
    public readonly LootItemData item;
    public readonly int amount;

    public LootDropResult(LootItemData item, int amount)
    {
        this.item = item;
        this.amount = amount;
    }
}

[CreateAssetMenu(fileName = "LootTable_New", menuName = "Loot/Loot Table")]
public class LootTable : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public LootItemData item;
        [Range(0f, 1f)]
        [Tooltip("이 항목이 드롭될 확률입니다 (0~1). 항목마다 독립적으로 판정되므로 여러 항목이 " +
                  "동시에 드롭되거나, 전부 안 나올 수도 있습니다.")]
        public float dropChance = 1f;
        [Tooltip("드롭이 확정됐을 때의 최소 개수(포함).")]
        public int minAmount = 1;
        [Tooltip("드롭이 확정됐을 때의 최대 개수(포함). minAmount와 같으면 항상 고정 개수입니다.")]
        public int maxAmount = 1;
    }

    [Tooltip("이 테이블에 속한 드롭 항목들입니다. 각 항목은 독립적으로 확률 판정됩니다.")]
    public List<Entry> entries = new List<Entry>();

    /// <summary>각 항목을 독립적으로 확률 판정해서, 이번에 실제로 드롭될 (아이템, 개수) 목록을 돌려줍니다.
    /// item이 비어있는 항목은 건너뜁니다. 드롭이 하나도 없으면 빈 리스트를 돌려줍니다(정상 - "전리품 없음"인
    /// 몬스터도 있을 수 있습니다).</summary>
    public List<LootDropResult> RollDrops()
    {
        List<LootDropResult> results = new List<LootDropResult>();

        foreach (Entry entry in entries)
        {
            if (entry.item == null) continue;
            if (UnityEngine.Random.value > entry.dropChance) continue;

            int amount = UnityEngine.Random.Range(entry.minAmount, entry.maxAmount + 1);
            if (amount <= 0) continue;

            results.Add(new LootDropResult(entry.item, amount));
        }

        return results;
    }
}