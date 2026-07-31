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
//
// [게임 오버 - UIGameOver]
//   UIExit과 완전히 같은 방식으로 GameManager의 자식에 UIGameOver를 붙여두면 됩니다. 플레이어가
//   죽으면(PlayerController.Die()) GameManager.Instance.TriggerGameOver()를 호출해주세요 -
//   deathAnimationDelay초(사망 모션이 끝날 때까지, 기본 3초) 기다린 뒤, 화면을 FadeOut으로
//   까맣게 만들고, 다 어두워지면 UIGameOver.Show()를 대신 호출해서 게임 오버 화면을 페이드 인으로
//   띄워줍니다. 씬을 다시 시작하는 버튼 처리는 UIGameOver.ClickRestartButton()이 담당합니다
//   (UIGameOver.cs 참고).
//
// [게임 오버 화면 자동 리셋 - 중요]
//   UIGameOver도 이 스크립트의 자식이라 DontDestroyOnLoad로 함께 유지됩니다 - 그런데 그 말은
//   UIGameOver.ClickRestartButton()으로 씬을 다시 불러와도 UIGameOver.Awake()가 재실행되지 않는다는
//   뜻이라, Show()가 알파 1/interactable/blocksRaycasts로 켜뒀던 상태가 그대로 남아 재시작된 게임
//   화면을 계속 가리고 클릭도 막아버립니다(이 스크립트의 _fadeCanvasGroup도 FadeOut으로 알파 1(까맣게)
//   상태로 남아있는 채라 마찬가지 문제입니다). 그래서 TriggerGameOver()가 실행되는 순간
//   gameOverActive를 true로 표시해두고, SceneManager.sceneLoaded(새 씬의 오브젝트가 다 준비된 뒤에
//   발생 - 위 [씬 재시작 시 정적 오브젝트 풀 캐시 초기화]에서 sceneUnloaded를 쓰는 것과는 반대로,
//   여기서는 "새 씬이 완전히 시작된 뒤"가 정확히 우리가 원하는 타이밍입니다) 시점에 gameOverActive가
//   켜져 있으면 UIGameOver.Hide()로 게임 오버 화면을 완전히 숨기고 restartFadeInDuration에 걸쳐 화면을
//   FadeIn으로 되돌립니다. 게임 오버로 인한 재시작이 아닌(예: 메인 메뉴에서 처음 씬을 불러오는) 일반적인
//   씬 전환에는 gameOverActive가 꺼져있으니 전혀 영향을 주지 않습니다.
//
// [씬 재시작 시 정적 오브젝트 풀 캐시 초기화 - 중요]
//   NPCNameplate/MonsterHealthBar/RewardOrb/LootPickup은 전부 프리팹별 GameObjectPool을 static
//   Dictionary에 캐싱해두는 방식을 씁니다(각 스크립트 상단 주석 참고) - 원래는 "인게임 씬이 플레이
//   도중 다시 로드되지 않는다"는 전제로 만들어졌는데, UIGameOver의 재시작 버튼이 정확히 그 전제를
//   깹니다. 씬이 다시 로드되면 이전 씬의 poolRoot와 그 안의 인스턴스들은 실제로 파괴되지만, static
//   캐시는 씬과 무관하게 그대로 남아있어서 죽은 오브젝트를 계속 참조하려다
//   MissingReferenceException이 납니다.
//
//   [중요 - sceneLoaded가 아니라 sceneUnloaded를 구독하는 이유]
//   처음에는 SceneManager.sceneLoaded를 구독해서 새 씬이 로드될 때마다 캐시를 비웠는데, 이렇게 해도
//   여전히 같은 MissingReferenceException이 났습니다 - 원인은 타이밍입니다. sceneLoaded 이벤트는 새
//   씬의 모든 오브젝트의 Awake()/OnEnable()가 "이미 다 끝난 뒤"에야 발생합니다. 그런데 하필
//   NPCNameplate.Awake()가 바로 그 문제의 GameObjectPool.Get() 호출부라서, sceneLoaded가 캐시를 비워줄
//   때는 이미 그 새 씬의 첫 NPCNameplate.Awake()가 죽은 캐시를 참조해서 크래시가 난 "다음"입니다 -
//   너무 늦은 타이밍입니다. 대신 SceneManager.sceneUnloaded는 "이전" 씬이 언로드될 때(=새 씬의
//   오브젝트가 생성되어 Awake가 불리기 전) 발생하므로, 여기서 캐시를 비워두면 새 씬의 어떤
//   Awake()보다도 먼저 확실하게 정리가 끝나 있습니다. 앞으로 같은 패턴(프리팹별 static
//   GameObjectPool 캐시)으로 새 클래스를 추가한다면, 그 클래스에도 ResetStaticPools()를 만들어서
//   아래 HandleSceneUnloaded()에 한 줄 추가해주세요.
// ============================================================================

using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    /// <summary>씬을 넘나들며 하나만 유지되는 컴포넌트라, 다른 스크립트에서 여기로 바로 접근합니다.</summary>
    public static GameManager Instance { get; private set; }

    /// <summary>자식으로 붙어있는 UIExit입니다. 없어도(아직 연결 안 해도) null만 담기고 에러는 나지
    /// 않습니다 - UIExit은 보통 UIExit.Instance로 직접 접근하므로, 이 프로퍼티는 씬 연결을 확인하는
    /// 용도 정도로 생각하시면 됩니다.</summary>
    public UIExit Exit { get; private set; }

    /// <summary>자식으로 붙어있는 UIGameOver입니다. Exit과 같은 이유로, 보통은 TriggerGameOver()를
    /// 통해서 간접적으로만 쓰이고 UIGameOver.Instance로 직접 접근할 일은 거의 없습니다.</summary>
    public UIGameOver GameOver { get; private set; }

    [Header("화면 페이드 (풀스크린 페이드 인/아웃)")]
    [Tooltip("화면 전체를 덮는 Image가 붙은 CanvasGroup입니다. 반드시 연결하세요 - 비어있으면 " +
              "FadeOut()/FadeIn() 호출 시 바로 NullReferenceException이 납니다(연결을 빠뜨렸다는 게 " +
              "바로 드러나도록 하기 위해 일부러 방어 코드를 넣지 않았습니다).")]
    [SerializeField] CanvasGroup _fadeCanvasGroup;

    [Header("게임 오버")]
    [Tooltip("TriggerGameOver()가 호출된 뒤, 화면을 FadeOut하기 전에 먼저 기다리는 시간(초)입니다 - " +
              "사망 모션이 다 끝날 때까지 기다리는 용도입니다. 대략적인 사망 애니메이션 길이(기본 3초)에 " +
              "맞춰뒀으니 실제 모션 길이에 맞게 조절하세요. Time.timeScale과 무관하게(실시간 기준으로) " +
              "기다립니다.")]
    public float deathAnimationDelay = 3f;
    [Tooltip("대기 시간이 끝난 뒤, 화면이 완전히 까매질 때까지 걸리는 시간(초)입니다.")]
    public float gameOverFadeOutDuration = 1f;
    [Tooltip("재시작 버튼으로 씬이 다시 로드된 뒤, 화면이 다시 보이기까지(FadeIn) 걸리는 시간(초)입니다 - " +
              "파일 상단 [게임 오버 화면 자동 리셋] 참고.")]
    public float restartFadeInDuration = 1f;

    /// <summary>지금 화면이 완전히 불투명(알파 1, 완전히 가려진 상태)인지 여부입니다.</summary>
    public bool IsScreenFullyFaded => _fadeCanvasGroup != null && _fadeCanvasGroup.alpha >= 1f;

    private Tween fadeTween;
    private bool gameOverActive;

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
        GameOver = GetComponentInChildren<UIGameOver>(true);

        if (_fadeCanvasGroup != null)
        {
            _fadeCanvasGroup.alpha = 0f;
            _fadeCanvasGroup.interactable = false;
            _fadeCanvasGroup.blocksRaycasts = false;
        }

        SceneManager.sceneUnloaded += HandleSceneUnloaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    /// <summary>씬이 언로드될 때마다(= 다음 씬의 오브젝트들이 생성되어 Awake가 불리기 전에) 호출됩니다
    /// - 파일 상단 [씬 재시작 시 정적 오브젝트 풀 캐시 초기화] 참고, 특히 sceneLoaded가 아니라
    /// sceneUnloaded를 구독하는 이유(타이밍 문제)를 반드시 함께 읽어보세요. 언로드되는 씬에 있던
    /// poolRoot/인스턴스들은 이 시점에 이미(또는 곧) 파괴되므로, 그 죽은 참조를 계속 들고 있게 될
    /// 정적 캐시들을 다음 씬이 시작되기 전에 미리 비워서 새 씬에서 깨끗하게 다시 만들어지도록
    /// 합니다.</summary>
    private void HandleSceneUnloaded(Scene scene)
    {
        NPCNameplate.ResetStaticPools();
        MonsterHealthBar.ResetStaticPools();
        RewardOrb.ResetStaticPools();
        LootPickup.ResetStaticPools();
    }

    /// <summary>새 씬의 오브젝트가 모두 준비된 뒤(Awake/Start까지 끝난 뒤) 호출됩니다 - 파일 상단
    /// [게임 오버 화면 자동 리셋] 참고. gameOverActive가 꺼져있으면(게임 오버로 인한 재시작이 아니라면)
    /// 아무 것도 하지 않고 조용히 넘어갑니다.</summary>
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!gameOverActive) return;
        gameOverActive = false;

        GameOver?.Hide();
        FadeIn(restartFadeInDuration);
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

    /// <summary>플레이어가 사망했을 때 호출하세요(PlayerController.Die() 참고). 먼저
    /// deathAnimationDelay초(사망 모션이 끝날 때까지, Time.timeScale과 무관하게 실시간 기준으로)
    /// 기다린 뒤, 화면을 gameOverFadeOutDuration초에 걸쳐 FadeOut(까맣게)하고, 완전히 어두워지고
    /// 나서야 UIGameOver.Show()를 호출해 게임 오버 화면을 페이드 인으로 띄웁니다. UIGameOver가
    /// 자식으로 연결되어 있지 않으면 화면은 그대로 까맣게 남고 경고 로그만 남습니다.</summary>
    public void TriggerGameOver()
    {
        StartCoroutine(GameOverRoutine());
    }

    private IEnumerator GameOverRoutine()
    {
        // 사망 모션이 다 재생될 때까지 먼저 기다립니다 - WaitForSecondsRealtime이라 Time.timeScale이
        // 0이 되어도(다른 팝업 등으로 멈춰도) 영향받지 않고 항상 같은 실제 시간만큼 기다립니다.
        yield return new WaitForSecondsRealtime(deathAnimationDelay);

        yield return FadeOut(gameOverFadeOutDuration).WaitForCompletion();

        if (GameOver != null)
        {
            GameOver.Show();
            gameOverActive = true; // 다음 sceneLoaded 때 HandleSceneLoaded()가 자동으로 리셋해줍니다.
        }
        else
        {
            Debug.LogWarning("[GameManager] UIGameOver가 연결되어 있지 않아 게임 오버 화면을 띄울 수 없습니다. " +
                              "GameManager의 자식으로 UIGameOver를 붙여주세요.", this);
        }
    }
}