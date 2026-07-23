// ============================================================================
// UIIngameLoot.cs
// ----------------------------------------------------------------------------
// 화면 왼쪽에 뜨는 "전리품 획득 로그" UI입니다. 전리품을 주울 때마다 LootPickup이
// UIIngameLoot.Instance.AddLoot()를 호출해서 이 UI의 Content 밑으로 UILootBar 항목이
// 하나씩 차례로 추가됩니다. 평소에는 숨겨져 있다가 첫 항목이 추가되는 순간 페이드인으로
// 나타납니다.
//
// [Bar마다 개별 소멸 - 패널은 Bar가 0개가 되면 숨김]
//   전체 패널에 걸린 하나의 숨김 타이머 대신, 각 UILootBar가 자기 자신의 소멸 타이머(기본
//   5초, UILootBar.lifetime)를 갖고 스스로 사라집니다 - 그래서 항목마다 "그 항목이 추가된
//   시점부터" 5초 후에 각자 사라지고, 한꺼번에 다 같이 사라지지 않습니다. Bar가 스스로
//   사라질 때 ReturnBarToPool()을 호출해주면, 그 결과 목록에 남은 Bar가 하나도 없을 때만
//   패널 전체가 페이드아웃됩니다.
//
// [오브젝트 풀링]
//   Bar가 많이 생성/소멸되는 로그 UI라, Instantiate/Destroy 대신 GameObjectPool로 재사용합니다
//   (VFXManager/DamageNumberManager와 같은 방식). Bar Prefab 필드에 연결한 프리팹을 대상으로
//   내부적으로 풀을 만들고, AddLoot()마다 풀에서 꺼내 쓰고 ReturnBarToPool()에서 반납합니다.
//
// [원래 스크립트와 달라진 점]
//   _viewIngameLoot를 UI Toolkit의 ScrollView 대신 UGUI의 ScrollRect로 바꿨습니다. UILootBar가
//   Image/TextMeshProUGUI를 쓰는 일반 GameObject 프리팹(UGUI)이라, UI Toolkit의 VisualElement
//   트리에는 애초에 들어갈 수 없습니다. 씬에서 이 필드에 UGUI Scroll View의 ScrollRect 컴포넌트를
//   연결해주세요.
//
// [씬 준비]
//   1) 화면 왼쪽에 배치할 패널(Canvas 하위 오브젝트)에 이 스크립트와 CanvasGroup을 붙이세요
//      (CanvasGroup은 RequireComponent로 자동 추가됩니다) - 알파값으로 페이드인/아웃합니다.
//   2) 그 안에 Vertical Layout Group 등을 붙인 Content를 가진 ScrollRect를 만들고
//      View Ingame Loot 필드에 연결하세요.
//   3) UILootBar 프리팹을 Bar Prefab 필드에 연결하세요. 소멸까지 걸리는 시간은 프리팹 쪽의
//      UILootBar.lifetime(기본 5초)에서 조절합니다.
//   4) 씬에 이 스크립트를 가진 오브젝트가 정확히 하나 있어야 합니다 - LootPickup 등 다른 곳에서
//      UIIngameLoot.Instance로 바로 찾아서 호출합니다.
// ============================================================================

using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class UIIngameLoot : MonoBehaviour
{
    [SerializeField] ScrollRect _viewIngameLoot;
    [SerializeField] UILootBar _barPrefab;

    [Header("표시/숨김")]
    [Tooltip("페이드인/아웃에 걸리는 시간(초).")]
    public float fadeDuration = 0.25f;

    [Header("오브젝트 풀")]
    [Tooltip("미리 만들어서 대기시켜둘 Bar 개수. 처음 전리품을 주울 때 생기는 순간적인 끊김(hitch)을 막아줍니다.")]
    public int prewarmCount = 5;
    [Tooltip("대기 풀에 보관할 수 있는 최대 Bar 개수. 초과분은 반납 시 Destroy됩니다.")]
    public int maxPoolSize = 50;

    /// <summary>씬에 하나만 배치해두고 쓰는 UI라, LootPickup 등 다른 스크립트에서 여기로 바로 접근합니다.</summary>
    public static UIIngameLoot Instance { get; private set; }

    private CanvasGroup canvasGroup;
    private GameObjectPool barPool;
    private readonly List<UILootBar> activeBars = new List<UILootBar>();
    private Tween fadeTween;
    private bool isVisible;

    private void Awake()
    {
        Instance = this;

        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Transform poolRoot = new GameObject("Pool_LootBar").transform;
        poolRoot.SetParent(transform, false);
        barPool = new GameObjectPool(_barPrefab.gameObject, poolRoot, prewarmCount, maxPoolSize);
    }

    /// <summary>전리품 하나를 로그에 추가합니다. LootPickup.Interact()에서 획득 처리 직전에 호출합니다.</summary>
    public void AddLoot(Sprite icon, string displayName)
    {
        GameObject instance = barPool.Get(Vector3.zero, Quaternion.identity, _viewIngameLoot.content);

        // Bar Prefab에는 반드시 UILootBar가 붙어있어야 합니다 - 없으면 여기서 바로
        // NullReferenceException이 나서, 프리팹 연결을 빠뜨렸다는 게 바로 드러납니다.
        UILootBar bar = instance.GetComponent<UILootBar>();
        bar.SetLootBar(icon, displayName);
        activeBars.Add(bar);

        Show();
    }

    /// <summary>UILootBar가 자기 소멸 타이머(기본 5초)가 다 되면 스스로 호출합니다. 목록에서 빼고
    /// 풀에 반납한 뒤, 남은 Bar가 하나도 없으면 패널 전체를 숨깁니다.</summary>
    public void ReturnBarToPool(UILootBar bar)
    {
        activeBars.Remove(bar);
        barPool.Release(bar.gameObject);

        if (activeBars.Count == 0)
        {
            Hide();
        }
    }

    private void Show()
    {
        if (isVisible) return;
        isVisible = true;

        fadeTween?.Kill();
        fadeTween = canvasGroup.DOFade(1f, fadeDuration).SetUpdate(true); // 다른 팝업(UIInventory 등)이 게임을 멈춰도(Time.timeScale = 0) 이 페이드는 얼어붙지 않습니다.
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    private void Hide()
    {
        isVisible = false;

        fadeTween?.Kill();
        fadeTween = canvasGroup.DOFade(0f, fadeDuration).SetUpdate(true);
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }
}