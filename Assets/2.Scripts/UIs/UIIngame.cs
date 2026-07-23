// ============================================================================
// UIIngame.cs
// ----------------------------------------------------------------------------
// 인게임 HUD를 담당하는 스크립트입니다 (HP/MP 바, 미니맵, 스킬/필살기 쿨타임, 각종 버튼).
//
// [HP/MP 바]
//   PlayerController가 매 프레임 SetHPBar()/SetMPBar()를 호출해서 최신 비율(0~1)을 반영합니다.
//
// [미니맵]
//   Cam Minimap에 연결한 카메라가 매 프레임(LateUpdate) 플레이어의 XZ 위치를 따라다닙니다
//   (높이는 minimapHeight로 고정, 회전은 건드리지 않습니다 - 씬에서 미리 아래를 내려다보도록
//   맞춰두세요. 플레이어 방향에 따라 미니맵 자체가 돌아가는 기능은 아닙니다). Plus/Minus
//   버튼(또는 =/- 키)은 카메라의 Orthographic Size(보이는 범위)를 minimapZoomStep만큼 조절합니다 -
//   확대(=키/Plus 버튼)를 누르면 더 좁은 범위를 크게 보여주고(Orthographic Size 감소), 축소(-키/Minus
//   버튼)를 누르면 더 넓은 범위를 보여줍니다(Orthographic Size 증가). minimapMinZoom/minimapMaxZoom으로
//   확대/축소 한계를 정합니다. 화면에 표시되는 미니맵 UI 패널 자체의 크기는 건드리지 않습니다.
//   =/- 키는 U/I 키와 같은 방식(InputAction)으로 처리되며, 누를 때마다(눌려있는 동안 계속이 아니라)
//   한 단계씩 조절됩니다 - 버튼 클릭과 동일한 동작이라 씬에서 따로 설정할 게 없습니다.
//
// [스킬 쿨타임]
//   PlayerController가 매 프레임 SetSkillCooldown()을 호출해서 최신 상태를 반영합니다. rate01은
//   스킬을 쓴 직후 1이었다가 쿨타임이 다 되면 0이 되므로, _skillCooldownImage를 Image Type = Filled로
//   설정해두면 "게이지가 닳아 없어지는" 연출이 그대로 나옵니다(Fill Amount = rate01). 텍스트는
//   쿨타임 중엔 소수점 1자리로 남은 시간을, 다 되면 빈 문자열을 표시합니다.
//
// [필살기 에너지/쿨타임]
//   PlayerController가 매 프레임 SetUltCooldown()을 호출합니다. energyRate01은 시간이 아니라
//   필살기 에너지(PlayerStats.CurrentEnergy)를 기준으로 계산됩니다 - 에너지가 0이면 1(가득 찬 상태로
//   표시, 아직 사용 불가), 에너지가 가득 차면(100) 0(사용 가능)이 되므로, _ultCooldownImage도
//   Image Type = Filled로 설정해두면 "에너지가 찰수록 게이지가 닳아 없어지는" 연출이 됩니다.
//   remainingSeconds는 에너지와는 별개로, 필살기를 쓴 뒤의 재사용 대기시간(쿨타임, 20초)이 얼마나
//   남았는지를 나타내며 텍스트에 소수점 1자리로 표시됩니다(0이면 빈 문자열). 즉 필살기를 쓰려면
//   에너지가 가득 차 있어야 하고(_ultCooldownImage가 0), 동시에 쿨타임도 다 돼 있어야 합니다
//   (_ultCooldownText가 비어있음).
//
// [인벤토리 버튼]
//   ClickInventoryButton()은 UICanvas.Instance.Inventory.ToggleInventory()를 호출합니다 - UIInventory에는
//   따로 static Instance를 두지 않고, UICanvas가 모든 UI를 붙잡고 있다가 타입이 있는 프로퍼티(Inventory)로
//   꺼내줍니다. 실제 여닫기/시간 정지 관리는 UIInventory/UICanvas 쪽에서 이미 처리하고 있어서, 여기서는
//   그냥 요청만 넘겨줍니다.
//
// [캐릭터 정보 버튼]
//   ClickCharInfoButton()은 UICanvas.Instance.CharacterInfo.ToggleCharacterInfo()를 호출합니다 -
//   ClickInventoryButton()과 같은 패턴입니다(UICharacterInfo.cs 참고).
//
// [옵션 버튼]
//   ClickOptionButton()은 UICanvas.Instance.Option.ToggleOption()을 호출합니다 - ClickInventoryButton()/
//   ClickCharInfoButton()과 같은 패턴입니다(UIOption.cs 참고).
//
// [퀘스트 버튼]
//   ClickQuestButton()은 UICanvas.Instance.Quest.ToggleQuest()를 호출합니다 - 다른 버튼들과 같은
//   패턴입니다(UIQuest.cs 참고). L 키로도 같은 창을 열고 닫을 수 있는데, 그건 이 버튼과 별개로
//   UIQuest 자신이 직접 처리합니다(I키를 자기가 직접 처리하는 UIInventory와 같은 방식이라 여기서는
//   따로 InputAction을 만들 필요가 없습니다).
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UIIngame : MonoBehaviour
{
    [SerializeField] Slider _hpBar;
    [SerializeField] Slider _mpBar;

    [SerializeField] Camera _camMinimap;

    [SerializeField] Image _skillCooldownImage;
    [SerializeField] TextMeshProUGUI _skillCooldownText;
    [SerializeField] Image _ultCooldownImage;
    [SerializeField] TextMeshProUGUI _ultCooldownText;

    [Header("미니맵")]
    [Tooltip("미니맵 카메라가 플레이어 머리 위 이 높이(미터)에서 내려다봅니다.")]
    public float minimapHeight = 20f;
    [Tooltip("Plus/Minus 버튼을 누를 때마다 카메라 Orthographic Size가 이만큼 바뀝니다.")]
    public float minimapZoomStep = 5f;
    [Tooltip("Orthographic Size의 최소값(가장 확대된 상태 - 좁은 범위를 크게 보여줌).")]
    public float minimapMinZoom = 10f;
    [Tooltip("Orthographic Size의 최대값(가장 축소된 상태 - 넓은 범위를 보여줌).")]
    public float minimapMaxZoom = 60f;

    private Transform playerTransform;

    // =/- 키 입력을 처리하는 InputAction입니다. UIInventory의 I키, UICharacterInfo의 U키와 완전히 같은
    // 패턴입니다 - Awake()에서 만들고, OnEnable/OnDisable에서 Enable/Disable 하고, Update()에서
    // WasPressedThisFrame()으로 눌린 순간만 감지합니다(누르고 있는 동안 계속 반응하는 게 아니라
    // 한 번 누를 때마다 한 단계씩 줌 조절). '=' 키의 바인딩 경로는 <Keyboard>/equals, '-' 키는
    // <Keyboard>/minus 입니다.
    private InputAction minimapZoomInAction;
    private InputAction minimapZoomOutAction;

    private void Awake()
    {
        minimapZoomInAction = new InputAction("MinimapZoomIn", InputActionType.Button, "<Keyboard>/equals");
        minimapZoomOutAction = new InputAction("MinimapZoomOut", InputActionType.Button, "<Keyboard>/minus");
    }

    private void OnEnable()
    {
        minimapZoomInAction.Enable();
        minimapZoomOutAction.Enable();
    }

    private void OnDisable()
    {
        minimapZoomInAction.Disable();
        minimapZoomOutAction.Disable();
    }

    // PlayerStats.Instance를 참조하는 건 Awake/OnEnable이 아니라 Start()에서 합니다 - 씬 로드
    // 시점에 존재하는 모든 오브젝트의 Awake()는 어떤 오브젝트의 Start()보다도 먼저 전부 끝나는 게
    // 유니티가 보장하는 순서라서, Start() 시점이면 PlayerStats.Instance가 이미 확실히 설정되어
    // 있습니다.
    private void Start()
    {
        playerTransform = PlayerStats.Instance.transform;
    }

    private void Update()
    {
        if (minimapZoomInAction.WasPressedThisFrame()) ClickMinimapPlusButton();
        if (minimapZoomOutAction.WasPressedThisFrame()) ClickMinimapMinusButton();
    }

    private void LateUpdate()
    {
        Vector3 position = playerTransform.position;
        position.y += minimapHeight;
        _camMinimap.transform.position = position;
    }

    public void SetHPBar(float rate)
    {
        _hpBar.value = rate;
    }
    public void SetMPBar(float rate)
    {
        _mpBar.value = rate;
    }

    /// <summary>스킬 쿨타임 게이지/텍스트를 갱신합니다. rate01은 1(방금 사용) → 0(사용 가능)
    /// 순서로 줄어듭니다 - _skillCooldownImage를 Image Type = Filled로 설정해두면 Fill Amount로
    /// 그대로 쓸 수 있습니다. remainingSeconds가 0보다 크면 소수점 1자리로 표시하고, 0 이하가
    /// 되면(=사용 가능) 텍스트를 비웁니다.</summary>
    public void SetSkillCooldown(float rate01, float remainingSeconds)
    {
        _skillCooldownImage.fillAmount = rate01;
        _skillCooldownText.text = remainingSeconds > 0f ? remainingSeconds.ToString("F1") : string.Empty;
    }

    /// <summary>필살기 에너지/쿨타임 상태를 갱신합니다. energyRate01은 에너지가 충전될수록 1(에너지 0) →
    /// 0(에너지 가득 참, 사용 가능)으로 줄어듭니다 - _ultCooldownImage를 Image Type = Filled로 설정해두면
    /// Fill Amount로 그대로 쓸 수 있습니다. remainingSeconds는 에너지와는 별개로 필살기를 쓴 뒤의 재사용
    /// 대기시간(쿨타임)이며, 0보다 크면 소수점 1자리로 표시하고 0 이하가 되면(=쿨타임 다 됨) 텍스트를
    /// 비웁니다.</summary>
    public void SetUltCooldown(float energyRate01, float remainingSeconds)
    {
        _ultCooldownImage.fillAmount = energyRate01;
        _ultCooldownText.text = remainingSeconds > 0f ? remainingSeconds.ToString("F1") : string.Empty;
    }

    /// <summary>미니맵을 확대합니다(Orthographic Size 감소 = 더 좁은 범위를 크게 보여줌).</summary>
    public void ClickMinimapPlusButton()
    {
        _camMinimap.orthographicSize = Mathf.Max(minimapMinZoom, _camMinimap.orthographicSize - minimapZoomStep);
    }

    /// <summary>미니맵을 축소합니다(Orthographic Size 증가 = 더 넓은 범위를 보여줌).</summary>
    public void ClickMinimapMinusButton()
    {
        _camMinimap.orthographicSize = Mathf.Min(minimapMaxZoom, _camMinimap.orthographicSize + minimapZoomStep);
    }

    /// <summary>UICanvas.Instance.CharacterInfo로 꺼내서 여닫기 요청만 넘깁니다(ClickInventoryButton과
    /// 같은 패턴). 실제 열기/닫기(팝업 관리, 게임 시간 정지, 커서 잠금 해제 등)는 UICharacterInfo/UICanvas
    /// 쪽에서 처리합니다.</summary>
    public void ClickCharInfoButton()
    {
        UICanvas.Instance.CharacterInfo.ToggleCharacterInfo();
    }

    /// <summary>UIInventory에게 열기/닫기 요청만 넘깁니다. UIInventory에는 static Instance가 없으므로
    /// UICanvas.Instance.Inventory로 꺼내서 씁니다. 실제 처리(팝업 하나만 열리게 관리, 게임 시간 정지,
    /// 커서 잠금 해제 등)는 UIInventory/UICanvas 쪽에 이미 구현되어 있습니다.</summary>
    public void ClickInventoryButton()
    {
        UICanvas.Instance.Inventory.ToggleInventory();
    }

    /// <summary>UIOption에게 열기/닫기 요청만 넘깁니다(ClickInventoryButton()/ClickCharInfoButton()과
    /// 같은 패턴). 실제 처리(팝업 하나만 열리게 관리, 게임 시간 정지, 커서 잠금 해제 등)는
    /// UIOption/UICanvas 쪽에서 처리합니다.</summary>
    public void ClickOptionButton()
    {
        UICanvas.Instance.Option.ToggleOption();
    }

    /// <summary>UIQuest에게 열기/닫기 요청만 넘깁니다(ClickInventoryButton()/ClickCharInfoButton()/
    /// ClickOptionButton()과 같은 패턴). L 키로도 같은 창을 열 수 있지만, 그 처리는 UIQuest 자신이
    /// 직접 담당하므로 여기서는 신경 쓸 필요가 없습니다.</summary>
    public void ClickQuestButton()
    {
        UICanvas.Instance.Quest.ToggleQuest();
    }
}