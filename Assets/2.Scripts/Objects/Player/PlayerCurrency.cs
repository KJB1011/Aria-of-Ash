// ============================================================================
// PlayerCurrency.cs
// ----------------------------------------------------------------------------
// 플레이어가 보유한 골드를 관리하는 컴포넌트입니다. RewardOrb(골드 오브젝트)가 플레이어에게
// 흡수되는 순간 AddGold()를 호출해서 여기에 쌓입니다. 인벤토리 아이템처럼 칸을 차지하는 게
// 아니라 원신의 모라처럼 별도의 숫자 하나로 관리되는 재화라, PlayerInventory와 분리했습니다.
//
// [씬 준비]
//   Player 오브젝트에 이 스크립트를 추가하세요. 씬에 하나만 있는 컴포넌트라, 다른 스크립트에서는
//   PlayerCurrency.Instance로 바로 접근합니다 (PlayerInventory 등과 같은 패턴).
// ============================================================================

using System;
using UnityEngine;

public class PlayerCurrency : MonoBehaviour
{
    /// <summary>씬에 Player 하나에만 붙는 컴포넌트라, 다른 스크립트에서 여기로 바로 접근합니다.</summary>
    public static PlayerCurrency Instance { get; private set; }

    [Tooltip("현재 보유 골드입니다.")]
    public int gold = 0;

    /// <summary>골드가 바뀔 때마다(획득 등) 호출됩니다. 골드 표시 UI가 생기면 이걸 구독해서 필요한
    /// 순간에만 갱신하면 됩니다 (지금은 구독하는 UI가 없어도 문제 없습니다).</summary>
    public event Action<int> OnGoldChanged;

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>골드를 amount만큼 추가합니다.</summary>
    public void AddGold(int amount)
    {
        gold += amount;
        OnGoldChanged?.Invoke(gold);
    }

    /// <summary>골드를 amount만큼 소모합니다. 충분한지(gold >= amount) 확인은 호출하는 쪽(한계돌파 등)에서
    /// 먼저 하세요 - 여기서는 이중 안전장치로 0 밑으로 내려가지 않게만 막습니다.</summary>
    public void SpendGold(int amount)
    {
        gold = Mathf.Max(0, gold - amount);
        OnGoldChanged?.Invoke(gold);
    }
}