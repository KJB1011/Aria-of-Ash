// ============================================================================
// LootItemData.cs
// ----------------------------------------------------------------------------
// 전리품 하나의 "종류"를 정의하는 ScriptableObject입니다. 실제로 씬에 떨어지는
// 오브젝트(LootPickup)가 아니라, "이 아이템이 뭔지"에 대한 데이터만 담습니다 -
// 원신의 아이템 하나하나가 갖는 아이콘/이름/설명과 같은 개념입니다.
//
// [애셋 만들기]
//   Project 창에서 우클릭 → Create → Loot > Loot Item 으로 새 아이템 애셋을 만드세요.
//   예) "Item_SlimeGel", "Item_GoldCoin" 등 아이템마다 하나씩 만들어서 재사용합니다.
//
// [필드]
//   itemId          : 코드/세이브 데이터 등에서 아이템을 구분할 고유 ID (예: "slime_gel").
//                      씬을 넘나들며 저장/복원할 일이 생기면 이 값을 키로 쓰세요.
//   displayName     : UI에 표시할 이름 (예: "슬라임 젤리").
//   description     : 아이템 설명 (툴팁 등에 사용).
//   icon            : 인벤토리 UI 등에 쓸 아이콘 스프라이트.
//   worldPickupPrefab : 이 아이템이 필드에 떨어질 때 실제로 생성할 프리팹. LootPickup
//                      컴포넌트가 붙어있어야 합니다 (LootPickup.cs 참고). 프리팹 하나를
//                      여러 아이템이 공유해도 되고(예: 공용 "동전" 모델), 아이템마다 다른
//                      모델을 쓰고 싶으면 각자 다른 프리팹을 연결하면 됩니다.
// ============================================================================

using UnityEngine;

[CreateAssetMenu(fileName = "Item_New", menuName = "Loot/Loot Item")]
public class LootItemData : ScriptableObject
{
    [Header("식별")]
    [Tooltip("코드/세이브 데이터에서 아이템을 구분할 고유 ID입니다. 예: \"slime_gel\"")]
    public string itemId;

    [Header("표시 정보")]
    public string displayName;
    [TextArea(2, 4)]
    public string description;
    public Sprite icon;

    [Header("월드 프리팹")]
    [Tooltip("이 아이템이 필드에 떨어질 때 생성할 프리팹입니다. LootPickup 컴포넌트가 붙어있어야 합니다.")]
    public GameObject worldPickupPrefab;
}