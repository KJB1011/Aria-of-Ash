// ============================================================================
// UITrash.cs
// ----------------------------------------------------------------------------
// 인벤토리에서 아이템을 선택한 채로 버리기 버튼을 눌렀을 때 뜨는, 버릴 개수를 정하는 팝업입니다.
// UINotice/UIYesNo와 같은 이유로 static Instance + 자체 보이기/숨기기 관리 방식입니다.
//
// [사용법]
//   UIInventory 등에서 아이템 칸을 선택한 뒤 버리기 버튼을 누르면
//   UITrash.Instance.Show(선택된_InventorySlot)을 호출해주면 됩니다. 확인을 누르면 그 시점에
//   정해진 개수만큼 PlayerInventory.Instance.RemoveItem()을 호출해서 실제로 버립니다.
//   [주의] 지금 UIInventory에는 아직 "칸을 클릭해서 선택하는" 기능 자체가 없습니다
//   (UIInventory.cs의 "[주의 - 아직 안 된 것]" 참고) - 그 선택 기능을 만들 때 버리기 버튼에서
//   이 Show()를 호출하도록 연결해주시면 됩니다.
//
// [수량 조절]
//   가운데 인풋필드(_trashCount)에 직접 숫자를 입력하거나, 양옆의 +/- 버튼으로 1씩 조절할 수
//   있습니다. 항상 1~보유수량 범위로 자동 클램프됩니다(0개나 보유 수량보다 많이 버릴 수 없음).
//   인풋필드에 숫자가 아닌 값을 입력하거나 비워두면 1로 취급합니다.
//
// [ESC로 닫기]
//   UINotice/UIYesNo와 동일하게 UICanvas가 Escape를 판단해서 취소(ClickCancelButton)와 똑같이
//   처리해줍니다 - 즉 아이템은 버려지지 않고 그냥 닫힙니다.
//
// [게임을 멈추는 방식]
//   UINotice/UIYesNo와 완전히 동일합니다(Show() 시점 값을 저장했다가 닫을 때 복원).
//
// [씬 준비]
//   1) 팝업 오브젝트(항상 활성화 상태로 두세요)에 이 스크립트와 CanvasGroup을 붙이세요.
//   2) 아이템 아이콘을 표시할 Image를 Img Icon 필드에, 현재 보유 수량을 표시할 TextMeshProUGUI를
//      Txt Owned Amount 필드에 연결하세요(둘 다 선택 사항 - 안 붙여도 나머지 기능은 정상 동작합니다).
//   3) 수량 인풋필드를 Trash Count 필드에 연결하세요.
//   4) +/- 버튼의 OnClick에 각각 ClickPlus()/ClickMinus()를, 확인/취소 버튼에 각각
//      ClickOKButton()/ClickCancelButton()을 연결하세요.
//   5) 씬에 이 스크립트를 가진 오브젝트가 정확히 하나 있어야 합니다.
// ============================================================================

using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class UITrash : MonoBehaviour
{
    public static UITrash Instance { get; private set; }

    [SerializeField] TMP_InputField _trashCount;
    [SerializeField] Image _imgIcon;
    [SerializeField] TextMeshProUGUI _txtOwnedAmount;

    [Header("표시/숨김")]
    public float fadeDuration = 0.15f;

    private CanvasGroup canvasGroup;
    private Tween fadeTween;
    private bool isOpen;

    private CursorLockMode previousCursorLockState;
    private bool previousCursorVisible;
    private float previousTimeScale;

    // 지금 버리려는 대상입니다. Show()에서 설정되고, 확인을 누르는 순간에만 실제로 소모합니다.
    private LootItemData targetItem;
    private int maxAmount;
    private int currentAmount;

    /// <summary>지금 열려있는지 여부입니다. UICanvas가 Escape 처리 우선순위를 판단할 때 확인합니다.</summary>
    public bool IsOpen => isOpen;

    private void Awake()
    {
        Instance = this;

        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void OnEnable()
    {
        if (_trashCount != null) _trashCount.onEndEdit.AddListener(OnInputEndEdit);
    }

    private void OnDisable()
    {
        if (_trashCount != null) _trashCount.onEndEdit.RemoveListener(OnInputEndEdit);
    }

    /// <summary>slot이 가리키는 아이템을 버리는 팝업을 엽니다(보유 수량 = slot.amount가 최대치).
    /// 수량은 1부터 시작합니다 - 전부 버리고 싶으면 +버튼을 눌러 최대치까지 올리거나 인풋필드에
    /// 직접 입력하면 됩니다.</summary>
    public void Show(InventorySlot slot)
    {
        if (slot == null || slot.item == null) return;
        Show(slot.item, slot.amount);
    }

    /// <summary>item을 최대 maxCount개까지 버릴 수 있는 팝업을 엽니다.</summary>
    public void Show(LootItemData item, int maxCount)
    {
        if (item == null) return;

        targetItem = item;
        maxAmount = Mathf.Max(1, maxCount);
        currentAmount = 1;

        if (_imgIcon != null) _imgIcon.sprite = item.icon;
        if (_txtOwnedAmount != null) {
            string str = "x" + maxAmount.ToString();
            _txtOwnedAmount.text = str;
             }
        RefreshCountText();

        if (isOpen) return;
        isOpen = true;

        previousCursorLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        fadeTween?.Kill();
        fadeTween = canvasGroup.DOFade(1f, fadeDuration).SetUpdate(true);
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    /// <summary>수량을 1 늘립니다(보유 수량을 넘지 않음).</summary>
    public void ClickPlus()
    {
        SetAmount(currentAmount + 1);
    }

    /// <summary>수량을 1 줄입니다(최소 1).</summary>
    public void ClickMinus()
    {
        SetAmount(currentAmount - 1);
    }

    /// <summary>확인 버튼 OnClick에 연결하세요. 지금 정해진 수량만큼 실제로 버립니다
    /// (PlayerInventory.RemoveItem 호출) - 이후 인벤토리 UI는 각자 구독 중인 OnInventoryChanged
    /// 이벤트로 자동 갱신됩니다.</summary>
    public void ClickOKButton()
    {
        if (targetItem != null)
        {
            PlayerInventory.Instance.RemoveItem(targetItem, currentAmount);
        }
        Close();
    }

    /// <summary>취소 버튼 OnClick에 연결하세요. 아무 것도 버리지 않고 닫습니다. UICanvas의 Escape
    /// 처리에서도 이 함수를 그대로 호출합니다.</summary>
    public void ClickCancelButton()
    {
        Close();
    }

    private void Close()
    {
        if (!isOpen) return;
        isOpen = false;

        targetItem = null;

        Cursor.lockState = previousCursorLockState;
        Cursor.visible = previousCursorVisible;
        Time.timeScale = previousTimeScale;

        fadeTween?.Kill();
        fadeTween = canvasGroup.DOFade(0f, fadeDuration).SetUpdate(true);
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void OnInputEndEdit(string text)
    {
        if (!int.TryParse(text, out int parsed))
        {
            parsed = 1;
        }
        SetAmount(parsed);
    }

    private void SetAmount(int amount)
    {
        currentAmount = Mathf.Clamp(amount, 1, maxAmount);
        RefreshCountText();
    }

    private void RefreshCountText()
    {
        if (_trashCount != null) _trashCount.SetTextWithoutNotify(currentAmount.ToString());
    }
}