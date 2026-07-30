// ============================================================================
// SeaPurifyTransition.cs
// ----------------------------------------------------------------------------
// 오염된 바다 오브젝트 → 깨끗한 바다 오브젝트로 부드럽게 알파 크로스페이드하는 컷씬 연출 전용
// 컴포넌트입니다. 서로 위치만 겹쳐두고 머티리얼만 다른 두 오브젝트(오염된 바다는 처음부터 활성,
// 깨끗한 바다는 처음부터 비활성)를 대상으로, 오염된 쪽의 알파를 1→0으로, 깨끗한 쪽의 알파를 0→1로
// 동시에 애니메이션합니다.
//
// [컷씬에서 쓰는 법 - TriggerEvent + Wait]
//   CutsceneManager의 Trigger Events 리스트에 원하는 키(예: "PurifySea")를 만들고, 그 UnityEvent에
//   이 컴포넌트의 BeginPurifyTransition()을 등록하세요(CutsceneManager.cs 상단 [씬 준비] 9번 참고).
//   TriggerEvent 스텝은 호출 즉시(yield 없이) 다음 스텝으로 넘어가므로, 전환이 시각적으로 다 끝날
//   때까지 자연스럽게 기다리려면 바로 뒤에 duration과 같은 시간만큼 Wait 스텝을 넣으세요
//   (WalkPlayerToWaypoints, MiddleSlimeBoss.PlayShockwaveForCutscene() 등과 같은 패턴).
//
// [알파를 어떤 프로퍼티로 조절하는지 - fadeFloatProperty가 우선]
//   물 셰이더처럼 Shader Graph의 Alpha 마스터 스택 출력이 _BaseColor/_Color의 알파가 아니라 포말/
//   굴절 등 다른 계산 결과에 연결되어 있는 경우, _BaseColor/_Color의 알파를 아무리 바꿔도 화면에는
//   반영되지 않습니다(실제로 slimeWater 셰이더가 이런 경우였습니다 - Alpha 출력에 연결된 게 없어서
//   "색상 프로퍼티를 찾지 못했다"는 경고가 났습니다). 이런 셰이더라면 Shader Graph에 새 Float
//   프로퍼티를 하나 노출시키고 그 값을 최종 Alpha 출력에 곱해 넣도록 그래프를 수정한 뒤, 그
//   프로퍼티 이름을 fadeFloatProperty에 적어주세요 - 그러면 이 스크립트가 GetFloat/SetFloat/DOFloat로
//   그 프로퍼티만 직접 조절합니다.
//   [Shader Graph 수정 방법 - 요약]
//     1) Blackboard에서 + → Float 추가 (이름 예: "Purify Alpha", Reference를 "_PurifyAlpha"로 지정,
//        Default 1).
//     2) 지금 마스터 스택의 "Alpha" 입력에 연결된 노드를 찾아서, 그 노드와 "Alpha" 입력 사이에
//        Multiply 노드를 끼워 넣고, 새로 만든 "_PurifyAlpha" 프로퍼티를 그 Multiply의 다른 입력에
//        연결하세요(Alpha Clip Threshold 입력이 아니라 "Alpha" 입력이어야 합니다 - 이름이 비슷해서
//        헷갈리기 쉽습니다).
//     3) 저장(Ctrl+S)하면 이 셰이더를 쓰는 모든 머티리얼에 "Purify Alpha" 슬라이더가 새로 생깁니다.
//   fadeFloatProperty를 비워두면(기본값) 대신 _BaseColor/_Color의 알파를 바꾸는 방식으로 동작합니다
//   (ResolveColorProperty() 참고 - 알파가 실제로 최종 렌더링에 연결되어 있는 더 단순한 셰이더라면
//   이 방식만으로 충분합니다).
//
// [전제 조건 - 알파가 최종 렌더링에 실제로 영향을 줘야 합니다]
//   fadeFloatProperty를 쓰든 _BaseColor/_Color 폴백을 쓰든, 그 값이 실제로 마스터 스택의 Alpha
//   출력까지 이어져 있어야 합니다 - 단순히 Surface Type이 Transparent라고 해서 아무 알파 프로퍼티나
//   자동으로 화면에 반영되는 건 아닙니다(Alpha 출력이 다른 값에 연결되어 있으면 무시됩니다).
//
// [동작 방식]
//   BeginPurifyTransition()이 호출되면 두 오브젝트를 모두 활성화한 뒤(전환 중엔 서로 겹쳐 보이는
//   과도기 상태), duration초에 걸쳐 오염된 바다는 사라지고 깨끗한 바다는 나타납니다 - 다른 팝업이
//   Time.timeScale을 0으로 만들어도 계속 진행되도록 .SetUpdate(true)를 사용합니다(GameManager.
//   FadeOut/FadeIn, SetTitleCardVisible과 같은 방식). 전환이 완전히 끝나면 오염된 바다는
//   SetActive(false)로 꺼서, 이후 두 반투명 오브젝트가 계속 겹쳐 렌더링되는(오버드로우) 낭비를
//   없앱니다.
//
// [머티리얼 인스턴스에 대해]
//   renderer.material(복수형이 아닌 단수 프로퍼티)에 접근하는 순간 유니티가 자동으로 이 오브젝트
//   전용 인스턴스를 만들어주므로, 원본 머티리얼 애셋 자체의 값은 건드리지 않습니다 - 씬에 같은
//   머티리얼을 쓰는 다른 오브젝트가 있어도 서로 영향을 주지 않습니다.
//
// [씬 준비]
//   빈 오브젝트(또는 Polluted Sea/Clean Sea 둘 중 하나)에 이 스크립트를 붙이고 Polluted Sea/Clean Sea
//   필드에 각각 연결하세요. 셰이더에 전용 Float 프로퍼티를 새로 추가했다면 Fade Float Property에 그
//   Reference 이름(예: "_PurifyAlpha")을 정확히 적어주세요. 씬 시작 시(Awake) 오염된 바다는 알파
//   1로, 깨끗한 바다는 비활성 상태로 자동 정리되므로, 에디터에서 테스트하다 남은 상태를 신경 쓸
//   필요 없습니다.
// ============================================================================

using System.Collections;
using DG.Tweening;
using UnityEngine;

public class SeaPurifyTransition : MonoBehaviour
{
    [Header("전환 대상")]
    [Tooltip("지금 활성 상태인 오염된 바다 오브젝트입니다. 전환이 끝나면 SetActive(false)로 꺼집니다.")]
    public GameObject pollutedSea;
    [Tooltip("지금 비활성 상태인 깨끗한 바다 오브젝트입니다. 전환이 시작되는 즉시 SetActive(true)로 켜지고 알파 0에서부터 나타납니다.")]
    public GameObject cleanSea;

    [Header("알파 프로퍼티 (비워두면 _BaseColor/_Color 자동 탐색)")]
    [Tooltip("셰이더 그래프에 직접 추가한, Alpha 출력에 연결된 전용 Float 프로퍼티의 Reference 이름입니다 " +
              "(예: \"_PurifyAlpha\" - 파일 상단 주석의 [Shader Graph 수정 방법] 참고). 비워두면 대신 " +
              "_BaseColor/_Color의 알파값을 바꿉니다 - 다만 그 값이 실제로 셰이더의 최종 Alpha 출력에 " +
              "연결되어 있는 경우에만 화면에 반영됩니다.")]
    public string fadeFloatProperty = "";

    [Header("전환 시간")]
    [Tooltip("알파가 완전히 뒤바뀌는 데 걸리는 시간(초)입니다. 컷씬 쪽 TriggerEvent 스텝 바로 뒤에 이 값과 " +
              "같은 시간만큼 Wait 스텝을 넣어 전환이 끝날 때까지 자연스럽게 기다리게 하세요.")]
    public float duration = 3f;

    private Coroutine activeTransition;

    private void Awake()
    {
        // 에디터에서 미리보기용으로 두 오브젝트를 이것저것 켜/끄거나 알파를 바꿔봤을 수 있으므로,
        // 씬이 시작되는 시점엔 항상 "정화 전" 상태로 확실히 되돌려둡니다.
        if (pollutedSea != null)
        {
            pollutedSea.SetActive(true);
            SetAlpha(GetInstanceMaterial(pollutedSea), 1f);
        }

        if (cleanSea != null)
        {
            cleanSea.SetActive(false);
        }
    }

    /// <summary>오염된 바다 → 깨끗한 바다로 알파 크로스페이드를 시작합니다. 매개변수가 없어야
    /// CutsceneManager의 Trigger Events UnityEvent 인스펙터 드롭다운에 나타납니다(MiddleSlimeBoss.
    /// PlayShockwaveForCutscene()과 같은 이유 - 매개변수가 있는 메서드는 그 목록에 뜨지 않습니다).
    /// 이미 전환이 진행 중이면 먼저 멈추고 처음부터 다시 시작합니다.</summary>
    public void BeginPurifyTransition()
    {
        if (activeTransition != null) StopCoroutine(activeTransition);
        activeTransition = StartCoroutine(TransitionRoutine());
    }

    private IEnumerator TransitionRoutine()
    {
        if (pollutedSea == null || cleanSea == null)
        {
            Debug.LogWarning("[SeaPurifyTransition] Polluted Sea/Clean Sea가 연결되어 있지 않습니다.", this);
            yield break;
        }

        if (!pollutedSea.activeSelf) pollutedSea.SetActive(true);
        if (!cleanSea.activeSelf) cleanSea.SetActive(true);

        Material pollutedMat = GetInstanceMaterial(pollutedSea);
        Material cleanMat = GetInstanceMaterial(cleanSea);

        // 겹쳐 보이는 과도기가 시작되는 순간, 깨끗한 바다는 완전히 투명한 상태에서부터 나타나야
        // 자연스럽습니다 - 이전에 중간에 멈췄던 상태가 남아있을 수 있으니 매번 확실히 초기화합니다.
        SetAlpha(cleanMat, 0f);
        SetAlpha(pollutedMat, 1f);

        Tween pollutedTween = BuildFadeTween(pollutedMat, 0f);
        Tween cleanTween = BuildFadeTween(cleanMat, 1f);

        if (pollutedTween == null)
        {
            Debug.LogWarning("[SeaPurifyTransition] Polluted Sea 머티리얼에서 알파 프로퍼티를 찾지 못해 전환을 재생할 수 없습니다 - Fade Float Property 설정이나 파일 상단 주석을 확인하세요.", pollutedSea);
        }
        if (cleanTween == null)
        {
            Debug.LogWarning("[SeaPurifyTransition] Clean Sea 머티리얼에서 알파 프로퍼티를 찾지 못해 전환을 재생할 수 없습니다 - Fade Float Property 설정이나 파일 상단 주석을 확인하세요.", cleanSea);
        }

        // 둘 다 같은 duration으로 동시에 진행되므로 하나만 기다려도 충분하지만, 혹시 한쪽 프로퍼티를
        // 못 찾아 다른 한쪽만 재생 중일 수도 있으니 존재하는 쪽을 기다립니다.
        if (pollutedTween != null) yield return pollutedTween.WaitForCompletion();
        else if (cleanTween != null) yield return cleanTween.WaitForCompletion();

        if (pollutedSea != null) pollutedSea.SetActive(false); // 다 정화됐으니 더 이상 겹쳐 그릴 필요가 없습니다.
        activeTransition = null;
    }

    /// <summary>fadeFloatProperty가 지정되어 있고 머티리얼에 실제로 그 프로퍼티가 있으면 그것을,
    /// 아니면 ResolveColorProperty()로 찾은 _BaseColor/_Color를 대상으로 duration초짜리 페이드
    /// 트윈을 만듭니다. 둘 다 없으면 null을 반환합니다.</summary>
    private Tween BuildFadeTween(Material mat, float targetAlpha)
    {
        if (mat == null) return null;

        if (!string.IsNullOrEmpty(fadeFloatProperty) && mat.HasProperty(fadeFloatProperty))
        {
            return mat.DOFloat(targetAlpha, fadeFloatProperty, duration).SetUpdate(true);
        }

        string colorProperty = ResolveColorProperty(mat);
        if (colorProperty != null)
        {
            return mat.DOFade(targetAlpha, colorProperty, duration).SetUpdate(true);
        }

        return null;
    }

    private static Material GetInstanceMaterial(GameObject go)
    {
        Renderer renderer = go.GetComponent<Renderer>();
        return renderer != null ? renderer.material : null;
    }

    /// <summary>이 머티리얼에서 실제로 화면에 반영되는 색상 프로퍼티 이름을 찾습니다 - "_BaseColor"
    /// (URP Lit 계열 셰이더의 진짜 베이스 컬러)가 있으면 그걸 우선 쓰고, 없으면 "_Color"를 시도합니다.
    /// 둘 다 없으면(shader가 둘 중 어느 이름도 쓰지 않는 완전히 다른 커스텀 프로퍼티라면) null을
    /// 반환합니다.</summary>
    private static string ResolveColorProperty(Material mat)
    {
        if (mat == null) return null;
        if (mat.HasProperty("_BaseColor")) return "_BaseColor";
        if (mat.HasProperty("_Color")) return "_Color";
        return null;
    }

    private void SetAlpha(Material mat, float alpha)
    {
        if (mat == null) return;

        if (!string.IsNullOrEmpty(fadeFloatProperty) && mat.HasProperty(fadeFloatProperty))
        {
            mat.SetFloat(fadeFloatProperty, alpha);
            return;
        }

        string colorProperty = ResolveColorProperty(mat);
        if (colorProperty == null) return;

        Color color = mat.GetColor(colorProperty);
        color.a = alpha;
        mat.SetColor(colorProperty, color);
    }
}