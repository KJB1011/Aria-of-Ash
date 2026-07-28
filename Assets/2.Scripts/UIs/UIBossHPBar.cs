// ============================================================================
// UIBossHPBar.cs
// ----------------------------------------------------------------------------
// 화면에 고정된 보스 체력바 UI입니다. World Space가 아니라 일반 Screen Space Canvas에 이름+레벨
// 표시용 TextMeshProUGUI + 체력 표시용 Slider(UnityEngine.UI.Slider)로 구성됩니다. 몬스터 머리
// 위를 따라다니는 UIMonsterHealthBar.cs와 달리 위치를 옮기거나 빌보드할 필요가 없어서, 이 스크립트는
// 순수하게 "이름/레벨 텍스트, 체력 비율(0~1)을 받아서 각각 텍스트/Slider 값에 반영"만 합니다
// (UIMonsterHealthBar.cs와 같은 "프리팹 컨트롤러" 패턴 - 실제로 언제 어떤 값을 넣을지는 보스 쪽
// 스크립트, 예: MiddleSlimeBoss.cs가 결정합니다).
//
// [프리팹/씬 준비]
//   1) Canvas(Screen Space - Overlay 또는 Camera)를 화면 위쪽 등 원하는 위치에 배치하세요.
//   2) 이름+레벨을 표시할 TextMeshProUGUI를 하나 만드세요.
//   3) 그 아래(또는 옆)에 UI > Slider로 체력바를 만드세요. Interactable은 꺼두세요(보여주기만
//      하는 용도입니다). Slider의 Min Value 0 / Max Value 1로 두면 SetHealthRate(0~1)를 그대로
//      value에 대입할 수 있습니다.
//   4) 이 스크립트를 Canvas 오브젝트(또는 그 자식 아무 곳)에 붙이고, 이름+레벨 텍스트를
//      Txt Name Level 필드에, Slider를 Health Slider 필드에 연결하세요.
//   5) 씬에 배치해둔 이 오브젝트를 보스 쪽 스크립트(예: MiddleSlimeBoss.Boss Hp Bar 필드)에
//      연결하세요.
//
// [표시 형식]
//   SetInfo(bossName, level)를 호출하면 "[보스 이름]   Lv. [레벨]" 형식으로 표시됩니다
//   (예: "미들 슬라임   Lv. 25"). 이름/레벨은 보스가 등장할 때(또는 씬 시작 시) 한 번만
//   반영하면 되고, 전투 중 계속 바뀌는 체력만 SetHealthRate()로 매 프레임 갱신하면 됩니다.
//
// [피격 연출 - 깎인 만큼 흰색 플래시 후 페이드]
//   SetHealthRate()가 이전보다 낮은 값으로 호출되면(=데미지를 입으면), "방금까지의 체력 비율"과
//   "새로 줄어든 체력 비율" 사이 구간(즉 이번에 깎인 만큼)만 Damage Flash Image가 흰색으로 채워졌다가
//   damageFlashFadeDuration초에 걸쳐 서서히 투명해집니다. Slider 자체는 항상 즉시 최신 값으로
//   반영되고(지연 없음), 이 흰색 영역은 그 위에 겹쳐서 "방금 깎인 부분"만 잠깐 강조해주는 연출용
//   오버레이입니다.
//
//   [프리팹 준비 - Damage Flash Image]
//   1) Health Slider의 Fill Area와 같은 크기/위치에 배경으로 겹쳐질 UI > Image를 하나 만드세요
//      (Slider의 Fill Area 바로 옆이나 그 부모 아래, Slider의 배경(Background)보다는 위, Fill보다는
//      위에 오도록 계층 순서를 잡아주세요 - 그래야 체력바 위에 흰색이 겹쳐 보입니다).
//   2) 색을 흰색(Alpha 포함 255)으로 설정하세요. 이 스크립트가 매 데미지마다 anchorMin/anchorMax.x와
//      Alpha를 직접 스크립트로 조절하므로, 초기 Rect 크기/앵커는 아무 값이어도 상관없습니다(재생 시
//      자동으로 덮어씁니다).
//   3) 이 Image를 Damage Flash Image 필드에 연결하세요. 비워두면 이 연출 없이 Slider 값만 즉시
//      바뀝니다(기존과 동일하게 동작).
// ============================================================================

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIBossHPBar : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _txtNameLevel;
    [SerializeField] Slider _healthSlider;

    [Header("피격 연출 (깎인 만큼 흰색 플래시 후 페이드)")]
    [Tooltip("HP가 깎일 때, 방금 깎인 구간(이전 비율~새 비율)만 흰색으로 채웠다가 서서히 투명해지는 " +
              "이미지입니다. Health Slider의 Fill Area와 같은 영역에 겹쳐두세요. 비워두면 이 연출 없이 " +
              "Slider 값만 즉시 바뀝니다.")]
    [SerializeField] Image _damageFlashImage;
    [Tooltip("흰색으로 채워진 뒤 완전히 투명해지기까지 걸리는 시간(초).")]
    public float damageFlashFadeDuration = 0.5f;

    private float currentRate = 1f; // 마지막으로 SetHealthRate()에 반영된 비율 - 다음 호출과 비교해 "얼마나 깎였는지" 계산합니다.
    private bool initialized; // 첫 SetHealthRate() 호출에서는 비교 대상(currentRate 기본값 1)이 아직 진짜 체력이 아니므로 플래시를 건너뜁니다.
    private Coroutine damageFlashRoutine;

    /// <summary>Damage Flash Image를 처음엔 꺼둡니다 - 씬에서 흰색/불투명으로 세팅해둔 상태 그대로
    /// 두면(권장 세팅) 데미지를 한 번도 안 입은 시작 시점에도 화면에 그대로 보여서 "보스바가 처음부터
    /// 흰색"으로 보이는 문제가 있었습니다. 실제로 흰색으로 채우고 보여주는 시점은 PlayDamageFlash()가
    /// 전담합니다.</summary>
    private void Awake()
    {
        if (_damageFlashImage != null)
        {
            _damageFlashImage.gameObject.SetActive(false);
        }
    }

    /// <summary>보스 이름과 레벨을 "[이름]   Lv. [레벨]" 형식으로 표시합니다. 보스 쪽 스크립트가
    /// 등장 시점에 한 번만 호출하면 됩니다(이름/레벨은 전투 중 바뀌지 않으므로 체력처럼 매 프레임
    /// 갱신할 필요가 없습니다).</summary>
    public void SetInfo(string bossName, int level)
    {
        if (_txtNameLevel == null) return;
        _txtNameLevel.text = $"{bossName}   Lv. {level}";
    }

    /// <summary>체력 비율(0~1)을 받아서 Slider의 value에 즉시 반영합니다. 범위 밖 값이 들어와도
    /// 0~1로 안전하게 잘라냅니다. Slider의 Min/Max Value가 0/1로 설정되어 있어야 정확히 맞습니다.
    /// 이전보다 값이 줄어든 경우(데미지를 입은 경우) 방금 깎인 구간만 흰색으로 잠깐 플래시했다가
    /// 서서히 사라지는 연출을 함께 재생합니다(Damage Flash Image가 연결되어 있을 때만).</summary>
    public void SetHealthRate(float rate01)
    {
        if (_healthSlider == null) return;

        rate01 = Mathf.Clamp01(rate01);

        if (initialized && rate01 < currentRate - 0.0001f)
        {
            PlayDamageFlash(rate01, currentRate);
        }

        currentRate = rate01;
        initialized = true;
        _healthSlider.value = rate01;
    }

    /// <summary>[newRate, oldRate] 구간(이번에 깎인 만큼)에 정확히 걸치도록 Damage Flash Image의
    /// anchorMin/anchorMax.x를 맞추고, 불투명한 흰색으로 되돌린 뒤 페이드를 새로 시작합니다. 아직
    /// 이전 페이드가 진행 중이었다면(연속으로 빠르게 맞은 경우) 멈추고 이번 구간 기준으로 다시
    /// 시작합니다.</summary>
    private void PlayDamageFlash(float newRate, float oldRate)
    {
        if (_damageFlashImage == null) return;

        RectTransform rt = _damageFlashImage.rectTransform;
        rt.anchorMin = new Vector2(newRate, 0f);
        rt.anchorMax = new Vector2(oldRate, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Color c = _damageFlashImage.color;
        c.a = 1f;
        _damageFlashImage.color = c;
        _damageFlashImage.gameObject.SetActive(true);

        if (damageFlashRoutine != null) StopCoroutine(damageFlashRoutine);
        damageFlashRoutine = StartCoroutine(FadeDamageFlash());
    }

    private IEnumerator FadeDamageFlash()
    {
        float elapsed = 0f;
        Color baseColor = _damageFlashImage.color;

        while (elapsed < damageFlashFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / damageFlashFadeDuration);

            Color c = baseColor;
            c.a = Mathf.Lerp(1f, 0f, t);
            _damageFlashImage.color = c;

            yield return null;
        }

        _damageFlashImage.gameObject.SetActive(false);
        damageFlashRoutine = null;
    }
}