// ============================================================================
// SoundManager.cs
// ----------------------------------------------------------------------------
// 프로젝트의 모든 사운드(배경음악 BGM, 효과음 SFX)를 한 곳에서 재생/관리하는 싱글턴
// 매니저입니다. VFXManager/DamageNumberManager와 같은 구조로, 클립을 "Resources/BGM",
// "Resources/SFX" 폴더 아래에 모아두면 이름(문자열)만으로 어디서든 재생할 수 있습니다.
//
// [BGM vs SFX - 왜 다르게 처리하나]
//   BGM은 항상 최대 1곡만 재생되고(다음 곡으로 바뀔 때 끊기지 않게 크로스페이드), 길이도 길고
//   미리 몇 개를 만들어둘 필요가 없어서 AudioSource 2개(자연스러운 교차 재생용)만 있으면 됩니다.
//   반대로 SFX는 전투 중 아주 짧은 시간에 여러 개가 동시다발적으로(콤보 타격, 범위 공격 등)
//   겹쳐서 재생돼야 하기 때문에, VFXManager가 이펙트에 쓰는 것과 같은 이유로 오브젝트 풀링
//   (GameObjectPool)을 사용합니다 - AudioSource가 붙은 빈 오브젝트("보이스")를 여러 개 풀링해두고
//   매번 Instantiate/Destroy하지 않고 재사용합니다.
//
// [씬/프로젝트 준비]
//   1) Assets/Resources/BGM, Assets/Resources/SFX 폴더에 오디오 클립을 넣어두세요
//      (이미 만들어두셨다고 하셨으니 그대로 쓰시면 됩니다).
//      예) Assets/Resources/BGM/Field_Theme.mp3, Assets/Resources/SFX/Hit_Slash.wav
//   2) 씬에 미리 배치해둘 필요 없습니다 - 아무 스크립트에서나 SoundManager.Instance를 처음
//      호출하는 순간 자동으로 생성되고, 씬이 바뀌어도 파괴되지 않습니다(DontDestroyOnLoad).
//      풀 크기나 기본 볼륨 등을 인스펙터에서 직접 조절하고 싶다면 빈 오브젝트를 만들어 이
//      스크립트를 미리 붙여 씬에 배치해도 동일하게 동작합니다.
//
// [사용 예시]
//   SoundManager.Instance.PlayBGM("Field_Theme");                       // 크로스페이드로 배경음악 전환
//   SoundManager.Instance.StopBGM();                                    // 페이드아웃하며 정지
//   SoundManager.Instance.PlaySFX("UI_Click");                          // 2D(비위치) 효과음
//   SoundManager.Instance.PlaySFX("Hit_Slash", hitPosition);            // 3D(위치 기반) 효과음
//   SoundManager.Instance.PlaySFX("Hit_Slash", hitPosition, 1f, 0.1f);  // 피치를 ±10% 무작위로
//   GameObject loop = SoundManager.Instance.PlaySFXAttached("Footstep_Loop", playerTransform, 1f, true);
//   SoundManager.Instance.StopSFX(loop);                                // attached + loop인 사운드는 직접 정지
//
// [GameObjectPool]
//   실제 SFX 풀링 로직은 VFXManager/DamageNumberManager와 동일한 범용 GameObjectPool을 그대로
//   재사용합니다(GameObjectPool.cs 참고). VFX 프리팹과 달리 SFX "보이스"는 전부 AudioSource
//   하나만 있으면 되는 동일한 모양이라, 이름별로 풀을 나누지 않고 보이스 풀 하나를 공유합니다.
//
// [기존 코드에 연결하려면]
//   예를 들어 AttackHitbox가 타격 VFX를 재생하는 자리 옆에
//     SoundManager.Instance.PlaySFX(hitSfxName, vfxPosition, 1f, 0.08f);
//   한 줄만 추가하면 타격음까지 같이 재생됩니다. 원하시면 AttackHitbox/MonsterFSM(피격,사망)/
//   PlayerController(콤보 스윙) 등 기존 스크립트들에도 이어서 연결해드릴 수 있습니다.
//
// [전투 음악 자동 전환 - NotifyCombatEngaged / NotifyCombatDisengaged / SetFieldBGM]
//   "지금 전투 중인가"도 이 매니저가 함께 판단합니다. 몬스터/보스가 교전을 시작/종료할 때마다 직접
//   PlayBGM을 부르지 않고 NotifyCombatEngaged(this)/NotifyCombatDisengaged(this)만 호출하면,
//   engagedSources(교전 중인 대상들의 집합)의 개수가 0→1이 되는 순간에만 combatBgmName을 재생하고,
//   1→0이 된 뒤 combatExitDelay초 동안 아무도 다시 교전하지 않을 때만 currentFieldBgm(가장 최근에
//   SetFieldBGM으로 알려준 구역 음악)으로 되돌립니다. 여러 몬스터가 동시에 싸우다 한 마리만 죽어도
//   음악이 끊기지 않고, 몬스터를 잡자마자 바로 다음 몬스터에게 감지돼도 음악이 깜빡이지 않습니다.
//   - MonsterFSM.ChangeState(): 교전 상태(Trace/Chase/BodyAttack/SplashAttack/Hit) 진입/이탈 시 호출.
//   - MiddleSlimeBoss.Update(): IsTargetInDetectRange() 조건으로 호출(별도 FSM이라 직접 연결).
//   - BGMZoneTrigger: 플레이어가 구역에 들어올 때 SetFieldBGM()으로 "평상시 음악"을 갱신.
//
// [게임 시작 시 기본 배경음악 - startBgmName]
//   이 매니저가 처음 생성되는 순간(=Awake(), 사실상 게임을 시작하자마자) startBgmName이 채워져
//   있으면 SetFieldBGM()으로 자동 재생합니다. 예: startBgmName = "Field_Theme"로 설정해두고, 마을
//   경계에는 zoneBgmName = "Village_Theme", revertOnExit = true, exitBgmName = "Field_Theme"인
//   BGMZoneTrigger를 배치하면 - 게임 시작 즉시 Field_Theme이 깔리고, 마을에 들어가면 Village_Theme으로,
//   마을에서 나가면 다시 Field_Theme으로 자동 전환됩니다. 씬에 SoundManager를 미리 배치하지 않았다면
//   이 값은 인스펙터에서 미리 설정해둘 수 없으므로, startBgmName을 쓰려면 시작 씬에 빈 오브젝트를
//   만들어 SoundManager를 미리 붙여두고 그 값을 지정해두세요([씬/프로젝트 준비] 2번 참고).
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    private const string BgmFolder = "BGM";
    private const string SfxFolder = "SFX";

    [Header("볼륨")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float bgmVolume = 1f;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    [Header("BGM")]
    [Tooltip("PlayBGM()에 fadeDuration을 따로 넘기지 않았을 때 사용할 기본 크로스페이드 시간(초).")]
    public float defaultBgmFadeDuration = 1.5f;
    [Tooltip("게임을 시작하자마자(이 매니저가 처음 생성되는 순간) 자동으로 재생할 기본 배경음악입니다 - " +
              "Resources/BGM/ 아래 파일명과 일치해야 합니다. BGMZoneTrigger가 하나도 없어도 이 곡이 항상 " +
              "깔려있고, 마을 등 특정 구역에 들어가면 그 구역의 BGMZoneTrigger가 SetFieldBGM()으로 잠시 " +
              "덮어썼다가, 그 구역을 나가면(BGMZoneTrigger의 revertOnExit + exitBgmName을 이 값과 같은 " +
              "이름으로 설정) 다시 이 곡으로 돌아오게 구성하세요. 비워두면 게임 시작 시 아무 것도 " +
              "재생하지 않고, 첫 BGMZoneTrigger를 만날 때까지 조용합니다.")]
    public string startBgmName;

    [Header("전투 음악 자동 전환")]
    [Tooltip("교전 중(몬스터 1마리 이상과 싸우는 중)일 때 재생할 곡입니다. Resources/BGM/ 아래 파일명과 일치해야 합니다.")]
    public string combatBgmName = "Combat_Theme";
    [Tooltip("평상시 음악 → 전투 음악으로 바뀔 때의 크로스페이드 시간(초). 전투 시작은 긴장감을 위해 " +
              "필드 복귀보다 짧게(빠르게) 잡는 걸 추천합니다.")]
    public float combatEnterFadeDuration = 0.6f;
    [Tooltip("전투 음악 → 평상시 음악으로 바뀔 때의 크로스페이드 시간(초).")]
    public float fieldReturnFadeDuration = 1.5f;
    [Tooltip("마지막 몬스터와의 교전이 끝난 뒤, 이 시간(초)만큼 아무도 다시 교전하지 않아야 실제로 " +
              "평상시 음악으로 되돌립니다. 몬스터를 잡자마자 근처 다른 몬스터에게 바로 감지되는 " +
              "상황에서 음악이 깜빡이는 것을 막아줍니다.")]
    public float combatExitDelay = 4f;

    [Header("UI 클릭 효과음")]
    [Tooltip("메뉴/인벤토리/캐릭터정보/옵션/퀘스트 창 등 게임 안의 모든 버튼이 공통으로 재생하는 클릭 " +
              "효과음입니다(Resources/SFX/ 아래 클립 이름과 일치해야 함) - PlayUIClickSfx()가 이 값을 " +
              "재생합니다. 버튼마다 다른 소리를 내고 싶다면 이 공용 메서드 대신 PlaySFX(다른 클립 이름)를 " +
              "직접 호출하세요. 대화/퀘스트 선택지(UIDialogueChoiceButton)는 이 값 대신 자신만의 " +
              "choiceSfxName을 따로 갖고 있습니다.")]
    public string uiClickSfxName = "UI_Click";

    [Header("SFX 풀")]
    [Tooltip("미리 만들어서 대기시켜둘 SFX 보이스(AudioSource) 개수. 전투 중 처음 재생할 때 생기는 순간적인 끊김을 막아줍니다.")]
    public int sfxVoicePrewarmCount = 8;
    [Tooltip("대기 풀에 보관할 수 있는 최대 보이스 개수. 초과분은 반납 시 Destroy됩니다.")]
    public int sfxVoiceMaxPoolSize = 32;
    [Tooltip("3D(위치 기반) SFX가 이 거리(미터) 안에서는 최대 음량으로 들립니다.")]
    public float sfxMinDistance = 1f;
    [Tooltip("3D(위치 기반) SFX가 이 거리(미터)를 넘어가면 들리지 않습니다.")]
    public float sfxMaxDistance = 25f;
    [Tooltip("켜두면 재생/반납 등 동작을 콘솔에 로그로 남깁니다.")]
    public bool debugLog = false;

    private static SoundManager instance;
    public static SoundManager Instance
    {
        get
        {
            if (instance == null)
            {
                // 씬에 이미 배치해둔 인스턴스가 있으면 그걸 쓰고, 없으면 새로 만듭니다.
                instance = FindFirstObjectByType<SoundManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("SoundManager");
                    instance = go.AddComponent<SoundManager>();
                }
            }
            return instance;
        }
    }

    /// <summary>인스턴스를 새로 만들지 않고 "이미 있으면" 그것만 돌려줍니다. 씬 종료/앱 종료 시점처럼
    /// 새로 만들 필요가 없는 정리(해제) 코드에서 Instance 대신 이걸 사용하세요(MonsterActivation.OnDestroy와
    /// 동일한 이유 - MonsterFSM/MiddleSlimeBoss의 OnDestroy 안전장치에서 씁니다).</summary>
    public static SoundManager InstanceIfExists => instance;

    // ------------------------------------------------------------------
    // BGM - AudioSource 2개를 번갈아 쓰면서 크로스페이드합니다.
    // ------------------------------------------------------------------
    private AudioSource bgmSourceA;
    private AudioSource bgmSourceB;
    private AudioSource activeBgmSource;
    private Coroutine bgmFadeRoutine;
    private string currentBgmName;

    // ------------------------------------------------------------------
    // 전투 음악 자동 전환 - "지금 교전 중인 몬스터/보스"를 세어서(참조 카운팅) 1마리 이상이면 전투
    // 음악을, 0마리면(그리고 combatExitDelay 동안 아무도 다시 교전하지 않으면) 구역 음악을 재생합니다.
    // ------------------------------------------------------------------
    // 몬스터가 여러 마리 동시에 플레이어를 공격할 수 있어서, 개별 몬스터가 직접 PlayBGM/StopBGM을
    // 호출하지 않고 "나 지금 교전 중" / "나 이제 아님"만 여기 알려줍니다 - A가 죽어서 필드 음악으로
    // 되돌리는 순간 아직 B가 싸우고 있는데도 음악이 끊기는 문제를 막기 위함입니다.
    private readonly HashSet<Object> engagedSources = new HashSet<Object>();
    // BGMZoneTrigger가 마지막으로 알려준 "평상시(비전투) 음악" 이름입니다. 전투가 끝나면 이 값으로 되돌아갑니다.
    private string currentFieldBgm;
    private Coroutine exitCombatRoutine;

    // ------------------------------------------------------------------
    // SFX - 클립 캐시 + 보이스(AudioSource) 오브젝트 풀
    // ------------------------------------------------------------------
    private readonly Dictionary<string, AudioClip> bgmClipCache = new Dictionary<string, AudioClip>();
    private readonly Dictionary<string, AudioClip> sfxClipCache = new Dictionary<string, AudioClip>();
    private readonly HashSet<string> missingBgmWarned = new HashSet<string>();
    private readonly HashSet<string> missingSfxWarned = new HashSet<string>();

    private GameObject sfxVoiceTemplate;
    private GameObjectPool sfxVoicePool;
    private Transform poolRoot;

    // 자동 반납 타이머와 "지금 실제로 사용 중인 보이스인지"를 추적합니다 (VFXManager와 동일한 이유 -
    // 자동 반납 전에 누군가 먼저 StopSFX()로 반납하는 경우의 이중 반납을 막기 위함입니다).
    private readonly Dictionary<int, Coroutine> pendingSfxReleases = new Dictionary<int, Coroutine>();
    private readonly HashSet<int> activeSfxIds = new HashSet<int>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            // 씬 전환 등으로 인해 두 번째 SoundManager가 생기면 기존 것을 유지하고 새로 생긴 걸 제거합니다.
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        poolRoot = new GameObject("Pools").transform;
        poolRoot.SetParent(transform, false);

        SetupBgmSources();
        SetupSfxVoicePool();

        // 게임 시작과 동시에 기본 배경음악을 깔아둡니다. SetFieldBGM()을 거치는 이유는 PlayBGM()을 직접
        // 부르면 currentFieldBgm이 갱신되지 않아서, 이후 전투가 한 번이라도 벌어지면 "돌아갈 곡"이 없어
        // 정지해버리기 때문입니다 - SetFieldBGM()을 쓰면 이 곡이 처음부터 currentFieldBgm으로 기억되어
        // 있으니 전투 종료 후에도 정상적으로 이 곡으로 복귀합니다.
        if (!string.IsNullOrEmpty(startBgmName))
        {
            SetFieldBGM(startBgmName);
        }
    }

    // ------------------------------------------------------------------
    // 외부에서 호출하는 BGM API
    // ------------------------------------------------------------------

    /// <summary>배경음악을 재생합니다. 이미 다른 곡이 재생 중이면 fadeDuration에 걸쳐 자연스럽게
    /// 크로스페이드됩니다. 같은 곡이 이미 재생 중이면 아무 것도 하지 않습니다.</summary>
    public void PlayBGM(string clipName, float fadeDuration = -1f, bool loop = true)
    {
        if (clipName == currentBgmName) return;

        AudioClip clip = GetClip(clipName, BgmFolder, bgmClipCache, missingBgmWarned);
        if (clip == null) return;

        float duration = fadeDuration >= 0f ? fadeDuration : defaultBgmFadeDuration;

        AudioSource outgoing = activeBgmSource;
        AudioSource incoming = activeBgmSource == bgmSourceA ? bgmSourceB : bgmSourceA;

        incoming.clip = clip;
        incoming.loop = loop;
        incoming.volume = 0f;
        incoming.Play();

        if (bgmFadeRoutine != null) StopCoroutine(bgmFadeRoutine);
        bgmFadeRoutine = StartCoroutine(CrossfadeBgm(outgoing, incoming, duration));

        activeBgmSource = incoming;
        currentBgmName = clipName;

        if (debugLog) Debug.Log($"[SoundManager] BGM '{clipName}' 재생 (크로스페이드 {duration}초)");
    }

    /// <summary>재생 중인 배경음악을 fadeDuration에 걸쳐 페이드아웃하며 정지합니다.</summary>
    public void StopBGM(float fadeDuration = -1f)
    {
        if (string.IsNullOrEmpty(currentBgmName)) return;

        float duration = fadeDuration >= 0f ? fadeDuration : defaultBgmFadeDuration;

        if (bgmFadeRoutine != null) StopCoroutine(bgmFadeRoutine);
        bgmFadeRoutine = StartCoroutine(FadeOutAndStopBgm(activeBgmSource, duration));

        currentBgmName = null;
    }

    /// <summary>지금 재생 중인 BGM의 이름입니다. 재생 중이 아니면 null입니다.</summary>
    public string CurrentBGMName => currentBgmName;

    // ------------------------------------------------------------------
    // 외부에서 호출하는 전투 음악 자동 전환 API
    // ------------------------------------------------------------------

    /// <summary>지금 전투 중(교전 중인 몬스터가 1마리 이상)인지 여부입니다.</summary>
    public bool IsInCombat => engagedSources.Count > 0;

    /// <summary>몬스터/보스가 교전을 시작할 때 호출하세요(예: MonsterFSM이 Trace/Chase 등으로 전환될 때,
    /// MiddleSlimeBoss가 탐지 범위에 타겟을 포착했을 때). 이미 다른 몬스터와 교전 중이었다면(=이미 전투
    /// 음악이 재생 중이라면) 카운트만 올리고 아무 것도 하지 않습니다.</summary>
    public void NotifyCombatEngaged(Object source)
    {
        if (source == null) return;

        bool wasEmpty = engagedSources.Count == 0;
        if (!engagedSources.Add(source)) return; // 이미 등록되어 있었다면 중복 처리하지 않습니다.

        if (wasEmpty)
        {
            if (exitCombatRoutine != null)
            {
                StopCoroutine(exitCombatRoutine);
                exitCombatRoutine = null;
            }

            PlayBGM(combatBgmName, combatEnterFadeDuration);
            if (debugLog) Debug.Log($"[SoundManager] 전투 시작 → '{combatBgmName}' 재생 (교전 시작: {source.name})", this);
        }
    }

    /// <summary>몬스터/보스가 교전을 벗어날 때 호출하세요(타겟을 놓치고 복귀, 사망 등). 등록되어 있지
    /// 않은 소스를 넘겨도(예: 애초에 교전한 적이 없거나 이미 해제됐다면) 안전하게 무시됩니다.</summary>
    public void NotifyCombatDisengaged(Object source)
    {
        if (source == null) return;
        if (!engagedSources.Remove(source)) return;

        if (engagedSources.Count == 0)
        {
            if (exitCombatRoutine != null) StopCoroutine(exitCombatRoutine);
            exitCombatRoutine = StartCoroutine(ExitCombatAfterDelay());
            if (debugLog) Debug.Log($"[SoundManager] 마지막 교전 종료 (해제: {source.name}) - {combatExitDelay}초 뒤 평상시 음악으로 복귀 예정", this);
        }
    }

    /// <summary>BGMZoneTrigger가 플레이어의 구역 진입/이탈 시 호출합니다. 지금 전투 중이 아니라면 즉시
    /// 이 음악으로 전환하고, 전투 중이라면 지금은 바꾸지 않고 "전투가 끝나면 돌아갈 음악"으로만
    /// 기억해둡니다.</summary>
    public void SetFieldBGM(string bgmName, float fadeDuration = -1f)
    {
        currentFieldBgm = bgmName;

        if (IsInCombat)
        {
            if (debugLog) Debug.Log($"[SoundManager] 구역 음악이 '{bgmName}'(으)로 바뀌었지만 아직 전투 중이라 전투가 끝난 뒤 적용됩니다.", this);
            return;
        }

        PlayBGM(bgmName, fadeDuration);
    }

    // ------------------------------------------------------------------
    // 외부에서 호출하는 SFX API
    // ------------------------------------------------------------------

    /// <summary>2D(비위치) 효과음을 재생합니다. UI 클릭음처럼 특정 위치와 상관없이 항상 같은
    /// 크기로 들려야 하는 소리에 사용하세요.</summary>
    public void PlaySFX(string clipName, float volume = 1f, float pitchVariation = 0f)
    {
        PlaySFXInternal(clipName, transform.position, spatial: false, volume, pitchVariation);
    }

    /// <summary>3D(위치 기반) 효과음을 재생합니다. 그 위치에서 들리도록 공간감(거리에 따른 감쇠)이
    /// 적용됩니다 - 타격음, 발소리, 폭발음 등 씬 안의 특정 지점에서 나는 소리에 사용하세요.</summary>
    public void PlaySFX(string clipName, Vector3 position, float volume = 1f, float pitchVariation = 0f)
    {
        PlaySFXInternal(clipName, position, spatial: true, volume, pitchVariation);
    }

    /// <summary>uiClickSfxName을 2D로 재생합니다. 씬 어디서든(로비/인게임 모두) 존재하는 이 매니저를
    /// 통해서만 재생하므로, UICanvas처럼 특정 씬에만 있는 허브에 이 기능을 두는 것보다 안전합니다 -
    /// 각 UI 스크립트의 ClickXButton() 맨 앞에서 SoundManager.Instance.PlayUIClickSfx();만 호출하면
    /// 됩니다. uiClickSfxName이 비어있으면 조용히 아무 것도 하지 않습니다.</summary>
    public void PlayUIClickSfx()
    {
        if (string.IsNullOrEmpty(uiClickSfxName)) return;
        PlaySFX(uiClickSfxName);
    }

    /// <summary>parent를 계속 따라다니는 효과음을 재생합니다 (발소리 루프, 캐릭터에 붙는 사운드 등).
    /// loop가 true면 자동으로 반납되지 않으니, 다 쓰면 반드시 StopSFX(반환값)을 호출해서 직접
    /// 반납해주세요. loop가 false면 클립 길이가 지나면 자동으로 반납됩니다.</summary>
    public GameObject PlaySFXAttached(string clipName, Transform parent, float volume = 1f, bool loop = false)
    {
        AudioClip clip = GetClip(clipName, SfxFolder, sfxClipCache, missingSfxWarned);
        if (clip == null) return null;

        Vector3 worldPosition = parent != null ? parent.position : transform.position;
        GameObject voice = sfxVoicePool.Get(worldPosition, Quaternion.identity, parent);

        AudioSource source = voice.GetComponent<AudioSource>();
        ConfigureVoice(source, clip, volume, pitch: 1f, spatial: true, loop);
        source.Play();

        int id = voice.GetInstanceID();
        activeSfxIds.Add(id);

        if (!loop)
        {
            Coroutine co = StartCoroutine(AutoReleaseSfxRoutine(voice, clip.length));
            pendingSfxReleases[id] = co;
        }
        // loop가 true면 자동 반납 타이머를 걸지 않습니다 - StopSFX()로 직접 반납해야 합니다.

        if (debugLog) Debug.Log($"[SoundManager] SFX '{clipName}' 재생 (parent='{parent?.name}'에 부착, loop={loop})", voice);

        return voice;
    }

    /// <summary>자동 반납 시간이 되기 전에 직접 반납하고 싶을 때 호출하세요 (특히 PlaySFXAttached를
    /// loop=true로 재생한 경우 필수입니다). 이미 반납된 보이스를 다시 넘기면 안전하게 무시됩니다.</summary>
    public void StopSFX(GameObject voice)
    {
        if (voice == null) return;

        int id = voice.GetInstanceID();
        if (pendingSfxReleases.TryGetValue(id, out Coroutine co))
        {
            StopCoroutine(co);
            pendingSfxReleases.Remove(id);
        }

        ReleaseSfxVoice(voice);
    }

    // ------------------------------------------------------------------
    // 외부에서 호출하는 볼륨 API
    // ------------------------------------------------------------------

    public void SetMasterVolume(float value)
    {
        masterVolume = Mathf.Clamp01(value);
        RefreshActiveBgmVolume();
    }

    public void SetBGMVolume(float value)
    {
        bgmVolume = Mathf.Clamp01(value);
        RefreshActiveBgmVolume();
    }

    public void SetSFXVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        // 이미 재생 중인 SFX에는 소급 적용되지 않습니다 - SFX는 대부분 아주 짧게 끝나기 때문에
        // 다음 PlaySFX() 호출부터 반영되는 것으로 충분합니다.
    }

    /// <summary>페이드가 진행 중이 아닐 때(즉시 반영이 필요할 때) 현재 재생 중인 BGM 소스의 볼륨을
    /// masterVolume × bgmVolume 기준으로 다시 맞춥니다.</summary>
    private void RefreshActiveBgmVolume()
    {
        if (bgmFadeRoutine != null) return; // 페이드 중이면 그 코루틴이 알아서 최신 목표 볼륨으로 수렴합니다.
        if (activeBgmSource != null && !string.IsNullOrEmpty(currentBgmName))
        {
            activeBgmSource.volume = bgmVolume * masterVolume;
        }
    }

    // ------------------------------------------------------------------
    // 내부 구현 - BGM
    // ------------------------------------------------------------------

    private void SetupBgmSources()
    {
        bgmSourceA = CreateBgmSource("BGM_A");
        bgmSourceB = CreateBgmSource("BGM_B");
        activeBgmSource = bgmSourceA;
    }

    private AudioSource CreateBgmSource(string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);

        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f; // BGM은 항상 2D
        source.volume = 0f;
        return source;
    }

    private IEnumerator CrossfadeBgm(AudioSource outgoing, AudioSource incoming, float duration)
    {
        float targetVolume = bgmVolume * masterVolume;

        if (duration <= 0f)
        {
            outgoing.Stop();
            outgoing.volume = 0f;
            incoming.volume = targetVolume;
            bgmFadeRoutine = null;
            yield break;
        }

        float startOutgoingVolume = outgoing.volume;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = t / duration;
            outgoing.volume = Mathf.Lerp(startOutgoingVolume, 0f, p);
            incoming.volume = Mathf.Lerp(0f, targetVolume, p);
            yield return null;
        }

        outgoing.Stop();
        outgoing.volume = 0f;
        incoming.volume = targetVolume;
        bgmFadeRoutine = null;
    }

    private IEnumerator FadeOutAndStopBgm(AudioSource source, float duration)
    {
        float startVolume = source.volume;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, t / duration);
            yield return null;
        }

        source.Stop();
        source.volume = 0f;
        bgmFadeRoutine = null;
    }

    private IEnumerator ExitCombatAfterDelay()
    {
        yield return new WaitForSeconds(combatExitDelay);
        exitCombatRoutine = null;

        // 대기하는 동안 다른 몬스터가 다시 교전을 시작했다면(NotifyCombatEngaged가 다시 호출됐다면)
        // engagedSources가 다시 채워져 있으므로 여기서 아무 것도 하지 않고 조용히 취소합니다.
        if (engagedSources.Count > 0) yield break;

        if (!string.IsNullOrEmpty(currentFieldBgm))
        {
            PlayBGM(currentFieldBgm, fieldReturnFadeDuration);
            if (debugLog) Debug.Log($"[SoundManager] 전투 종료 → 평상시 음악 '{currentFieldBgm}'로 복귀", this);
        }
        else
        {
            StopBGM(fieldReturnFadeDuration);
            if (debugLog) Debug.Log("[SoundManager] 전투 종료 - 되돌아갈 구역 음악이 지정되어 있지 않아 정지합니다.", this);
        }
    }

    // ------------------------------------------------------------------
    // 내부 구현 - SFX
    // ------------------------------------------------------------------

    private void SetupSfxVoicePool()
    {
        sfxVoiceTemplate = new GameObject("SFXVoice_Template");
        sfxVoiceTemplate.transform.SetParent(poolRoot, false);
        AudioSource templateSource = sfxVoiceTemplate.AddComponent<AudioSource>();
        templateSource.playOnAwake = false;
        sfxVoiceTemplate.SetActive(false);

        Transform sfxPoolParent = new GameObject("Pool_SFX").transform;
        sfxPoolParent.SetParent(poolRoot, false);

        sfxVoicePool = new GameObjectPool(sfxVoiceTemplate, sfxPoolParent, sfxVoicePrewarmCount, sfxVoiceMaxPoolSize);
    }

    private void PlaySFXInternal(string clipName, Vector3 position, bool spatial, float volume, float pitchVariation)
    {
        AudioClip clip = GetClip(clipName, SfxFolder, sfxClipCache, missingSfxWarned);
        if (clip == null) return;

        GameObject voice = sfxVoicePool.Get(position, Quaternion.identity, null);
        AudioSource source = voice.GetComponent<AudioSource>();

        float pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        ConfigureVoice(source, clip, volume, pitch, spatial, loop: false);
        source.Play();

        BeginSfxUse(voice, clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch)));

        if (debugLog) Debug.Log($"[SoundManager] SFX '{clipName}' 재생 (spatial={spatial}, position={position})", voice);
    }

    private void ConfigureVoice(AudioSource source, AudioClip clip, float volume, float pitch, bool spatial, bool loop)
    {
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume) * sfxVolume * masterVolume;
        source.pitch = pitch;
        source.loop = loop;
        source.spatialBlend = spatial ? 1f : 0f;
        source.minDistance = sfxMinDistance;
        source.maxDistance = sfxMaxDistance;
        source.rolloffMode = AudioRolloffMode.Linear;
    }

    private void BeginSfxUse(GameObject voice, float lifetime)
    {
        int id = voice.GetInstanceID();
        activeSfxIds.Add(id);

        Coroutine co = StartCoroutine(AutoReleaseSfxRoutine(voice, lifetime));
        pendingSfxReleases[id] = co;
    }

    private IEnumerator AutoReleaseSfxRoutine(GameObject voice, float delay)
    {
        yield return new WaitForSeconds(delay);
        pendingSfxReleases.Remove(voice.GetInstanceID());
        ReleaseSfxVoice(voice);
    }

    /// <summary>실제 반납을 수행합니다. activeSfxIds에서 빠져있다면(=이미 어딘가에서 반납되어
    /// 재사용됐거나, 중복 호출) 아무 것도 하지 않습니다 - 이게 이중 반납을 막는 핵심 안전장치입니다.</summary>
    private void ReleaseSfxVoice(GameObject voice)
    {
        int id = voice.GetInstanceID();
        if (!activeSfxIds.Remove(id))
        {
            if (debugLog) Debug.LogWarning("[SoundManager] SFX 보이스가 이미 반납된 상태에서 또 반납이 시도됐습니다. 무시합니다.", voice);
            return;
        }

        AudioSource source = voice.GetComponent<AudioSource>();
        if (source != null)
        {
            source.Stop();
            source.clip = null;
        }

        sfxVoicePool.Release(voice);
    }

    // ------------------------------------------------------------------
    // 내부 구현 - Resources 로드/캐싱
    // ------------------------------------------------------------------

    private AudioClip GetClip(string clipName, string folder, Dictionary<string, AudioClip> cache, HashSet<string> warned)
    {
        if (string.IsNullOrEmpty(clipName))
        {
            Debug.LogWarning("[SoundManager] 빈 이름으로 재생을 시도했습니다.");
            return null;
        }

        if (cache.TryGetValue(clipName, out AudioClip cached))
        {
            return cached;
        }

        AudioClip loaded = Resources.Load<AudioClip>($"{folder}/{clipName}");
        if (loaded == null)
        {
            // 같은 이름으로 반복 호출될 때(예: 매 프레임 히트) 콘솔이 경고로 도배되지 않도록 한 번만 띄웁니다.
            if (warned.Add(clipName))
            {
                Debug.LogWarning($"[SoundManager] 'Resources/{folder}/{clipName}' 오디오 클립을 찾을 수 없습니다. " +
                                  $"파일 이름과 경로(Assets/Resources/{folder}/ 바로 아래)를 확인해주세요.");
            }
            return null;
        }

        cache[clipName] = loaded;
        return loaded;
    }
}