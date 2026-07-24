// ============================================================================
// UILocationTitle.cs
// ----------------------------------------------------------------------------
// 화면에 지역 이름(예: "루멘 마을")을 큼직하게 잠깐 띄우는 타이틀 카드 UI입니다. 컷씬의 Establishing
// Shot(마을 전체를 훑어 보여주는 오프닝 샷) 같은 곳에서, CutsceneData의 SetLocationTitleVisible
// 스텝이 이 UI의 FadeIn()/FadeOut()을 호출해서 켜고 끕니다(CutsceneManager.cs 참고). GameManager의
// 화면 페이드(FadeOut/FadeIn)와 완전히 같은 방식(CanvasGroup + DOTween, .SetUpdate(true))이고,
// UIIngameLoot처럼 씬에 하나만 두고 static Instance로 접근합니다.
//
// [텍스트가 아니라 로고 이미지를 쓰고 싶다면]
//   Txt Title 대신(또는 함께) 이 오브젝트 하위에 로고 Image를 추가로 배치해도 됩니다 - 이 스크립트는
//   CanvasGroup의 알파만 조절하므로, 그 안에 뭐가 들어있든(텍스트/로고/배경 이미지 등) 함께
//   페이드인/아웃됩니다. Txt Title은 비워두면 SetName 호출을 그냥 무시하니, 텍스트 없이 로고
//   이미지만 쓰는 구성도 가능합니다.
//
// [씬 준비]
//   1) Canvas 하위에 지역 이름을 보여줄 패널(빈 오브젝트)을 만들고, 이 스크립트와 CanvasGroup
//      (RequireComponent로 자동 추가)을 붙이세요. 다른 UI보다 위에 그려지도록 Sort Order를
//      적당히 높게 잡으세요.
//   2) 그 안에 TextMeshProUGUI를 만들어 Txt Title 필드에 연결하세요(가운데 정렬 추천).
//   3) 씬에 이 스크립트를 가진 오브젝트가 정확히 하나 있어야 합니다 - CutsceneManager가
//      UILocationTitle.Instance로 바로 접근합니다.
// ============================================================================

using DG.Tweening;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class UILocationTitle : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _txtTitle;

    /// <summary>씬에 하나만 두고 쓰는 UI라, CutsceneManager 등 다른 스크립트에서 여기로 바로 접근합니다.</summary>
    public static UILocationTitle Instance { get; private set; }

    private CanvasGroup canvasGroup;
    private Tween fadeTween;

    private void Awake()
    {
        Instance = this;

        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>Txt Title의 텍스트를 title로 바꾼 뒤 duration초에 걸쳐 서서히 나타냅니다(알파 0 → 1).
    /// 반환하는 Tween에 .WaitForCompletion()을 걸어 yield하면 완전히 다 나타날 때까지 대기할 수
    /// 있습니다(GameManager.FadeIn()과 같은 패턴). .SetUpdate(true)를 붙여서 다른 팝업이 게임을
    /// 멈춰도(Time.timeScale = 0) 얼어붙지 않습니다.</summary>
    public Tween FadeIn(string title, float duration)
    {
        if (_txtTitle != null) _txtTitle.text = title;

        fadeTween?.Kill();
        fadeTween = canvasGroup.DOFade(1f, duration).SetUpdate(true);
        return fadeTween;
    }

    /// <summary>duration초에 걸쳐 서서히 사라집니다(알파 1 → 0).</summary>
    public Tween FadeOut(float duration)
    {
        fadeTween?.Kill();
        fadeTween = canvasGroup.DOFade(0f, duration).SetUpdate(true);
        return fadeTween;
    }
}