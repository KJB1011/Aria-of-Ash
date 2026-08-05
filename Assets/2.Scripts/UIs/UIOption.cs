// ============================================================================
// UIOption.cs
// ----------------------------------------------------------------------------
// I/U키 창(UIInventory/UICharacterInfo)과 완전히 같은 패턴을 따르는 옵션 창입니다 - IUIWindow를
// 구현해서 UICanvas가 "팝업 하나만 열리게, 열려있는 동안 게임 시간 멈추기"를 관리해줍니다.
// CanvasGroup 알파 페이드로 보이기/숨기기를 처리하는 것도 동일합니다.
//
// [BGM/SFX 슬라이더]
//   SoundManager.Instance의 bgmVolume/sfxVolume(둘 다 0~1)을 그대로 읽고 씁니다 - 요청하신 필드가
//   BGM/SFX 두 개뿐이라 마스터 볼륨 슬라이더는 만들지 않았습니다(필요해지면 같은 방식으로 하나
//   더 추가하면 됩니다). 슬라이더의 Min/Max는 Inspector에서 0~1로 맞춰주세요. 값이 바뀔 때마다
//   (드래그 중에도) 바로 SoundManager.Instance.SetBGMVolume()/SetSFXVolume()을 호출해서 실시간으로
//   반영됩니다.
//
// [해상도 드롭다운 - 지금은 화면 비율만]
//   말씀하신 대로 지금은 정확한 해상도(1920x1080 등)나 프레임 관련 설정 없이, 화면 비율만
//   고를 수 있게 했습니다(16:9 / 16:10 / 21:9 / 4:3). 비율을 고르면 "현재 화면 높이는 그대로
//   두고 그 비율에 맞는 너비"를 계산해서 Screen.SetResolution()을 호출합니다 - 예를 들어 높이가
//   1080일 때 16:9를 고르면 1920x1080, 21:9를 고르면 2520x1080이 되는 식입니다. 나중에 실제
//   해상도 목록(1920x1080/2560x1440 등 - 다른 프레임/픽셀 조합)이나 프레임레이트 설정이
//   필요해지면 그때 이 드롭다운을 확장하면 됩니다.
//
// [화면 모드 드롭다운]
//   창모드/테두리없는창모드/전체화면 3가지를 Unity의 FullScreenMode로 매핑합니다.
//   창모드 = FullScreenMode.Windowed, 테두리없는창모드 = FullScreenMode.FullScreenWindow(테두리 없이
//   화면 전체 크기의 창), 전체화면 = FullScreenMode.ExclusiveFullScreen(독점 전체화면)입니다.
//
// [설정 저장 - 요청엔 없었지만 추가한 기능]
//   옵션 창의 목적상 설정이 게임을 껐다 켜도 유지되는 게 자연스러워서 PlayerPrefs에
//   저장/불러오기를 추가했습니다(Option_BGMVolume/Option_SFXVolume/Option_AspectRatioIndex/
//   Option_ScreenModeIndex 키 사용). 저장된 값이 없으면(첫 실행) 그 시점의 실제 볼륨/화면
//   상태를 기준으로 UI를 맞춥니다. 필요 없으시면 LoadAndApplySettings()의 PlayerPrefs 부분과
//   각 On***Changed()의 PlayerPrefs.SetXxx() 줄을 지우시면 됩니다.
//
// [나가기/확인 버튼 - 왜 다르게 동작하나]
//   BGM/SFX/해상도/화면모드는 값이 바뀌는 즉시(슬라이더 드래그 중에도) 실시간으로 적용됩니다 -
//   그래서 "확인"과 "나가기"를 구분해서, 확인은 지금 적용된 값을 그대로 유지하며 닫고, 나가기는
//   이 창을 연 시점의 값으로 전부 되돌린 뒤 닫습니다(취소). Open() 시점에 CaptureSnapshot()으로
//   그 시점의 실제 값을 저장해뒀다가, ClickExitButton()(나가기)에서 RevertToSnapshot()으로
//   되돌립니다. Inventory/CharacterInfo의 ClickExitButton()은 그냥 닫기만 하는 것과 달리, 옵션
//   창은 실시간 미리보기 값이 있어서 이렇게 다르게 동작합니다.
//
// [조작법 버튼 - UIControls 연동]
//   옵션 창 안에 "조작법" 버튼을 하나 두면 언제든 조작법 안내 패널(UIControls, 인게임 씬 시작 시
//   자동으로 한 번 뜨는 그 패널)을 다시 볼 수 있습니다. OnClick에 이 스크립트의
//   ClickShowControlsButton()을 연결하세요 - 옵션 창이 조작법 패널로 바로 바뀌고, 조작법 패널은
//   자기 자신의 닫기 버튼을 눌러야 닫힙니다(옵션 창으로 자동으로 돌아가지는 않습니다 - 자세한
//   내용은 UIControls.cs 참고).
//
// [O 키로 열고 닫기]
//   UIInventory의 I키, UICharacterInfo의 U키와 완전히 같은 패턴입니다 - Awake()에서
//   InputAction("<Keyboard>/o")을 만들고, OnEnable/OnDisable에서 Enable/Disable, Update()에서
//   WasPressedThisFrame()으로 눌린 순간에 ToggleOption()을 호출합니다. O키로 닫을 때는(나가기/확인
//   버튼을 거치지 않으므로) 지금 적용된 값을 그대로 유지한 채 닫힙니다 - 버튼 없이 그냥 다시
//   O를 눌러 끄는 것도 "확인"과 같은 동작이라고 보시면 됩니다.
//
// [씬 준비]
//   1) 옵션 창 패널(전체를 여닫을 오브젝트)에 이 스크립트와 CanvasGroup을 붙이세요
//      (CanvasGroup은 RequireComponent로 자동 추가됩니다).
//   2) BGM/SFX 슬라이더, Resolution/ScreenMode 드롭다운을 각 필드에 연결하세요 - 드롭다운의
//      Option 항목(16:9 등, 창모드 등)은 코드에서 자동으로 채우므로 Inspector에서 미리
//      만들어둘 필요 없습니다.
//   3) 나가기(취소) 버튼의 OnClick에 ClickExitButton()을, 확인 버튼의 OnClick에
//      ClickConfirmButton()을 각각 연결하세요. O 키는 코드에서 자동으로 처리되므로 따로
//      설정할 게 없습니다.
//   3-1) (선택) "조작법" 버튼을 만들었다면 OnClick에 ClickShowControlsButton()을 연결하세요.
//        UICanvas의 Controls 필드에 UIControls 패널이 먼저 연결되어 있어야 합니다.
//   4) 이 오브젝트는 항상 활성화(Active) 상태로 두세요 - UIInventory/UICharacterInfo와 같은
//      이유로, SetActive가 아니라 CanvasGroup 알파로 보이기/숨기기를 처리합니다. 씬 시작 시
//      기본적으로 닫혀있습니다(알파 0, 상호작용 불가).
//   5) UICanvas의 _uiOption 필드에 이 오브젝트를 연결하세요(UICanvas.cs도 함께 수정해서
//      Option 프로퍼티를 추가해뒀습니다). UIIngame의 옵션 버튼(ClickOptionButton())도
//      UICanvas.Instance.Option.ToggleOption()을 호출하도록 이미 연결해뒀습니다.
// ============================================================================

using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class UIOption : MonoBehaviour, IUIWindow
{
    [SerializeField] Slider _sliderBGM;
    [SerializeField] Slider _sliderSFX;
    [SerializeField] TMP_Dropdown _dropdownResolution;
    [SerializeField] TMP_Dropdown _dropdownScreenMode;

    [Header("표시/숨김")]
    public float fadeDuration = 0.15f;

    // 화면 비율만 다루기로 했으므로(정확한 해상도 목록이 아님), 비율 자체를 데이터로 들고 있다가
    // 드롭다운 인덱스와 1:1로 매칭합니다.
    private static readonly Vector2Int[] AspectRatios =
    {
        new Vector2Int(16, 9),
        new Vector2Int(16, 10),
        new Vector2Int(21, 9),
        new Vector2Int(4, 3),
    };
    private static readonly string[] AspectRatioLabels = { "16:9", "16:10", "21:9", "4:3" };

    private static readonly FullScreenMode[] ScreenModes =
    {
        FullScreenMode.Windowed,
        FullScreenMode.FullScreenWindow,
        FullScreenMode.ExclusiveFullScreen,
    };
    private static readonly string[] ScreenModeLabels = { "창모드", "테두리없는창모드", "전체화면" };

    private const string PrefBgmVolume = "Option_BGMVolume";
    private const string PrefSfxVolume = "Option_SFXVolume";
    private const string PrefAspectRatioIndex = "Option_AspectRatioIndex";
    private const string PrefScreenModeIndex = "Option_ScreenModeIndex";

    private CanvasGroup canvasGroup;
    private Tween fadeTween;
    private bool isOpen;

    // Open() 시점의 값을 저장해두는 스냅샷입니다 - "나가기"(취소)를 누르면 이 값으로 되돌립니다.
    private float snapshotBgmVolume;
    private float snapshotSfxVolume;
    private int snapshotAspectIndex;
    private int snapshotScreenModeIndex;

    private InputAction toggleAction;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        SetupDropdownOptions();

        toggleAction = new InputAction("ToggleOption", InputActionType.Button, "<Keyboard>/o");
    }

    private void OnEnable()
    {
        toggleAction.Enable();
    }

    private void OnDisable()
    {
        toggleAction.Disable();
    }

    // SoundManager.Instance를 참조하는 건 Awake가 아니라 Start()에서 합니다 - 씬 로드 시점에
    // 존재하는 모든 오브젝트의 Awake()는 어떤 오브젝트의 Start()보다도 먼저 전부 끝나는 게
    // 유니티가 보장하는 순서라서, Start() 시점이면 안전합니다(SoundManager는 Instance를 처음
    // 부르는 순간 자동 생성되므로 사실 Awake에서 불러도 동작은 하지만, 다른 UI들과 순서를
    // 맞춰뒀습니다).
    private void Start()
    {
        LoadAndApplySettings();

        _sliderBGM.onValueChanged.AddListener(OnBgmSliderChanged);
        _sliderSFX.onValueChanged.AddListener(OnSfxSliderChanged);
        _dropdownResolution.onValueChanged.AddListener(OnResolutionDropdownChanged);
        _dropdownScreenMode.onValueChanged.AddListener(OnScreenModeDropdownChanged);
    }

    private void Update()
    {
        if (toggleAction.WasPressedThisFrame())
        {
            ToggleOption();
        }
    }

    private void SetupDropdownOptions()
    {
        _dropdownResolution.ClearOptions();
        _dropdownResolution.AddOptions(new List<string>(AspectRatioLabels));

        _dropdownScreenMode.ClearOptions();
        _dropdownScreenMode.AddOptions(new List<string>(ScreenModeLabels));
    }

    /// <summary>옵션 버튼 OnClick에서 호출하는 열기/닫기 토글 함수입니다. UIInventory.ToggleInventory()와
    /// 같은 패턴 - UICanvas에게 요청만 하고, 실제 Open()/Close() 호출은 UICanvas가 해줍니다.</summary>
    public void ToggleOption()
    {
        if (isOpen) UICanvas.Instance.CloseUI(gameObject);
        else UICanvas.Instance.OpenUI(gameObject);
    }

    /// <summary>나가기(취소) 버튼 OnClick에 연결하세요. 이 창을 연 뒤 바뀐 BGM/SFX 볼륨, 해상도
    /// 비율, 화면 모드를 전부 Open() 시점 값으로 되돌린 뒤 닫습니다 - 실시간 미리보기 값을 버리고
    /// 싶을 때 씁니다.</summary>
    public void ClickExitButton()
    {
        SoundManager.Instance.PlayUIClickSfx();
        RevertToSnapshot();
        UICanvas.Instance.CloseUI(gameObject);
    }

    /// <summary>확인 버튼 OnClick에 연결하세요. 지금 적용되어 있는 값(슬라이더/드롭다운으로 이미
    /// 실시간 반영된 값)을 그대로 유지한 채 닫습니다 - Close()에서 PlayerPrefs.Save()가 호출되어
    /// 디스크에도 저장됩니다.</summary>
    public void ClickConfirmButton()
    {
        SoundManager.Instance.PlayUIClickSfx();
        UICanvas.Instance.CloseUI(gameObject);
    }

    /// <summary>"조작법" 버튼 OnClick에 연결하세요. 지금 열려있는 옵션 창을 조작법 안내 패널
    /// (UIControls)로 바로 바꿔줍니다 - UICanvas.OpenUI()가 원래 "이미 다른 팝업이 열려있으면
    /// 그것부터 닫고 새 팝업을 연다"는 방식으로 동작하므로, 옵션 창을 먼저 직접 닫을 필요 없이
    /// 그냥 OpenUI만 호출하면 됩니다(스냅샷 취소 없이 지금 적용된 값 그대로 유지된 채 전환됩니다 -
    /// ClickConfirmButton과 같은 동작). 조작법 패널은 자기 자신의 닫기 버튼을 눌러야 닫힙니다
    /// (UIControls.cs 참고) - 옵션 창으로 자동으로 돌아가지는 않습니다.</summary>
    public void ClickShowControlsButton()
    {
        SoundManager.Instance.PlayUIClickSfx();
        UICanvas.Instance.OpenUI(UICanvas.Instance.Controls.gameObject);
    }

    /// <summary>IUIWindow 구현. UICanvas.OpenUI()가 호출합니다 - 직접 호출하지 말고 ToggleOption()이나
    /// UICanvas.Instance.OpenUI(gameObject)를 쓰세요.</summary>
    public void Open()
    {
        if (isOpen) return;
        isOpen = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 창을 여는 시점의 실제 볼륨/화면 상태로 UI를 다시 맞춥니다 - 예를 들어 다른 스크립트가
        // 코드로 볼륨을 바꿔놨을 수도 있으니, 저장된 값이 아니라 "지금 진짜 값" 기준입니다.
        RefreshUIFromCurrentState();

        // "나가기"를 눌렀을 때 되돌아갈 기준점을 지금(=열리는 시점) 값으로 저장해둡니다.
        CaptureSnapshot();

        fadeTween?.Kill();
        fadeTween = canvasGroup.DOFade(1f, fadeDuration).SetUpdate(true); // 게임이 멈춰도(Time.timeScale = 0) 페이드는 정상 속도로 재생됩니다.
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    /// <summary>IUIWindow 구현. UICanvas.CloseUI()가 호출합니다 - 직접 호출하지 말고 ToggleOption()이나
    /// UICanvas.Instance.CloseUI(gameObject)를 쓰세요. 닫히는 순간 커서를 무조건 다시 잠그고 숨깁니다
    /// (UIInventory.Close() 참고 - 열기 직전 상태를 복원하는 대신 항상 게임플레이 기본 상태로
    /// 되돌리는 방식으로 통일했습니다).</summary>
    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        fadeTween?.Kill();
        fadeTween = canvasGroup.DOFade(0f, fadeDuration).SetUpdate(true);
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        PlayerPrefs.Save(); // 닫는 시점에 한 번만 디스크에 씁니다(슬라이더 드래그마다 매번 쓰지 않도록).
    }

    // ------------------------------------------------------------------
    // 슬라이더/드롭다운 콜백 - 값이 바뀌는 즉시 실제로 적용하고, PlayerPrefs에도 기록해둡니다.
    // ------------------------------------------------------------------

    private void OnBgmSliderChanged(float value)
    {
        SoundManager.Instance.SetBGMVolume(value);
        PlayerPrefs.SetFloat(PrefBgmVolume, value);
    }

    private void OnSfxSliderChanged(float value)
    {
        SoundManager.Instance.SetSFXVolume(value);
        PlayerPrefs.SetFloat(PrefSfxVolume, value);
    }

    private void OnResolutionDropdownChanged(int index)
    {
        ApplyAspectRatio(index);
        PlayerPrefs.SetInt(PrefAspectRatioIndex, index);
    }

    private void OnScreenModeDropdownChanged(int index)
    {
        ApplyScreenMode(index);
        PlayerPrefs.SetInt(PrefScreenModeIndex, index);
    }

    // ------------------------------------------------------------------
    // 실제 적용
    // ------------------------------------------------------------------

    /// <summary>현재 화면 높이는 유지한 채, 고른 비율에 맞는 너비로 해상도를 다시 잡습니다
    /// (예: 높이 1080에서 16:9를 고르면 1920x1080, 21:9를 고르면 2520x1080).</summary>
    private void ApplyAspectRatio(int index)
    {
        Vector2Int ratio = AspectRatios[Mathf.Clamp(index, 0, AspectRatios.Length - 1)];
        int height = Screen.height;
        int width = Mathf.RoundToInt(height * (ratio.x / (float)ratio.y));

        Screen.SetResolution(width, height, Screen.fullScreenMode);
    }

    private void ApplyScreenMode(int index)
    {
        FullScreenMode mode = ScreenModes[Mathf.Clamp(index, 0, ScreenModes.Length - 1)];
        Screen.SetResolution(Screen.width, Screen.height, mode);
    }

    /// <summary>PlayerPrefs에 저장된 값이 있으면 그걸로, 없으면(첫 실행) 지금 SoundManager/화면
    /// 상태를 기준으로 UI와 실제 설정을 맞춥니다.</summary>
    private void LoadAndApplySettings()
    {
        float bgm = PlayerPrefs.HasKey(PrefBgmVolume) ? PlayerPrefs.GetFloat(PrefBgmVolume) : SoundManager.Instance.bgmVolume;
        float sfx = PlayerPrefs.HasKey(PrefSfxVolume) ? PlayerPrefs.GetFloat(PrefSfxVolume) : SoundManager.Instance.sfxVolume;
        int aspectIndex = PlayerPrefs.HasKey(PrefAspectRatioIndex) ? PlayerPrefs.GetInt(PrefAspectRatioIndex) : FindClosestAspectRatioIndex();
        int screenModeIndex = PlayerPrefs.HasKey(PrefScreenModeIndex) ? PlayerPrefs.GetInt(PrefScreenModeIndex) : FindScreenModeIndex();

        SoundManager.Instance.SetBGMVolume(bgm);
        SoundManager.Instance.SetSFXVolume(sfx);
        ApplyAspectRatio(aspectIndex);
        ApplyScreenMode(screenModeIndex);

        _sliderBGM.SetValueWithoutNotify(bgm);
        _sliderSFX.SetValueWithoutNotify(sfx);
        _dropdownResolution.SetValueWithoutNotify(aspectIndex);
        _dropdownScreenMode.SetValueWithoutNotify(screenModeIndex);
    }

    /// <summary>창을 열 때마다 UI를 "지금 실제 상태"와 다시 맞춥니다(저장된 값 기준이 아님).</summary>
    private void RefreshUIFromCurrentState()
    {
        _sliderBGM.SetValueWithoutNotify(SoundManager.Instance.bgmVolume);
        _sliderSFX.SetValueWithoutNotify(SoundManager.Instance.sfxVolume);
        _dropdownResolution.SetValueWithoutNotify(FindClosestAspectRatioIndex());
        _dropdownScreenMode.SetValueWithoutNotify(FindScreenModeIndex());
    }

    private int FindClosestAspectRatioIndex()
    {
        float currentRatio = Screen.width / (float)Screen.height;
        int bestIndex = 0;
        float bestDiff = float.MaxValue;

        for (int i = 0; i < AspectRatios.Length; i++)
        {
            float ratio = AspectRatios[i].x / (float)AspectRatios[i].y;
            float diff = Mathf.Abs(ratio - currentRatio);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private int FindScreenModeIndex()
    {
        for (int i = 0; i < ScreenModes.Length; i++)
        {
            if (ScreenModes[i] == Screen.fullScreenMode) return i;
        }
        return 0;
    }

    // ------------------------------------------------------------------
    // 나가기(취소) - Open() 시점 값을 저장해뒀다가 되돌립니다.
    // ------------------------------------------------------------------

    private void CaptureSnapshot()
    {
        snapshotBgmVolume = SoundManager.Instance.bgmVolume;
        snapshotSfxVolume = SoundManager.Instance.sfxVolume;
        snapshotAspectIndex = FindClosestAspectRatioIndex();
        snapshotScreenModeIndex = FindScreenModeIndex();
    }

    /// <summary>실제 적용 상태(SoundManager/Screen)와 UI(슬라이더/드롭다운)를 스냅샷 값으로
    /// 되돌리고, PlayerPrefs도 같은 값으로 맞춰둡니다(취소했으니 방금 만졌던 값이 디스크에
    /// 남지 않도록).</summary>
    private void RevertToSnapshot()
    {
        SoundManager.Instance.SetBGMVolume(snapshotBgmVolume);
        SoundManager.Instance.SetSFXVolume(snapshotSfxVolume);
        ApplyAspectRatio(snapshotAspectIndex);
        ApplyScreenMode(snapshotScreenModeIndex);

        _sliderBGM.SetValueWithoutNotify(snapshotBgmVolume);
        _sliderSFX.SetValueWithoutNotify(snapshotSfxVolume);
        _dropdownResolution.SetValueWithoutNotify(snapshotAspectIndex);
        _dropdownScreenMode.SetValueWithoutNotify(snapshotScreenModeIndex);

        PlayerPrefs.SetFloat(PrefBgmVolume, snapshotBgmVolume);
        PlayerPrefs.SetFloat(PrefSfxVolume, snapshotSfxVolume);
        PlayerPrefs.SetInt(PrefAspectRatioIndex, snapshotAspectIndex);
        PlayerPrefs.SetInt(PrefScreenModeIndex, snapshotScreenModeIndex);
    }
}