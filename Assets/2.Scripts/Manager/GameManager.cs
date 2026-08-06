// ============================================================================
// GameManager.cs
// ----------------------------------------------------------------------------
// 씬을 넘나들며 계속 살아있는 전역 매니저입니다. UIExit(종료 확인창)를 자식으로 붙잡고 있다가
// DontDestroyOnLoad로 씬 전환에도 파괴되지 않게 하고, 화면 전체를 까맣게 덮는 풀스크린 페이드도
// 여기서 함께 관리합니다 - 나중에 로그인 정보 등 다른 전역 데이터가 필요해지면 이 스크립트에 계속
// 이어서 추가하시면 됩니다.
//
// [씬을 넘어 들고 다니는 데이터 - PlayerId]
//   LobbyScene의 아이디 입력창에 적은 값을 IngameScene에서도 그대로 쓸 수 있도록(UICharacterInfo의
//   플레이어 닉네임 표시 등) 여기 들고 있습니다. UILobby.ClickGameStartButton()이 씬을 넘어가기
//   직전에 SetPlayerId()로 채워주고, IngameScene 쪽에서는 GameManager.Instance.PlayerId로 읽기만
//   하면 됩니다 - GameManager 자체가 DontDestroyOnLoad라 씬이 바뀌어도 값이 그대로 유지됩니다.
//
// [화면 페이드 - Fade Canvas Group]
//   컷씬(CutsceneSequence) 시작/종료뿐 아니라, 사망 화면/씬 전환/로딩 등 "화면을 잠깐 가렸다가 다시
//   보여주는" 어떤 용도로도 재사용할 수 있도록 만들어뒀습니다. 원래는 별도의 ScreenFader
//   싱글턴이었는데, 씬이 바뀌어도 유지되어야 하는 전역 기능이라 GameManager로 옮겼습니다 - Exit과
//   마찬가지로 Fade Canvas Group도 이 오브젝트의 자식으로 두면 DontDestroyOnLoad로 함께 유지됩니다.
//   FadeOut()/FadeIn()은 DOTween Tween을 그대로 반환하니, 완전히 끝날 때까지 기다리고 싶으면
//   `yield return GameManager.Instance.FadeOut(duration).WaitForCompletion();`처럼 쓰세요
//   (CutsceneSequence.cs 참고). UIIngameLoot/UIIngameQuest 등 다른 CanvasGroup 페이드와 같은 이유로
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
//   5) (선택) LoadSceneWithFade()로 씬을 전환할 때 로딩 화면을 보여주고 싶다면, Fade Canvas Group과
//      같은 자식 Canvas 아래(그 위에 그려지도록 Sort Order를 더 높게) 슬라이더 + 퍼센트 텍스트를
//      담은 로딩 UI 오브젝트를 만들어 Loading Root에 연결하고, 그 안의 Slider/텍스트를 각각 Loading
//      Slider/Loading Percent Text에 연결하세요. 처음엔 비활성화 상태로 둬도 됩니다 - Awake()가
//      알아서 꺼둡니다. 셋 다 비워두면 로딩 화면 없이 조용히 로드만 진행됩니다.
//
// [UIExit 접근]
//   다른 스크립트는 UIExit의 static Instance로 바로 접근하면 됩니다: UIExit.Instance.Show().
//   이 스크립트는 그 UIExit이 씬 전환에도 살아있도록 "부모" 역할만 할 뿐, 굳이 이 스크립트를
//   거칠 필요는 없습니다. 다만 씬 연결이 잘 됐는지 바로 확인해볼 수 있도록 Exit 프로퍼티로도
//   꺼내볼 수 있게 해뒀습니다(GameManager.Instance.Exit).
//
// [게임 오버 - UIGameOver] (지금은 PlayerController.Die()가 호출하지 않는 미사용 경로입니다 - 아래
//  [리스폰] 참고. 씬을 완전히 초기화하는 게임 오버 UI가 나중에 다시 필요해지면 그대로 재사용할 수
//  있도록 코드는 남겨뒀습니다.)
//   UIExit과 완전히 같은 방식으로 GameManager의 자식에 UIGameOver를 붙여두면 됩니다. TriggerGameOver()를
//   호출하면 deathAnimationDelay초(사망 모션이 끝날 때까지, 기본 3초) 기다린 뒤, 화면을 FadeOut으로
//   까맣게 만들고, 다 어두워지면 UIGameOver.Show()를 대신 호출해서 게임 오버 화면을 페이드 인으로
//   띄워줍니다. 씬을 다시 시작하는 버튼 처리는 UIGameOver.ClickRestartButton()이 담당합니다
//   (UIGameOver.cs 참고).
//
// [리스폰 - 완전 초기화 대신 위치/HP/MP만 리셋]
//   플레이어가 죽으면 이제 PlayerController.Die()가 TriggerGameOver() 대신 TriggerRespawn(this)를
//   호출합니다 - 씬을 다시 불러오지 않으므로 퀘스트/인벤토리/레벨 등 진행도가 그대로 유지됩니다.
//   deathAnimationDelay초 기다렸다가 화면을 FadeOut(둘 다 [게임 오버] 항목의 값을 그대로 재사용합니다)한
//   뒤, 화면이 완전히 까매진 상태에서 (1) PlayerController.Respawn()으로 플레이어를 respawnPoint로
//   되돌리고 HP/MP를 꽉 채우고, (2) 지금 씬에 있는 살아있는 몬스터(일반 MonsterFSM + MiddleSlimeBoss)를
//   전부 풀피로 되돌린 다음, respawnFadeInDuration에 걸쳐 FadeIn합니다 - 자세한 내용은 아래
//   TriggerRespawn()/RespawnRoutine()/HealAllAliveMonsters() 참고. UIGameOver/gameOverActive 플래그는
//   이 경로에서 전혀 건드리지 않습니다(완전히 독립된 별도 흐름입니다).
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
// [일반적인 씬 전환 - LoadSceneWithFade()]
//   게임 오버 재시작(위 [게임 오버 화면 자동 리셋])과는 별개로, 로비 → 인게임처럼 "페이드 아웃 →
//   (로딩 화면) → 씬 로드 → 페이드 인"이 필요한 모든 일반적인 씬 전환에 그대로 쓸 수 있는 범용
//   함수입니다(UILobby.cs 참고). 게임 오버 재시작은 gameOverActive 플래그로 다음 SceneManager.
//   sceneLoaded 이벤트 때 HandleSceneLoaded()가 자동으로 FadeIn을 걸어주는 방식이지만, 이쪽은 아래
//   [로딩 화면] 문단에 적은 이유로 그 이벤트에 기대지 않고 LoadSceneWithFadeRoutine() 코루틴이 직접
//   FadeIn()을 호출합니다 - 서로 다른 메커니즘이지만 완전히 독립적이라 섞여도 안전합니다.
//
//   [BGM도 화면과 같은 타이밍으로 - bgmNameOnLoad]
//   선택 인자 bgmNameOnLoad를 넘기면, 화면이 까매지기 시작하는 순간 지금 재생 중이던 BGM도 함께
//   fadeOutDuration에 걸쳐 페이드아웃(로딩 내내 무음)되고, 새 씬 로딩이 끝나 화면이 다시 fadeInDuration에
//   걸쳐 밝아지는 순간 이 곡이 SetFieldBGM()으로 같은 길이만큼 페이드인됩니다 - 화면과 음악이 항상
//   같은 리듬으로 어두워지고/밝아집니다. 비워두면(기본값) BGM은 전혀 건드리지 않습니다.
//
// [로딩 화면 - 슬라이더 + 진행률 텍스트]
//   화면이 완전히 까매진(FadeOut 완료) 뒤부터 새 씬이 다 준비될 때까지 SceneManager.LoadSceneAsync()로
//   비동기 로드하면서, 그 진행률(AsyncOperation.progress)을 Loading Root 아래 슬라이더/텍스트에
//   실시간으로 반영합니다. AsyncOperation.progress는 씬 에셋을 다 불러오면 1이 아니라 0.9에서
//   멈추고, allowSceneActivation을 true로 바꿔야 그 이후(0.9→1) 실제 씬 전환이 일어나는 유니티의
//   고유한 동작이라(에셋 로드 자체는 순식간에 끝나는 작은 씬에서도 슬라이더가 항상 90%에서 잠깐
//   멈춰있는 것처럼 보이는 이유이기도 합니다), SetLoadingProgress()에서 progress를 0~0.9 구간으로
//   정규화(progress / 0.9f)해서 0~100%가 자연스럽게 채워지도록 했습니다.
//
//   [중요 - FadeIn을 SceneManager.sceneLoaded 이벤트가 아니라 코루틴이 직접 호출하는 이유]
//   처음에는 게임 오버 재시작과 똑같이 "sceneLoaded 이벤트가 오면 자동으로 FadeIn"하는 방식으로
//   만들었는데, 그러면 로딩 화면(_loadingRoot)이 가려주는 순간과 FadeIn이 시작되는 순간이 서로
//   다른 두 메커니즘(코루틴의 op.isDone 폴링 vs 엔진이 내부적으로 dispatch하는 sceneLoaded 이벤트)에
//   맡겨져 있어서, sceneLoaded가 코루틴이 op.isDone을 알아채기도 전에(또는 같은 프레임 안에서 더
//   먼저) 발생하면 FadeIn이 로딩 화면에 가려진 채로 시작(심할 땐 다 끝나기)까지 해버리고, 그 다음에야
//   로딩 화면이 꺼지면서 이미 다 밝아진 화면이 갑자기 나타나 "페이드 인이 안 보인다"는 증상으로
//   이어졌습니다. 그래서 이 경로는 sceneLoaded 이벤트에 전혀 기대지 않고, 코루틴이 _loadingRoot를
//   끈 바로 다음 줄에서 직접 FadeIn()을 호출하도록 바꿨습니다 - 순서가 코드 한 줄 한 줄로 보장되어
//   더 이상 타이밍이 어긋날 수 없습니다.
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
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

    /// <summary>LobbyScene의 아이디 입력창에서 넘어온 값입니다(UILobby.cs 참고). 아직 한 번도
    /// SetPlayerId()가 호출되지 않았다면(예: IngameScene을 로비 없이 바로 테스트하는 경우) 빈
    /// 문자열입니다 - null 체크 없이 바로 표시에 써도 안전합니다.</summary>
    public string PlayerId { get; private set; } = "";

    /// <summary>UILobby.ClickGameStartButton()이 씬을 넘어가기 직전에 호출합니다. 직접 호출할 일은
    /// 거의 없습니다.</summary>
    public void SetPlayerId(string id)
    {
        PlayerId = id ?? "";
    }

    [Header("화면 페이드 (풀스크린 페이드 인/아웃)")]
    [Tooltip("화면 전체를 덮는 Image가 붙은 CanvasGroup입니다. 반드시 연결하세요 - 비어있으면 " +
              "FadeOut()/FadeIn() 호출 시 바로 NullReferenceException이 납니다(연결을 빠뜨렸다는 게 " +
              "바로 드러나도록 하기 위해 일부러 방어 코드를 넣지 않았습니다).")]
    [SerializeField] CanvasGroup _fadeCanvasGroup;
    [Tooltip("게임을 처음 시작했을 때(이 스크립트의 Awake() - 사실상 게임의 첫 씬이 켜지는 순간), 화면이 " +
              "완전히 까만 상태에서 이 시간(초)에 걸쳐 서서히 드러나도록(FadeIn) 합니다. 씬을 다시 불러올 " +
              "때마다가 아니라 게임 세션 전체에서 딱 한 번만(Instance가 처음 만들어질 때) 재생됩니다 - " +
              "두 번째 GameManager는 이 Awake() 블록에 도달하기 전에 Destroy되기 때문입니다.")]
    public float bootFadeInDuration = 1f;

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

    [Header("리스폰 (사망 시 위치만 초기화 - 씬을 다시 불러오지 않음)")]
    [Tooltip("리스폰 후 화면이 다시 보이기까지(FadeIn) 걸리는 시간(초)입니다. 사망 모션을 기다리는 시간과 " +
              "화면이 까매지는 시간은 위 [게임 오버] 항목의 deathAnimationDelay/gameOverFadeOutDuration을 " +
              "그대로 재사용합니다(같은 과정이기 때문입니다) - 이 필드는 그 뒤 다시 밝아지는 시간만 별도로 " +
              "관리합니다.")]
    public float respawnFadeInDuration = 1f;

    [Header("로딩 화면 (LoadSceneWithFade() 전용)")]
    [Tooltip("씬을 비동기로 불러오는 동안 표시할 로딩 UI 루트 오브젝트입니다. 평소엔 비활성화해두세요 - " +
              "화면이 완전히 까매진(FadeOut 완료) 직후 자동으로 켜지고, 새 씬 진입 직후(FadeIn 시작 " +
              "전) 자동으로 꺼집니다. 비워두면 로딩 화면 없이 조용히 로드만 진행됩니다.")]
    [SerializeField] GameObject _loadingRoot;
    [Tooltip("로딩 진행률(0~1)을 보여주는 슬라이더입니다. Interactable은 꺼두세요(표시 전용).")]
    [SerializeField] Slider _loadingSlider;
    [Tooltip("로딩 진행률을 퍼센트 텍스트로 보여줍니다. 예: \"63%\"")]
    [SerializeField] TextMeshProUGUI _loadingPercentText;

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
            // 게임을 막 시작한 순간이므로, 우선 화면을 완전히 까맣게 만들어둔 뒤(알파 1) 곧바로
            // bootFadeInDuration에 걸쳐 FadeIn()으로 서서히 드러냅니다 - FadeIn()이 알아서 interactable/
            // blocksRaycasts를 끝나는 시점에 정리해주므로 여기서는 시작 상태만 맞춰주면 됩니다.
            _fadeCanvasGroup.alpha = 1f;
            _fadeCanvasGroup.interactable = false;
            _fadeCanvasGroup.blocksRaycasts = true;
            FadeIn(bootFadeInDuration);
        }

        if (_loadingRoot != null) _loadingRoot.SetActive(false);

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
    /// 아무 것도 하지 않고 조용히 넘어갑니다. LoadSceneWithFade()로 불러온 씬의 페이드 인은 이 이벤트가
    /// 아니라 LoadSceneWithFadeRoutine() 코루틴이 직접 처리합니다(파일 상단 [로딩 화면] 문단의
    /// [중요] 참고) - 그래서 여기서는 게임 오버 재시작 경로만 다룹니다.</summary>
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

    /// <summary>플레이어가 사망했을 때 호출하세요(PlayerController.Die() 참고). deathAnimationDelay초
    /// (사망 모션이 끝날 때까지, 실시간 기준) 기다린 뒤 화면을 gameOverFadeOutDuration초에 걸쳐
    /// FadeOut하고, 화면이 완전히 까매지면 player.Respawn()으로 위치/HP/MP를 되돌리고 지금 씬에 살아있는
    /// 모든 몬스터를 풀피로 회복시킨 뒤 respawnFadeInDuration초에 걸쳐 FadeIn합니다. TriggerGameOver()
    /// (씬을 완전히 다시 불러오는 기존 경로)와 달리 씬을 다시 불러오지 않으므로 퀘스트/인벤토리/레벨 등
    /// 진행도가 그대로 유지됩니다.</summary>
    public void TriggerRespawn(PlayerController player)
    {
        StartCoroutine(RespawnRoutine(player));
    }

    private IEnumerator RespawnRoutine(PlayerController player)
    {
        // 사망 모션이 다 재생될 때까지 먼저 기다립니다 - GameOverRoutine()과 같은 이유로
        // WaitForSecondsRealtime을 사용합니다(Time.timeScale이 0이 되어도 영향받지 않음).
        yield return new WaitForSecondsRealtime(deathAnimationDelay);

        yield return FadeOut(gameOverFadeOutDuration).WaitForCompletion();

        // 화면이 완전히 까매진 상태에서 위치/HP/MP를 되돌리고 몬스터를 회복시키므로, 플레이어에게는
        // 순간이동이나 몬스터 체력바가 갑자기 차는 모습이 전혀 보이지 않습니다.
        if (player != null) player.Respawn();
        HealAllAliveMonsters();

        FadeIn(respawnFadeInDuration);
    }

    /// <summary>지금 씬에 등록되어 있는(MonsterActivationManager.Monsters) 모든 몬스터 중, 아직 죽지
    /// 않은 몬스터(일반 MonsterFSM + MiddleSlimeBoss)를 전부 풀피로 회복시킵니다. 이미 죽었거나(Die/Dead
    /// 상태) 죽는 중(dieDelay 대기, 시체)인 몬스터는 건너뜁니다 - 거리 때문에 SetActive(false)로
    /// 비활성화되어 있던 몬스터도 등록은 그대로 유지되므로 함께 회복됩니다.</summary>
    private void HealAllAliveMonsters()
    {
        foreach (MonsterActivation activation in MonsterActivationManager.Instance.Monsters)
        {
            if (activation == null) continue;

            GameObject monsterObject = activation.gameObject;

            MonsterFSM fsm = monsterObject.GetComponent<MonsterFSM>();
            if (fsm != null)
            {
                if (fsm.CurrentState == MonsterFSM.State.Die) continue;

                MonsterStats fsmStats = monsterObject.GetComponent<MonsterStats>();
                if (fsmStats != null) fsmStats.Heal(fsmStats.MaxHP);
                continue;
            }

            MiddleSlimeBoss boss = monsterObject.GetComponent<MiddleSlimeBoss>();
            if (boss != null)
            {
                if (boss.IsDead) continue;

                MonsterStats bossStats = monsterObject.GetComponent<MonsterStats>();
                if (bossStats != null) bossStats.Heal(bossStats.MaxHP);
            }
        }
    }

    /// <summary>화면을 fadeOutDuration초에 걸쳐 까맣게 만든 뒤 sceneName을 불러오고, 그 씬이 다
    /// 준비되면 fadeInDuration초에 걸쳐 다시 보이게 합니다. 로비 → 인게임처럼 일반적인 씬 전환에 그대로
    /// 쓰세요(UILobby.cs 참고, 파일 상단 [일반적인 씬 전환] 참고). 게임 오버 재시작(TriggerGameOver())과는
    /// 완전히 독립적인 경로입니다.
    /// bgmNameOnLoad를 넘기면(Resources/BGM/ 아래 클립 이름), 화면이 까매지기 시작하는 것과 정확히 같은
    /// 순간 지금 재생 중이던 BGM도 fadeOutDuration에 걸쳐 페이드아웃되어 로딩 내내 조용해지고, 새 씬
    /// 로딩이 끝나 화면이 다시 fadeInDuration에 걸쳐 밝아지는 바로 그 순간 이 곡이 같은 길이로
    /// 페이드인됩니다 - 화면과 음악이 항상 같은 타이밍으로 어두워지고 밝아집니다. SetFieldBGM()을
    /// 거치므로 이후 전투가 벌어졌다 끝나도 이 곡으로 정상적으로 되돌아옵니다. 비워두면(기본값 null)
    /// BGM은 전혀 건드리지 않습니다.</summary>
    public void LoadSceneWithFade(string sceneName, float fadeOutDuration, float fadeInDuration, string bgmNameOnLoad = null)
    {
        StartCoroutine(LoadSceneWithFadeRoutine(sceneName, fadeOutDuration, fadeInDuration, bgmNameOnLoad));
    }

    private IEnumerator LoadSceneWithFadeRoutine(string sceneName, float fadeOutDuration, float fadeInDuration, string bgmNameOnLoad)
    {
        // 화면이 까매지기 시작하는 것과 같은 프레임에 BGM 페이드아웃도 함께 시작합니다 - 둘 다
        // FadeOut()/StopBGM()을 "시작만" 시키고(둘 다 코루틴 내부에서 알아서 진행되는 논블로킹 호출),
        // 아래 WaitForCompletion()은 화면 쪽만 기다립니다(화면 fadeOutDuration과 BGM fadeOutDuration이
        // 같은 값이라 실제로는 같은 순간에 끝납니다).
        if (!string.IsNullOrEmpty(bgmNameOnLoad))
        {
            SoundManager.Instance.StopBGM(fadeOutDuration);
        }

        yield return FadeOut(fadeOutDuration).WaitForCompletion();

        SetLoadingProgress(0f);
        if (_loadingRoot != null) _loadingRoot.SetActive(true);

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false; // 90%(에셋 로드 완료)에서 멈춰뒀다가, 아래에서 직접 활성화 시점을 제어합니다.

        // AsyncOperation.progress는 에셋을 다 불러와도 1이 아니라 0.9에서 멈추는 유니티 고유의 동작이라
        // (allowSceneActivation을 true로 바꿔야 그 이후 0.9→1 구간의 실제 씬 전환이 일어납니다),
        // 0~0.9 구간을 0~1로 정규화해서 슬라이더/텍스트가 자연스럽게 0%→100%로 채워지도록 합니다.
        while (op.progress < 0.9f)
        {
            SetLoadingProgress(op.progress / 0.9f);
            yield return null;
        }

        SetLoadingProgress(1f);
        op.allowSceneActivation = true;

        while (!op.isDone) yield return null; // 실제 씬 전환(및 새 씬의 Awake/Start)이 끝날 때까지 기다립니다.

        // FloatingText/데미지 숫자가 게임플레이 중 처음 뜰 때 겪던 순간 렉(폰트 아틀라스 생성 +
        // 셰이더 컴파일이 그 순간에 몰려서 발생)을 없애기 위해, 아직 로딩 화면이 가리고 있는 지금
        // 미리 한 번씩 "보이지 않게" 렌더링시켜 그 비용을 여기서 끝내둡니다(각 Prewarm() 참고).
        // 데미지 숫자는 UI가 아니라 월드 스페이스 3D TextMeshPro라 셰이더 자체가 달라서 따로
        // 워밍업해야 합니다. 워밍업 자체는 알파 0으로 그려지므로 로딩 화면을 끄는 타이밍과 겹쳐도
        // 화면에 아무 영향이 없어, 아래 절차를 기다리지 않고 발사 후 신경 쓰지 않아도
        // (fire-and-forget) 안전합니다.
        FloatingTextManager.Instance.Prewarm();
        DamageNumberManager.Instance.Prewarm();

        // 로딩 화면을 먼저 끄고, 바로 다음 줄에서 페이드 인을 시작합니다 - 이 둘의 순서가 코드 한
        // 줄 한 줄로 보장되므로(파일 상단 [로딩 화면]의 [중요] 참고), sceneLoaded 이벤트에 맡겼을 때
        // 생기던 "로딩 화면에 페이드 인이 가려지는" 타이밍 문제가 원천적으로 발생할 수 없습니다.
        if (_loadingRoot != null) _loadingRoot.SetActive(false);
        FadeIn(fadeInDuration);

        // 화면이 다시 밝아지는 것과 같은 순간, 새 씬의 BGM도 같은 길이로 페이드인을 시작합니다. SoundManager는
        // DontDestroyOnLoad라 씬이 바뀌어도 그대로 살아있으므로 여기서 바로 접근해도 안전합니다.
        if (!string.IsNullOrEmpty(bgmNameOnLoad))
        {
            SoundManager.Instance.SetFieldBGM(bgmNameOnLoad, fadeInDuration);
        }
    }

    /// <summary>로딩 슬라이더/퍼센트 텍스트를 progress01(0~1)에 맞춰 갱신합니다. 필드가 비어있으면
    /// (로딩 화면을 안 쓰는 경우) 조용히 넘어갑니다.</summary>
    private void SetLoadingProgress(float progress01)
    {
        progress01 = Mathf.Clamp01(progress01);

        if (_loadingSlider != null) _loadingSlider.value = progress01;
        if (_loadingPercentText != null) _loadingPercentText.text = $"{Mathf.RoundToInt(progress01 * 100f)}%";
    }
}