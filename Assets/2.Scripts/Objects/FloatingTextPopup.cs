// ============================================================================
// FloatingTextPopup.cs
// ----------------------------------------------------------------------------
// 알림 텍스트 프리팹 하나에 붙이는 컴포넌트입니다. FloatingTextManager가 오브젝트 풀에서
// 꺼내 Play()를 호출해주면, 스스로 페이드 인 → (살짝 위로 뜨며) 유지 → 페이드 아웃 애니메이션을
// 재생하고 끝나면 스스로 FloatingTextManager.ReturnToPool()을 호출해서 반납합니다 - 매니저는
// "언제 반납할지"를 몰라도 되고, 이 컴포넌트가 자기 생애주기를 알아서 책임지는 구조입니다
// (DamageNumberPopup과 완전히 같은 패턴입니다).
//
// [DamageNumberPopup과의 차이]
//   DamageNumberPopup은 월드 좌표에 떠 있는 3D TextMeshPro(카메라를 바라보게 매 프레임 회전)인
//   반면, 이 컴포넌트는 화면 좌표에 고정으로 뜨는 일반 UI(TextMeshProUGUI + RectTransform)입니다.
//   그래서 Canvas 아래에 있어야 하고(FloatingTextManager가 자동으로 처리), 위치도 카메라 투영이 아니라
//   RectTransform.anchoredPosition으로만 다룹니다.
//
// [프리팹 준비]
//   1) Canvas(또는 FloatingTextManager가 자동으로 만든 Canvas) 아래에 빈 UI 오브젝트를 만드세요
//      (GameObject > UI > Text - TextMeshPro로 만들면 자동으로 Canvas 자식이 되고 TextMeshProUGUI도
//      같이 생깁니다 - 그 루트 오브젝트를 그대로 프리팹으로 써도 됩니다).
//   2) 루트 오브젝트의 RectTransform Anchor/Pivot을 (0.5, 0.5)(정중앙 기준)로 맞춰두세요 -
//      FloatingTextManager.anchoredPosition이 "화면 중앙에서 얼마나 떨어졌는지"로 해석되려면
//      이 기준이 맞아야 합니다.
//   3) 이 스크립트(FloatingTextPopup)를 루트 오브젝트에 붙이고, Label에 TextMeshProUGUI를
//      연결하세요(비워두면 자식에서 자동으로 찾습니다). CanvasGroup은 비워두면 자동으로
//      추가됩니다 - 알파 페이드를 텍스트 색상이 아니라 CanvasGroup으로 처리해서, 나중에 아이콘
//      등 다른 자식 UI를 추가해도 한 번에 같이 페이드됩니다.
//   4) TextMeshProUGUI 자체에 설정해둔 Color가 "색을 지정하지 않고 Show()를 호출했을 때"의
//      기본 색으로 쓰입니다(Awake에서 기억해둡니다).
//   5) 완성되면 Assets/Resources/HUD/ 폴더 아래에 "FloatingText"라는 이름으로 프리팹을
//      저장하세요 (FloatingTextManager가 기본으로 이 이름을 찾습니다).
//   6) 씬에 배치할 필요 없습니다 - FloatingTextManager가 Resources에서 알아서 불러와 풀링합니다.
// ============================================================================

using System.Collections;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class FloatingTextPopup : MonoBehaviour, IPoolable
{
    [Header("참조")]
    [Tooltip("비워두면 자식에서 자동으로 찾습니다.")]
    public TextMeshProUGUI label;
    [Tooltip("알파(투명도) 페이드를 담당합니다. 비워두면 Awake에서 자동으로 추가합니다.")]
    public CanvasGroup canvasGroup;

    [Header("애니메이션")]
    [Tooltip("이 시간(초) 동안 화면에 떠 있다가(페이드 인/아웃 포함) 자동으로 풀에 반납됩니다.")]
    public float lifetime = 2f;
    [Tooltip("페이드 인(서서히 나타나기)에 걸리는 시간(초). lifetime보다 짧아야 합니다.")]
    public float fadeInDuration = 0.15f;
    [Tooltip("페이드 아웃(서서히 사라지기)에 걸리는 시간(초, lifetime 끝에서부터 역산). lifetime보다 짧아야 합니다.")]
    public float fadeOutDuration = 0.4f;
    [Tooltip("떠 있는 동안 위로 떠오르는 총 거리(px). 0으로 두면 제자리에 고정된 채로 페이드만 됩니다.")]
    public float riseDistance = 20f;
    [Tooltip("진행도(0~1)에 따라 얼마나 떠올랐는지의 곡선. 기본값은 처음에 빠르게 올라가고 " +
              "점점 느려지는 모양입니다.")]
    public AnimationCurve riseCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 2f),
        new Keyframe(1f, 1f, 0f, 0f));

    [Header("스택 쌓기 (FloatingTextManager 전용)")]
    [Tooltip("새 알림이 떠서 FloatingTextManager.AddStackOffset()이 호출될 때, 목표 위치까지 얼마나 " +
              "빠르게(부드럽게) 밀려 올라갈지의 속도입니다. 값이 클수록 거의 즉시 밀려납니다.")]
    public float stackMoveSpeed = 12f;

    private RectTransform rectTransform;
    private Color baseColor;
    private Vector2 baseAnchoredPosition;
    private Coroutine animateRoutine;

    // 스택으로 밀려 올라간 정도(px, 위 방향이 +)입니다. targetStackOffsetY는 AddStackOffset()이 호출될
    // 때마다 누적되는 "도착해야 할 목표값"이고, stackOffsetY는 매 프레임 stackMoveSpeed로 그 목표를
    // 부드럽게 따라가는 "지금 실제로 적용된 값"입니다 - 이렇게 나눠둔 덕분에 새 알림이 연달아 떠서
    // 목표가 계속 바뀌어도 순간이동하듯 튀지 않고 자연스럽게 계속 밀려 올라갑니다.
    private float stackOffsetY;
    private float targetStackOffsetY;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        if (label == null) label = GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null) baseColor = label.color;
        else Debug.LogWarning("[FloatingTextPopup] TextMeshProUGUI(Label)를 찾을 수 없습니다. 프리팹 구성을 확인해주세요.", this);

        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    /// <summary>FloatingTextManager 전용 진입점입니다. 메세지/색을 설정하고 페이드 인 → 유지 →
    /// 페이드 아웃 애니메이션을 시작합니다. color가 null이면 프리팹에 설정해둔 기본 색(baseColor)을
    /// 그대로 씁니다. 시작 위치는 호출 시점의 rectTransform.anchoredPosition(매니저가 이미 고정
    /// 위치로 맞춰준 상태)입니다.</summary>
    public void Play(string message, Color? color)
    {
        if (label != null)
        {
            label.text = message;
            label.color = color ?? baseColor;
        }

        baseAnchoredPosition = rectTransform.anchoredPosition;

        if (animateRoutine != null) StopCoroutine(animateRoutine);
        animateRoutine = StartCoroutine(AnimateAndRelease());
    }

    /// <summary>IPoolable 구현. 풀에서 꺼내져 활성화된 직후 호출됩니다. 실제 값 채우기는 뒤이어
    /// 호출되는 Play()가 담당하므로, 여기서는 이전 사용의 흔적(진행 중이던 코루틴/알파/스택 오프셋)만
    /// 정리합니다 - 스택 오프셋을 리셋하지 않으면 재사용된 인스턴스가 이전 생애의 밀려 올라간 위치에서
    /// 그대로 다시 시작해버립니다.</summary>
    public void OnGetFromPool()
    {
        if (animateRoutine != null)
        {
            StopCoroutine(animateRoutine);
            animateRoutine = null;
        }

        if (canvasGroup != null) canvasGroup.alpha = 0f; // Play()의 페이드 인이 0에서부터 시작하도록.

        stackOffsetY = 0f;
        targetStackOffsetY = 0f;
    }

    /// <summary>FloatingTextManager 전용입니다. 새 알림이 하나 더 뜰 때, 이미 떠 있는 이 알림을 위로
    /// deltaY(px)만큼 더 밀어올립니다 - 누적되므로 그 뒤에도 새 알림이 계속 뜨면 계속 더 위로
    /// 쌓입니다. 즉시 이동하지 않고 AnimateAndRelease()가 매 프레임 stackMoveSpeed로 이 목표값을
    /// 부드럽게 따라갑니다.</summary>
    public void AddStackOffset(float deltaY)
    {
        targetStackOffsetY += deltaY;
    }

    /// <summary>IPoolable 구현. 풀로 반납되어 비활성화되기 직전 호출됩니다. 애니메이션 코루틴이
    /// 남아있다면 정리해서, 다음에 재사용될 때 옛 코루틴이 뒤늦게 끼어들지 않도록 합니다.</summary>
    public void OnReleaseToPool()
    {
        if (animateRoutine != null)
        {
            StopCoroutine(animateRoutine);
            animateRoutine = null;
        }
    }

    private IEnumerator AnimateAndRelease()
    {
        float elapsed = 0f;

        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / lifetime);

            // 스택 오프셋(다른 알림에 밀려 올라간 정도)을 매 프레임 목표값(targetStackOffsetY)으로
            // 부드럽게 근접시킨 뒤, 원래의 "떠오르는" 애니메이션(riseDistance/riseCurve)과 더해서 최종
            // 위치를 계산합니다 - 두 움직임이 서로 방해하지 않고 자연스럽게 합쳐집니다.
            stackOffsetY = Mathf.Lerp(stackOffsetY, targetStackOffsetY, Time.deltaTime * stackMoveSpeed);

            rectTransform.anchoredPosition = baseAnchoredPosition +
                Vector2.up * (riseDistance * riseCurve.Evaluate(t) + stackOffsetY);

            if (canvasGroup != null)
            {
                float alpha = 1f;
                if (elapsed < fadeInDuration && fadeInDuration > 0f)
                {
                    alpha = elapsed / fadeInDuration;
                }
                else if (elapsed > lifetime - fadeOutDuration && fadeOutDuration > 0f)
                {
                    alpha = (lifetime - elapsed) / fadeOutDuration;
                }
                canvasGroup.alpha = Mathf.Clamp01(alpha);
            }

            yield return null;
        }

        if (canvasGroup != null) canvasGroup.alpha = 0f;

        animateRoutine = null;
        FloatingTextManager.Instance.ReturnToPool(gameObject);
    }
}