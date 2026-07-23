// ============================================================================
// UILootBar.cs
// ----------------------------------------------------------------------------
// UIIngameLoot(화면 왼쪽 전리품 획득 로그)의 Content 안에 하나씩 추가되는 항목 프리팹입니다.
// 전리품을 주울 때마다 오브젝트 풀에서 하나 꺼내져 아이콘과 이름(+개수)을 보여주다가, lifetime
// (기본 5초)이 지나면 스스로 UIIngameLoot.ReturnBarToPool()을 호출해 풀로 돌아갑니다 - 매니저는
// "언제 사라질지"를 몰라도 되고, 이 컴포넌트가 자기 생애주기를 알아서 책임지는 구조입니다
// (DamageNumberPopup과 같은 패턴입니다).
//
// [프리팹 준비]
//   Image(_imgLoot)와 TextMeshProUGUI(_txtItemName)를 인스펙터에서 연결해두면 끝입니다.
//   UIIngameLoot.AddLoot()이 풀에서 꺼낸 직후 SetLootBar()를 호출해서 내용을 채워줍니다.
//   Lifetime 값도 이 프리팹의 인스펙터에서 조절할 수 있습니다.
//
// [시간 정지와의 관계]
//   소멸 타이머는 Time.unscaledDeltaTime을 씁니다 - 다른 팝업(UIInventory 등)이 게임을
//   멈춰도(Time.timeScale = 0) 이 타이머는 계속 정상적으로 흘러서, 인벤토리를 열어두는 동안
//   로그가 안 사라지고 멈춰버리는 일이 없습니다.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UILootBar : MonoBehaviour, IPoolable
{
    [SerializeField] Image _imgLoot;
    [SerializeField] TextMeshProUGUI _txtItemName;

    [Header("자동 소멸")]
    [Tooltip("풀에서 꺼내져 화면에 나타난 뒤 이 시간(초)이 지나면 자동으로 사라지고(풀에 반납) 목록에서 빠집니다.")]
    public float lifetime = 5f;

    private float timer;
    private bool isCountingDown;

    /// <summary>아이콘과 이름(개수 포함)을 설정합니다. UIIngameLoot.AddLoot()이 풀에서 꺼낸 직후 호출합니다.
    /// itemID 대신 Sprite를 직접 받도록 했습니다 - LootItemData가 아이콘 Sprite를 이미 갖고 있어서,
    /// ID로 다시 조회하는 별도의 아이템 데이터베이스가 필요 없습니다.</summary>
    public void SetLootBar(Sprite icon, string name)
    {
        _imgLoot.sprite = icon;
        _txtItemName.text = name;
    }

    /// <summary>IPoolable 구현. 풀에서 꺼내져 화면에 나타난 직후 호출됩니다 - 소멸 타이머를 처음부터 다시 시작합니다.</summary>
    public void OnGetFromPool()
    {
        timer = lifetime;
        isCountingDown = true;
    }

    /// <summary>IPoolable 구현. 풀로 반납되어 비활성화되기 직전 호출됩니다.</summary>
    public void OnReleaseToPool()
    {
        isCountingDown = false;
    }

    private void Update()
    {
        if (!isCountingDown) return;

        timer -= Time.unscaledDeltaTime;
        if (timer <= 0f)
        {
            isCountingDown = false; // 반납 요청이 같은 프레임에 두 번 나가지 않도록 먼저 꺼둡니다.
            UIIngameLoot.Instance.ReturnBarToPool(this);
        }
    }
}