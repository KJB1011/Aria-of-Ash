// ============================================================================
// PlayerInventory.cs
// ----------------------------------------------------------------------------
// 플레이어가 갖고 있는 아이템(전리품 등)을 종류별로 개수를 합쳐서(스택) 들고 있는 인벤토리
// 데이터입니다. LootPickup.Interact()가 전리품을 주울 때 AddItem()을 호출해서 여기에 쌓입니다.
// UIInventory가 OnInventoryChanged 이벤트를 구독해서, 내용이 바뀔 때마다(=아이템을 얻을 때마다)
// 화면을 새로 그립니다 - 매 프레임 폴링하지 않고 실제로 바뀐 순간에만 갱신됩니다.
//
// [씬 준비]
//   Player 오브젝트에 이 스크립트를 추가하세요. 씬에 하나만 있는 컴포넌트라, 다른 스크립트에서는
//   PlayerInventory.Instance로 바로 접근합니다 (SoundManager/UIIngameLoot 등과 같은 패턴).
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>인벤토리 한 칸입니다. 같은 LootItemData는 새 칸을 만들지 않고 amount만 늘어납니다.</summary>
[Serializable]
public class InventorySlot
{
    public LootItemData item;
    public int amount;
}

public class PlayerInventory : MonoBehaviour
{
    /// <summary>씬에 Player 하나에만 붙는 컴포넌트라, 다른 스크립트에서 여기로 바로 접근합니다.</summary>
    public static PlayerInventory Instance { get; private set; }

    /// <summary>인벤토리 내용이 바뀔 때마다(아이템 추가 등) 호출됩니다. UI가 이걸 구독해서
    /// 필요한 순간에만 다시 그리도록 하기 위한 이벤트입니다.</summary>
    public event Action OnInventoryChanged;

    /// <summary>현재 인벤토리에 들어있는 모든 칸입니다.</summary>
    public IReadOnlyList<InventorySlot> Slots => slots;

    private readonly List<InventorySlot> slots = new List<InventorySlot>();

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>item을 amount만큼 인벤토리에 추가합니다. 이미 갖고 있는 아이템이면(같은 LootItemData면)
    /// 기존 칸의 개수만 늘리고, 처음 얻는 아이템이면 새 칸을 만듭니다.</summary>
    public void AddItem(LootItemData item, int amount)
    {
        InventorySlot slot = FindSlot(item);
        if (slot != null)
        {
            slot.amount += amount;
        }
        else
        {
            slots.Add(new InventorySlot { item = item, amount = amount });
        }

        OnInventoryChanged?.Invoke();
    }

    /// <summary>item을 현재 몇 개 갖고 있는지 반환합니다. 아예 갖고 있지 않으면(칸이 없으면) 0입니다.
    /// 한계돌파/스킬 강화 등 재료 소모 전에 충분한지 확인하는 용도로 씁니다(UICharacterInfo 참고).</summary>
    public int GetItemCount(LootItemData item)
    {
        InventorySlot slot = FindSlot(item);
        return slot != null ? slot.amount : 0;
    }

    /// <summary>item을 amount만큼 소모합니다. 보유 수량이 amount보다 적으면 아무 것도 바꾸지 않고 false를
    /// 반환합니다 - 호출하는 쪽(한계돌파 등)이 미리 GetItemCount()로 충분한지 확인했더라도 이중으로
    /// 안전하게 막아줍니다. 성공하면 amount만큼 줄이고(0이 되면 그 칸을 아예 없앰) true를 반환합니다.</summary>
    public bool RemoveItem(LootItemData item, int amount)
    {
        InventorySlot slot = FindSlot(item);
        if (slot == null || slot.amount < amount) return false;

        slot.amount -= amount;
        if (slot.amount <= 0)
        {
            slots.Remove(slot);
        }

        OnInventoryChanged?.Invoke();
        return true;
    }

    private InventorySlot FindSlot(LootItemData item)
    {
        foreach (InventorySlot slot in slots)
        {
            if (slot.item == item) return slot;
        }
        return null;
    }
}