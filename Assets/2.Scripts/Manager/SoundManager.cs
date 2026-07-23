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
                GameObject go = new GameObject("SoundManager");
                instance = go.AddComponent<SoundManager>();
            }
            return instance;
        }
    }

    // ------------------------------------------------------------------
    // BGM - AudioSource 2개를 번갈아 쓰면서 크로스페이드합니다.
    // ------------------------------------------------------------------
    private AudioSource bgmSourceA;
    private AudioSource bgmSourceB;
    private AudioSource activeBgmSource;
    private Coroutine bgmFadeRoutine;
    private string currentBgmName;

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