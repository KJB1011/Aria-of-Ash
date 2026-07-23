// ============================================================================
// UIInventoryBar.cs
// ----------------------------------------------------------------------------
// UIInventory(인벤토리 창)의 Content 안에 하나씩 추가되는 항목 프리팹입니다. 인벤토리 칸
// 하나(아이템 종류 하나)마다 이 프리팹이 Instantiate되어 아이콘과 개수를 보여주고, 선택된
// 상태면 _imgSelected가 켜집니다.
//
// [클릭으로 선택하기]
//   이 칸을 클릭하면 OnClicked 이벤트가 발생하고, UIInventory.HandleBarClicked()가 이를 구독해서
//   "지금 어떤 칸이 선택됐는지"를 관리합니다(같은 칸을 다시 클릭하면 선택 해제, 다른 칸을 클릭하면
//   기존 선택은 풀리고 새 칸이 선택됨). 선택된 칸의 Slot을 UIInventory.ClickTrashButton()이
//   UITrash.Show()에 그대로 넘겨서 버리기 흐름으로 이어집니다.
//
// [프리팹 준비]
//   1) Image(_imgSelected/_imgItem)와 TextMeshProUGUI(_txtItemCount)를 인스펙터에서 연결해두세요.
//   2) 프리팹 루트(또는 클릭 영역)에 Button 컴포넌트를 추가하고, OnClick에 이 스크립트의
//      OnClickBar()를 연결하세요 - 다른 UI들과 동일하게 Button → OnClick → 함수 방식입니다.
//   3) UIInventory가 Instantiate 직후 SetInventoryBar()로 아이콘/개수/슬롯 정보를,
//      SetSelected()로 선택 표시를 설정합니다.
// ============================================================================

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIInventoryBar : MonoBehaviour
{
    [SerializeField] Image _imgSelected;
    [SerializeField] Image _imgItem;
    [SerializeField] TextMeshProUGUI _txtItemCount; // "x64" 이런식으로 앞에 x가 붙어서 표시됨

    /// <summary>이 칸을 클릭하면(Button OnClick → OnClickBar()) 발생하는 이벤트입니다. UIInventory가
    /// 구독해서 어떤 칸이 선택됐는지 추적합니다.</summary>
    public event Action<UIInventoryBar> OnClicked;

    /// <summary>이 칸이 나타내는 인벤토리 슬롯입니다. SetInventoryBar()에서 설정되고, 선택된 뒤
    /// 버리기 등 후속 처리에서 다시 꺼내 씁니다.</summary>
    public InventorySlot Slot { get; private set; }

    /// <summary>아이콘/개수(예: "x64")와 이 칸이 나타내는 슬롯을 설정합니다.</summary>
    public void SetInventoryBar(InventorySlot slot)
    {
        Slot = slot;
        _imgItem.sprite = slot.item.icon;
        _txtItemCount.text = $"x{slot.amount}";
    }

    /// <summary>지금 선택되어 있는지 표시를 켜고 끕니다.</summary>
    public void SetSelected(bool isOn)
    {
        _imgSelected.enabled = isOn;
    }

    /// <summary>이 칸의 Button OnClick에 연결하세요.</summary>
    public void OnClickBar()
    {
        OnClicked?.Invoke(this);
    }
}