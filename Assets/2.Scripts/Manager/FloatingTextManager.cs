// ============================================================================
// FloatingTextManager.cs
// ----------------------------------------------------------------------------
// 화면에 고정된 위치(기본: 화면 중앙에서 살짝 위쪽)에 알림 메세지를 잠깐 띄웠다가 사라지게 하는
// 매니저입니다. "레벨업!", "퀘스트 완료", "인벤토리가 가득 찼습니다" 같은, 특정 월드 좌표가 아니라
// 화면 자체에 고정으로 떠야 하는 알림용 텍스트에 씁니다(맞은 자리에 뜨는 DamageNumberManager와는
// 용도가 다릅니다 - 그쪽은 월드 좌표 기준 3D 텍스트, 이쪽은 화면 좌표 기준 UI 텍스트입니다).
//
// VFXManager/DamageNumberManager와 똑같은 이유로 오브젝트 풀링(GameObjectPool)을 사용합니다 -
// 알림이 짧은 시간 안에 연달아 뜰 수 있어서, 매번 Instantiate/Destroy하면 그때마다 GC 비용이
// 발생해 프레임 드랍의 원인이 됩니다.
//
// [프리팹 준비 - FloatingTextPopup.cs 상단 주석 참고]
//   VFXManager/DamageNumberManager와 똑같이 Resources 폴더 이름 규칙으로 프리팹을 불러옵니다.
//   1) Project 창에서 "Assets/Resources/HUD" 폴더를 만드세요(이미 DamageNumber 프리팹을 위해
//      만들어뒀다면 그 폴더를 그대로 씁니다 - Resources 폴더는 정확히 이 이름이어야 합니다).
//   2) 그 안에 알림 텍스트 프리팹을 하나 만들어 넣으세요 (기본 이름: "FloatingText",
//      Assets/Resources/HUD/FloatingText.prefab). 프리팹 자체를 어떻게 구성하는지는
//      FloatingTextPopup.cs 상단 주석을 참고하세요 - 여기서 중요한 건 그 프리팹의 RectTransform
//      Anchor/Pivot을 (0.5, 0.5)(정중앙 기준)로 맞춰둬야 아래 anchoredPosition 오프셋이 "화면
//      중앙에서 얼마나 떨어졌는지"로 정확히 해석된다는 점입니다.
//   3) 씬에 미리 배치해둘 필요 없습니다 - 아무 스크립트에서나 FloatingTextManager.Instance를
//      처음 호출하는 순간 자동으로 생성되고, 씬이 바뀌어도 파괴되지 않습니다(DontDestroyOnLoad).
//
// [Canvas - 자동 생성 또는 직접 지정]
//   targetCanvas를 비워두면 이 매니저가 처음 필요해지는 시점에 Screen Space - Overlay Canvas를
//   자동으로 하나 만들어서 씁니다(별도 씬 설정 없이 바로 동작). 이미 만들어둔 UI Canvas(다른
//   HUD와 같은 정렬 순서/스케일러 설정을 공유하고 싶은 경우)가 있다면, 인스펙터에서 그 Canvas를
//   targetCanvas에 직접 연결하세요 - 그러면 자동 생성 없이 그 Canvas 아래에 알림 텍스트가 뜹니다.
//
// [고정 위치 - anchoredPosition]
//   새로 뜨는 알림 메세지는 항상 이 하나의 자리(anchoredPosition)에서 시작합니다. 기본값(0, 200)은
//   화면 정중앙 기준으로 위쪽 200px 지점입니다 - "화면 중앙에서 살짝 위쪽"에 맞춘 기본값이니, 원하는
//   위치로 인스펙터에서 직접 조절하세요.
//
// [여러 개가 동시에 뜰 때 - 스택 쌓기]
//   Show()가 새 알림을 만들 때마다, 그 시점에 아직 화면에 떠 있는(페이드 인/유지/페이드 아웃 중인)
//   기존 알림들을 전부 stackSpacing(px)만큼 위로 밀어올립니다 - 그래서 여러 알림이 짧은 시간 안에
//   연달아 뜨면 최신 알림이 항상 anchoredPosition(맨 아래 고정 자리)에서 시작하고, 먼저 떴던
//   알림일수록 그 위에 순서대로 쌓입니다(각 FloatingTextPopup이 매 프레임 stackMoveSpeed로 그
//   목표 위치까지 부드럽게 밀려 올라갑니다 - FloatingTextPopup.AddStackOffset() 참고). 밀려 올라간
//   채로 자기 lifetime이 끝나면 평소처럼 페이드 아웃되어 사라지고, 그 뒤로도 새 알림이 뜨면 남은
//   알림들은 계속 위로 밀려 올라갑니다(사라진 알림이 있었다고 아래로 다시 채워지진 않습니다).
//   stackSpacing을 0으로 두면 예전처럼 전부 같은 자리에 겹쳐서 뜹니다.
//
// [사용 예시]
//   FloatingTextManager.Instance.Show("퀘스트 완료!");
//   FloatingTextManager.Instance.Show("인벤토리가 가득 찼습니다", Color.red);
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FloatingTextManager : MonoBehaviour
{
    private const string ResourceFolder = "HUD";

    [Header("설정")]
    [Tooltip("Resources/HUD/ 아래에 있는 알림 텍스트 프리팹 이름.")]
    public string prefabName = "FloatingText";
    [Tooltip("알림 텍스트가 뜰 UI Canvas입니다. 비워두면 Screen Space - Overlay Canvas를 자동으로 " +
              "하나 만들어서 씁니다 - 다른 UI와 같은 Canvas/정렬 순서를 공유하고 싶을 때만 직접 연결하세요.")]
    public Canvas targetCanvas;
    [Tooltip("알림 텍스트가 항상 뜨는 고정 위치입니다(프리팹 RectTransform의 anchoredPosition에 그대로 " +
              "대입됩니다). 프리팹의 Anchor/Pivot이 (0.5, 0.5)라는 전제로, 기본값(0, 200)은 화면 " +
              "정중앙에서 위로 200px 떨어진 지점입니다.")]
    public Vector2 anchoredPosition = new Vector2(0f, 200f);
    [Tooltip("미리 만들어서 대기시켜둘 인스턴스 개수.")]
    public int prewarmCount = 3;
    [Tooltip("대기 풀에 보관할 수 있는 최대 개수. 초과분은 반납 시 Destroy됩니다.")]
    public int maxPoolSize = 20;
    [Tooltip("켜두면 Show()/반납 등 동작을 콘솔에 로그로 남깁니다.")]
    public bool debugLog = false;

    [Header("스택 쌓기 (여러 개가 동시에 뜰 때)")]
    [Tooltip("새 알림이 뜰 때마다 그 시점에 아직 화면에 떠 있는 기존 알림들을 위로 밀어올리는 간격(px)입니다. " +
              "0으로 두면 예전처럼 전부 같은 자리(anchoredPosition)에 겹쳐서 뜹니다. 파일 상단 [여러 개가 동시에 " +
              "뜰 때 - 스택 쌓기] 참고.")]
    public float stackSpacing = 50f;

    // 지금 화면에 떠 있는(아직 풀에 반납되지 않은) 알림들입니다 - Show()가 새 알림을 추가할 때 이 목록의
    // 기존 항목들을 먼저 위로 밀어올린 뒤, 새 알림을 이 목록에 추가합니다. ReturnToPool()에서 제거됩니다.
    private readonly List<FloatingTextPopup> activePopups = new List<FloatingTextPopup>();

    private static FloatingTextManager instance;
    public static FloatingTextManager Instance
    {
        get
        {
            if (instance == null)
            {
                // 씬에 이미 배치해둔 인스턴스가 있으면 그걸 쓰고, 없으면 새로 만듭니다.
                instance = FindFirstObjectByType<FloatingTextManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("FloatingTextManager");
                    instance = go.AddComponent<FloatingTextManager>();
                }
            }
            return instance;
        }
    }

    private GameObject prefab;
    private GameObjectPool pool;
    private RectTransform poolRoot;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            // 씬 전환 등으로 인해 두 번째 FloatingTextManager가 생기면 기존 것을 유지하고 새로 생긴 걸 제거합니다.
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>고정 위치(anchoredPosition)에 메세지를 표시합니다. 색을 지정하지 않으면 프리팹에
    /// 설정해둔 기본 색을 그대로 씁니다.</summary>
    public GameObject Show(string message)
    {
        return Show(message, null);
    }

    /// <summary>고정 위치(anchoredPosition)에 메세지를 지정한 색으로 표시합니다.</summary>
    public GameObject Show(string message, Color? color)
    {
        GameObjectPool p = GetOrCreatePool();
        if (p == null) return null;

        RectTransform canvasTransform = GetCanvasTransform();
        if (canvasTransform == null) return null;

        GameObject spawned = p.Get(Vector3.zero, Quaternion.identity, canvasTransform);

        RectTransform rt = spawned.transform as RectTransform;
        if (rt != null)
        {
            rt.anchoredPosition = anchoredPosition;
        }

        FloatingTextPopup popup = spawned.GetComponent<FloatingTextPopup>();
        if (popup == null)
        {
            Debug.LogWarning($"[FloatingTextManager] '{prefabName}' 프리팹에 FloatingTextPopup 컴포넌트가 없습니다.", spawned);
            pool.Release(spawned);
            return null;
        }

        // 새 알림이 기본 자리(anchoredPosition)를 차지하기 전에, 지금 떠 있는 기존 알림들을 먼저
        // stackSpacing만큼 위로 밀어올립니다 - 그래야 최신 알림이 항상 맨 아래(기본 자리)에서 시작하고
        // 먼저 떴던 알림일수록 그 위에 순서대로 쌓입니다.
        foreach (FloatingTextPopup existing in activePopups)
        {
            existing.AddStackOffset(stackSpacing);
        }
        activePopups.Add(popup);

        popup.Play(message, color);

        if (debugLog) Debug.Log($"[FloatingTextManager] \"{message}\" 표시 (position={anchoredPosition})", spawned);

        return spawned;
    }

    /// <summary>FloatingTextPopup이 자기 애니메이션을 끝내고 스스로 반납할 때 호출합니다.
    /// 다른 곳에서 직접 호출할 일은 없습니다. 스택 목록(activePopups)에서도 함께 제거해서, 이후에
    /// 뜨는 새 알림이 이미 사라진 알림까지 밀어올리려 하지 않도록 합니다.</summary>
    public void ReturnToPool(GameObject instance)
    {
        FloatingTextPopup popup = instance.GetComponent<FloatingTextPopup>();
        if (popup != null) activePopups.Remove(popup);

        pool?.Release(instance);
    }

    private GameObjectPool GetOrCreatePool()
    {
        if (pool != null) return pool;

        if (prefab == null)
        {
            prefab = Resources.Load<GameObject>($"{ResourceFolder}/{prefabName}");
            if (prefab == null)
            {
                Debug.LogWarning($"[FloatingTextManager] 'Resources/{ResourceFolder}/{prefabName}' 프리팹을 찾을 수 없습니다. " +
                                  "파일 이름과 경로(Assets/Resources/HUD/ 바로 아래)를 확인해주세요.");
                return null;
            }
        }

        RectTransform canvasTransform = GetCanvasTransform();
        if (canvasTransform == null) return null;

        if (poolRoot == null)
        {
            GameObject rootGo = new GameObject("Pool_FloatingText", typeof(RectTransform));
            poolRoot = rootGo.GetComponent<RectTransform>();
            poolRoot.SetParent(canvasTransform, false);
        }

        pool = new GameObjectPool(prefab, poolRoot, prewarmCount, maxPoolSize);
        return pool;
    }

    /// <summary>targetCanvas가 지정되어 있으면 그 Canvas를, 비워져 있으면 자동으로 만들어둔(또는
    /// 지금 처음 만드는) Screen Space - Overlay Canvas를 반환합니다.</summary>
    private RectTransform GetCanvasTransform()
    {
        if (targetCanvas != null) return targetCanvas.transform as RectTransform;

        EnsureAutoCanvas();
        return targetCanvas != null ? targetCanvas.transform as RectTransform : null;
    }

    /// <summary>targetCanvas가 비어있을 때, Screen Space - Overlay Canvas를 하나 새로 만들어
    /// targetCanvas에 채워둡니다. 다른 UI 위에 항상 보이도록 Sorting Order를 넉넉히 높게 잡습니다.</summary>
    private void EnsureAutoCanvas()
    {
        if (targetCanvas != null) return;

        GameObject canvasGo = new GameObject("FloatingTextCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        targetCanvas = canvasGo.GetComponent<Canvas>();
        targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        targetCanvas.sortingOrder = 500; // 다른 일반 HUD보다 위에 뜨도록 넉넉히 높게 설정.

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
    }
}