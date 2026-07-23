// ============================================================================
// GameManager.cs
// ----------------------------------------------------------------------------
// 씬을 넘나들며 계속 살아있는 전역 매니저입니다. UIExit(종료 확인창)를 자식으로 붙잡고 있다가
// DontDestroyOnLoad로 씬 전환에도 파괴되지 않게 하고, 화면 전체를 까맣게 덮는 풀스크린 페이드도
// 여기서 함께 관리합니다 - 나중에 로그인 정보 등 다른 전역 데이터가 필요해지면 이 스크립트에 계속
// 이어서 추가하시면 됩니다.
//
// [화면 페이드 - Fade Canvas Group]
//   컷씬(CutsceneManager) 시작/종료뿐 아니라, 사망 화면/씬 전환/로딩 등 "화면을 잠깐 가렸다가 다시
//   보여주는" 어떤 용도로도 재사용할 수 있도록 만들어뒀습니다. 원래는 별도의 ScreenFader
//   싱글턴이었는데, 씬이 바뀌어도 유지되어야 하는 전역 기능이라 GameManager로 옮겼습니다 - Exit과
//   마찬가지로 Fade Canvas Group도 이 오브젝트의 자식으로 두면 DontDestroyOnLoad로 함께 유지됩니다.
//   FadeOut()/FadeIn()은 DOTween Tween을 그대로 반환하니, 완전히 끝날 때까지 기다리고 싶으면
//   `yield return GameManager.Instance.FadeOut(duration).WaitForCompletion();`처럼 쓰세요
//   (CutsceneManager.cs 참고). UIIngameLoot/UIIngameQuest 등 다른 CanvasGroup 페이드와 같은 이유로
//   .SetUpdate(true)를 붙여서, 다른 팝업이 게임을 멈춰도(Time.timeScale = 0) 얼어붙지 않습니다.
//
// [씬 준비]
//   1) 로그인 씬(또는 게임이 가장 먼저 시작하는 씬)에 빈 오브젝트를 만들고 이 스크립트를 붙이세요.
//   2) 그 오브젝트의 자식으로 UIExit이 붙어있는 팝업 오브젝트(Canvas 포함)를 두세요 - 부모가
//      DontDestroyOnLoad되면 자식도 함께 유지되므로, UIExit도 씬이 바뀌어도 계속 살아있고
//      다른 씬에서도 UIExit.Instance로 그대로 접근할 수 있습니다.
//   3) 마찬가지로 화면 전체를 덮는 검은색(또는 원하는 색) Image를 만들어 그 오브젝트의 자식(또는
//      다른 자식 Canvas)으로 두고, CanvasGroup을 붙이세요(다른 UI보다 위에 그려지도록 Sort Order를
//      가장 높게 잡으세요). 그 CanvasGroup을 Fade Canvas Group 필드에 연결하세요 - Image의 색은
//      인스펙터에서 직접 지정하면 됩니다(이 스크립트는 CanvasGroup의 알파만 조절합니다).
//   4) 씬에 이 스크립트를 가진 오브젝트가 정확히 하나만 있으면 됩니다 - 씬을 다시 불러오는 등
//      두 번째 GameManager가 생겨도 Awake()가 자동으로 기존 것을 유지하고 새로 생긴 걸
//      제거합니다(SoundManager.cs의 중복 방지 패턴과 동일).
//
// [UIExit 접근]
//   다른 스크립트는 UIExit의 static Instance로 바로 접근하면 됩니다: UIExit.Instance.Show().
//   이 스크립트는 그 UIExit이 씬 전환에도 살아있도록 "부모" 역할만 할 뿐, 굳이 이 스크립트를
//   거칠 필요는 없습니다. 다만 씬 연결이 잘 됐는지 바로 확인해볼 수 있도록 Exit 프로퍼티로도
//   꺼내볼 수 있게 해뒀습니다(GameManager.Instance.Exit).
// ============================================================================

using DG.Tweening;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    /// <summary>씬을 넘나들며 하나만 유지되는 컴포넌트라, 다른 스크립트에서 여기로 바로 접근합니다.</summary>
    public static GameManager Instance { get; private set; }

    /// <summary>자식으로 붙어있는 UIExit입니다. 없어도(아직 연결 안 해도) null만 담기고 에러는 나지
    /// 않습니다 - UIExit은 보통 UIExit.Instance로 직접 접근하므로, 이 프로퍼티는 씬 연결을 확인하는
    /// 용도 정도로 생각하시면 됩니다.</summary>
    public UIExit Exit { get; private set; }

    [Header("화면 페이드 (풀스크린 페이드 인/아웃)")]
    [Tooltip("화면 전체를 덮는 Image가 붙은 CanvasGroup입니다. 반드시 연결하세요 - 비어있으면 " +
              "FadeOut()/FadeIn() 호출 시 바로 NullReferenceException이 납니다(연결을 빠뜨렸다는 게 " +
              "바로 드러나도록 하기 위해 일부러 방어 코드를 넣지 않았습니다).")]
    [SerializeField] CanvasGroup _fadeCanvasGroup;

    /// <summary>지금 화면이 완전히 불투명(알파 1, 완전히 가려진 상태)인지 여부입니다.</summary>
    public bool IsScreenFullyFaded => _fadeCanvasGroup != null && _fadeCanvasGroup.alpha >= 1f;

    private Tween fadeTween;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // 씬 전환 등으로 인해 두 번째 GameManager가 생기면 기존 것을 유지하고 새로 생긴 걸 제거합니다.
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Exit = GetComponentInChildren<UIExit>(true); // 비활성화된 자식도 찾도록 true를 넘깁니다.

        if (_fadeCanvasGroup != null)
        {
            _fadeCanvasGroup.alpha = 0f;
            _fadeCanvasGroup.interactable = false;
            _fadeCanvasGroup.blocksRaycasts = false;
        }
    }

    /// <summary>화면을 duration초에 걸쳐 서서히 까맣게(알파 0 → 1) 만듭니다. 시작하자마자 뒤쪽 클릭이
    /// 새어나가지 않도록 blocksRaycasts를 즉시 켭니다. 반환하는 Tween에 .WaitForCompletion()을 걸어
    /// yield하면 완전히 까매질 때까지 대기할 수 있습니다.</summary>
    public Tween FadeOut(float duration)
    {
        _fadeCanvasGroup.blocksRaycasts = true;

        fadeTween?.Kill();
        fadeTween = _fadeCanvasGroup.DOFade(1f, duration).SetUpdate(true); // 다른 팝업이 Time.timeScale을 0으로 만들어도 얼어붙지 않습니다.
        return fadeTween;
    }

    /// <summary>화면을 duration초에 걸쳐 서서히 다시 보이게(알파 1 → 0) 만듭니다. 다 끝나면
    /// blocksRaycasts를 꺼서 뒤쪽 UI/월드 클릭이 다시 통과하게 합니다.</summary>
    public Tween FadeIn(float duration)
    {
        fadeTween?.Kill();
        fadeTween = _fadeCanvasGroup.DOFade(0f, duration)
            .SetUpdate(true)
            .OnComplete(() => _fadeCanvasGroup.blocksRaycasts = false);
        return fadeTween;
    }

    /// <summary>페이드 애니메이션 없이 즉시 알파를 0(완전히 보임) 또는 1(완전히 가려짐)로 맞춥니다.
    /// 씬 시작 시점 등 페이드 연출 없이 상태만 강제로 맞추고 싶을 때 사용하세요.</summary>
    public void SetScreenFadeInstant(bool opaque)
    {
        fadeTween?.Kill();
        _fadeCanvasGroup.alpha = opaque ? 1f : 0f;
        _fadeCanvasGroup.blocksRaycasts = opaque;
    }
}