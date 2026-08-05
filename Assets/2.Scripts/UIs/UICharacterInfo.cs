// ============================================================================
// UICharacterInfo.cs
// ----------------------------------------------------------------------------
// 캐릭터 정보 창입니다. UIIngame의 캐릭터 정보 버튼(ClickCharInfoButton)으로 열고 닫습니다.
// 안에는 두 개의 하위 화면(CharInfo/SkillInfo)이 있고, 왼쪽 사이드 버튼으로 서로 전환합니다.
//
// [SkillInfo - 스킬 트리 (붕괴 스타레일의 '행적'과 비슷한 방식)]
//   패시브/기본공격/스킬/필살기 4개 + 그 강화 버전 4개, 총 8개의 노드를 씬에 미리 배치해뒀다고
//   가정합니다. 각 노드는 SkillTreeNode 컴포넌트를 붙인 버튼이고, SkillTreeData(ScriptableObject,
//   LootItemData와 같은 방식)를 하나씩 참조합니다. 노드를 클릭하면(버튼 OnClick → SkillTreeNode.
//   OnClickNode() → 부모 계층에서 UICharacterInfo를 찾아 SelectSkillNode(this) 호출) 오른쪽에
//   아이콘/이름/타입/설명과 해제(또는 이미 해제됐다면 "OK") 상태가 표시됩니다.
//   UNLOCK 버튼(ClickSkillUpgradeButton)은 한계돌파와 완전히 같은 방식으로 재료 2종 + 골드를
//   확인/소모한 뒤 SkillTreeNode.MarkUnlocked()를 호출해서 그 노드를 해제합니다 - 재료/골드
//   확인·소모 로직(HasEnoughMaterialsAndGold)은 한계돌파와 공유합니다.
//   [주의] 처음 올려주신 스켈레톤에는 재료2의 수량 텍스트(_txtSkillUpgradeRequirements2Count)가
//   빠져있었습니다(재료1은 있는데 재료2만 없는 비대칭 상태) - CharInfo의 한계돌파 재료 UI와 맞추려고
//   이번에 추가했으니, 씬에서 재료2용 텍스트 오브젝트를 하나 만들어 연결해주세요.
//   [주의 2] 실제 게임플레이 효과(예: 패시브 강화를 해제하면 진짜로 에너지 충전 속도가 빨라지는 것)는
//   아직 연결하지 않았습니다 - 지금은 "해제됐다/안 됐다" 상태와 UI 표시까지만 처리합니다. 각 스킬
//   노드가 해제됐을 때 실제로 무엇이 바뀌어야 하는지 정해주시면 이어서 연결해드릴 수 있습니다.
//   [주의 3] 기본 4개 노드(패시브/기본공격/스킬/필살기, SkillTreeNode.startUnlocked = true) 자체를
//   재료로 강화해서 수치를 올리는 기능은 포트폴리오 범위에서 제외했습니다 - SkillTreeNode.IsUnlocked가
//   시작부터 true라 RefreshSkillUpgradeUI()가 항상 "이미 해제된 노드" 분기를 타므로, 이 4개는 클릭하면
//   정보만 보여주고 재료는 OK로 표시되며 UNLOCK/UPGRADE 버튼(_btnSkillUpgrade)은 항상 비활성화된
//   채로 남습니다. 그 옆의 "강화" 버전 4개 노드(원래부터 startUnlocked = false로 잠긴 채 시작)는
//   지금까지처럼 정상적으로 재료/골드를 소모해 해제할 수 있습니다 - 이번에 빠진 건 기본 노드 자신의
//   강화뿐입니다.
//   [주의 4] 스킬 강화 알림(느낌표) - 아직 해제 안 됐는데 재료/골드가 이미 충분해서 지금 바로 해제할 수
//   있는 강화 노드마다 SkillTreeNode.unlockableNotification(씬에 미리 붙여두신 느낌표 이미지)이 켜지고,
//   그런 노드가 하나라도 있으면 스킬 사이드탭 버튼의 _imgSideSkillNotification도 함께 켜집니다
//   (RefreshSkillNodeNotifications() 참고). 창을 열 때, 창이 열려있는 동안 인벤토리/골드가 바뀔 때,
//   UNLOCK 버튼을 눌러 해제할 때마다 자동으로 갱신되므로 따로 호출하실 필요는 없습니다 - 기본 4개
//   노드는 항상 이미 해제된 상태라 이 알림 대상에서 자연스럽게 제외됩니다.
//
// [전체 창 여닫기 - UICanvas 연동]
//   UIInventory와 완전히 같은 패턴입니다. IUIWindow를 구현해서 UICanvas가 "팝업 하나만 열리게,
//   열려있는 동안 게임 시간을 멈추게" 관리해줍니다. ToggleCharacterInfo()(버튼 OnClick 또는 U 키)는
//   UICanvas.Instance.OpenUI()/CloseUI()를 호출할 뿐이고, 실제 Open()/Close()는 UICanvas가 그 안에서
//   호출해줍니다 - 직접 이 컴포넌트의 Open()/Close()를 호출하지 마세요.
//   열리는 순간 마우스 커서 잠금을 자동으로 풀고, 닫히면(클릭이든 U 키든) 항상 다시 잠그고
//   숨깁니다(UIInventory.cs 상단 [마우스 커서] 참고).
//   U 키는 UIInventory의 I 키와 같은 방식(InputAction)으로 처리됩니다 - 따로 설정할 게 없습니다.
//
// [CharInfo - 레벨/최종 스탯/경험치]
//   레벨은 "Lv.{PlayerStats.level}" 형식으로 표시합니다. HP/MP/공격력/방어력/치명타 확률/치명타
//   피해량은 PlayerStats의 "최종(Total) 스탯"을 그대로 보여줍니다 - UIIngame의 HP/MP 바(현재값)와는
//   다른, 캐릭터 시트용 스탯 표시입니다. 경험치는 PlayerStats.currentExp / expToNextLevel을 텍스트로
//   그대로 표시하는 동시에, 그 비율(0~1)을 _sliderExp.value에도 채워서 게이지로 보여줍니다.
//
// [CharInfo - 한계돌파]
//   PlayerStats.Breakthrough()는 "지금 돌파 가능한 레벨인지"만 알고, 재료/골드를 확인하고
//   차감하는 건 이 UI의 책임입니다(PlayerStats.cs 헤더 주석 참고). breakthroughRequirements 배열에
//   PlayerStats.breakthroughLevels(기본 {20, 40})와 순서를 맞춰서 각 돌파 단계에 필요한 재료 2종 +
//   골드를 인스펙터에서 설정하세요 - [0]은 20레벨 돌파, [1]은 40레벨 돌파용입니다.
//   재료 수량 텍스트는 "x{필요수량}" 형식으로 표시하고, 보유량이 부족하면 빨간색, 충분하면
//   흰색으로 칠합니다(SetRequirementText 참고). 아직 그 레벨에 도달하지 않았어도(예: 15레벨인데
//   다음 차례가 20레벨 돌파) 미리 무엇이 필요한지 보여주지만, 실제로 돌파 버튼을 눌러도
//   PlayerStats.IsAwaitingBreakthrough가 true일 때만(정확히 그 레벨에 도달했을 때만) 실행됩니다.
//   PlayerInventory에 RemoveItem()/GetItemCount(), PlayerCurrency에 SpendGold()를 새로 추가해서 씁니다.
//
// [씬 준비]
//   1) 이 스크립트와 CanvasGroup을 창 전체 패널(여닫을 오브젝트)에 붙이세요(RequireComponent로 자동 추가).
//   2) UICanvas의 Char Info 필드에 이 오브젝트를 연결하세요.
//   3) UIIngame의 캐릭터 정보 버튼 OnClick에 UICanvas.Instance.CharacterInfo.ToggleCharacterInfo()가
//      연결되도록, UIIngame.ClickCharInfoButton()이 이미 그렇게 구현되어 있습니다.
//   4) 이 오브젝트는 항상 활성화(Active) 상태로 두세요 - CanvasGroup 알파로 보이기/숨기기를 처리합니다.
// ============================================================================

using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>한계돌파 한 단계(20레벨 또는 40레벨 등)에 필요한 재료 2종 + 골드입니다.
/// PlayerStats.breakthroughLevels와 인덱스가 1:1로 대응합니다.</summary>
[Serializable]
public class BreakthroughRequirement
{
    public LootItemData material1;
    public int material1Amount;
    public LootItemData material2;
    public int material2Amount;
    public int requiredGold;
}

[RequireComponent(typeof(CanvasGroup))]
public class UICharacterInfo : MonoBehaviour, IUIWindow
{
    [SerializeField] Image _imgSideCharBlack;
    [SerializeField] Image _imgSideSkillBlack;
    [Tooltip("스킬 사이드 탭 버튼에 표시할 느낌표 알림 이미지입니다. 스킬 트리 노드 중 하나라도 지금 바로 " +
              "해제할 수 있는 상태(재료/골드 충분)가 되면 켜집니다 - RefreshSkillNodeNotifications() 참고. " +
              "비워두면 사이드탭 알림 표시를 건너뜁니다(각 노드 자신의 느낌표는 SkillTreeNode." +
              "unlockableNotification이 따로 처리하므로 이 필드가 없어도 노드별 알림은 정상 동작합니다).")]
    [SerializeField] GameObject _imgSideSkillNotification;

    [SerializeField] GameObject _uicharInfo;
    [SerializeField] GameObject _uiSkillInfo;

    [Header("CharInfo")]
    [Tooltip("플레이어 닉네임 표시용입니다. LobbyScene의 아이디 입력창(UILobby.cs)에 적은 값이 " +
              "GameManager.PlayerId로 넘어와서, Start()에서 자동으로 SetPlayerName()에 채워집니다 - " +
              "따로 연결하거나 호출할 필요 없습니다. 로비를 거치지 않고 IngameScene을 바로 테스트하는 " +
              "등 GameManager.Instance.PlayerId가 비어있는 경우에는 빈 문자열이 표시됩니다.")]
    [SerializeField] TextMeshProUGUI _txtPlayerName;
    [SerializeField] TextMeshProUGUI _txtCharLevel;
    [SerializeField] TextMeshProUGUI _txtCharHP;
    [SerializeField] TextMeshProUGUI _txtCharMP;
    [SerializeField] TextMeshProUGUI _txtCharATK;
    [SerializeField] TextMeshProUGUI _txtCharDEF;
    [SerializeField] TextMeshProUGUI _txtCharCritRate;
    [SerializeField] TextMeshProUGUI _txtCharCritDamage;
    [SerializeField] TextMeshProUGUI _txtCharCurrentExp;
    [SerializeField] TextMeshProUGUI _txtCharMaxExp;
    [Tooltip("currentExp / expToNextLevel 비율(0~1)을 채워주는 슬라이더입니다. Interactable은 꺼두세요 " +
              "(표시 전용이라 사용자가 직접 드래그하면 안 됩니다).")]
    [SerializeField] Slider _sliderExp;

    [SerializeField] Image _imgBreakThroughRequirements1;
    [SerializeField] TextMeshProUGUI _txtBreakThroughRequirements1Count;
    [SerializeField] Image _imgBreakThroughRequirements2;
    [SerializeField] TextMeshProUGUI _txtBreakThroughRequirements2Count;
    [SerializeField] TextMeshProUGUI _txtBreakThroughRequiredGold;

    [Tooltip("한계돌파 단계별 필요 재료/골드입니다. PlayerStats.breakthroughLevels(기본 {20, 40})와 순서가 " +
              "1:1로 대응합니다 - [0]은 20레벨 돌파, [1]은 40레벨 돌파에 필요한 조건입니다. 배열 길이를 " +
              "breakthroughLevels 길이와 맞춰주세요(안 맞으면 넘치는 단계는 경고 로그만 남기고 표시를 건너뜁니다).")]
    [SerializeField] private BreakthroughRequirement[] breakthroughRequirements;

    [Header("SkillInfo")]
    [SerializeField] Image _imgSelectedSkill;
    [SerializeField] TextMeshProUGUI _txtSkillName;
    [SerializeField] TextMeshProUGUI _txtSkillType;
    [SerializeField] TextMeshProUGUI _txtSkillInfo;

    [SerializeField] Image _imgSkillUpgradeRequirements1;
    [SerializeField] TextMeshProUGUI _txtSkillUpgradeRequirements1Count;
    [SerializeField] Image _imgSkillUpgradeRequirements2;
    [Tooltip("재료2의 수량 텍스트입니다 - 처음 스켈레톤에는 없던 필드라 새로 추가했습니다. 씬에서 재료2용 " +
              "텍스트 오브젝트를 만들어 연결해주세요(재료1과 대칭되는 위치에).")]
    [SerializeField] TextMeshProUGUI _txtSkillUpgradeRequirements2Count;
    [SerializeField] TextMeshProUGUI _txtSkillUpgradeRequiredGold;
    [Tooltip("UNLOCK/UPGRADE 버튼입니다. 선택된 노드가 이미 해제된 상태(selectedSkillNode.IsUnlocked)면 " +
              "자동으로 비활성화됩니다 - 패시브/기본공격/스킬/필살기 기본 4개 노드는 처음부터 해제된 " +
              "상태로 시작하므로(SkillTreeNode.startUnlocked) 항상 이 버튼이 비활성화된 채로 정보만 " +
              "보여주게 됩니다(기본 스킬 자체를 강화하는 기능은 포트폴리오 범위에서 제외했습니다 - " +
              "RefreshSkillUpgradeUI() 참고). 비워두면 활성화 상태 제어를 건너뜁니다.")]
    [SerializeField] Button _btnSkillUpgrade;

    [Tooltip("스크롤 뷰에 배치해두신 8개의 스킬 트리 버튼(SkillTreeNode)입니다. 선택 사항이지만, 연결해두면 " +
              "SkillInfo 탭을 처음 열 때 0번 노드를 자동으로 선택해서 오른쪽 정보 패널이 비어있지 않게 해줍니다.")]
    [SerializeField] private SkillTreeNode[] skillNodes;

    private SkillTreeNode selectedSkillNode;

    [Header("표시/숨김")]
    public float fadeDuration = 0.15f;

    private CanvasGroup canvasGroup;
    private PlayerStats playerStats;
    private bool isOpen;
    private bool isShowingCharInfo;
    private bool subscribedToInventory;
    private bool subscribedToCurrency;
    private Tween fadeTween;

    // U 키로 여닫기 위한 입력 액션입니다(UIInventory가 I 키를 처리하는 것과 같은 패턴).
    private InputAction toggleAction;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        SetActiveTab(true); // 씬 저장 상태와 무관하게 항상 CharInfo 탭으로 시작합니다.

        toggleAction = new InputAction("ToggleCharacterInfo", InputActionType.Button, "<Keyboard>/u");
    }

    private void OnEnable()
    {
        toggleAction.Enable();
    }

    private void OnDisable()
    {
        toggleAction.Disable();

        if (subscribedToInventory)
        {
            PlayerInventory.Instance.OnInventoryChanged -= HandleInventoryChanged;
            subscribedToInventory = false;
        }

        if (subscribedToCurrency)
        {
            PlayerCurrency.Instance.OnGoldChanged -= HandleGoldChanged;
            subscribedToCurrency = false;
        }
    }

    // PlayerStats/PlayerInventory/PlayerCurrency의 Instance 참조 및 이벤트 구독은 Awake()가 아니라
    // Start()에서 합니다 - 씬 로드 시점에 존재하는 모든 오브젝트의 Awake()는 어떤 오브젝트의 Start()보다도
    // 먼저 전부 끝나는 게 유니티가 보장하는 순서라서, Start() 시점이면 각 Instance가 이미 확실히
    // 설정되어 있습니다(UIIngame/UIInventory와 같은 패턴).
    private void Start()
    {
        playerStats = PlayerStats.Instance;

        PlayerInventory.Instance.OnInventoryChanged += HandleInventoryChanged;
        subscribedToInventory = true;

        PlayerCurrency.Instance.OnGoldChanged += HandleGoldChanged;
        subscribedToCurrency = true;

        // LobbyScene의 아이디 입력창(UILobby.cs)에서 넘어온 값을 표시합니다. GameManager는
        // DontDestroyOnLoad라 씬이 바뀌어도 값이 그대로 유지되어 있습니다. 로비를 거치지 않고
        // IngameScene을 바로 테스트하는 등 GameManager.Instance가 없거나 PlayerId가 비어있어도
        // PlayerId 자체가 빈 문자열을 기본값으로 갖고 있으므로(GameManager.cs 참고) 안전합니다.
        SetPlayerName(GameManager.Instance != null ? GameManager.Instance.PlayerId : "");
    }

    private void Update()
    {
        if (toggleAction.WasPressedThisFrame())
        {
            ToggleCharacterInfo();
        }
    }

    // ------------------------------------------------------------------
    // 열기/닫기 - UICanvas 연동
    // ------------------------------------------------------------------

    /// <summary>캐릭터 정보 버튼 OnClick에서 호출하는 열기/닫기 토글 함수입니다. UICanvas에게 요청만 하고,
    /// 실제 Open()/Close() 호출은 UICanvas가 해줍니다 - 그래야 팝업이 한 번에 하나만 열리고, 여는 동안
    /// 게임 시간이 멈추는 게 같이 관리됩니다.</summary>
    public void ToggleCharacterInfo()
    {
        if (isOpen) UICanvas.Instance.CloseUI(gameObject);
        else UICanvas.Instance.OpenUI(gameObject);
    }

    /// <summary>IUIWindow 구현. UICanvas.OpenUI()가 호출합니다 - 직접 호출하지 말고 ToggleCharacterInfo()나
    /// UICanvas.Instance.OpenUI(gameObject)를 쓰세요.</summary>
    public void Open()
    {
        if (isOpen) return;
        isOpen = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SetActiveTab(true); // 열 때마다 항상 CharInfo 탭부터 보여줍니다.
        RefreshCharInfo();
        RefreshSkillNodeNotifications(); // CharInfo 탭으로 열어도 스킬 사이드탭 알림은 미리 최신 상태로 맞춰둡니다.

        fadeTween?.Kill();
        fadeTween = canvasGroup.DOFade(1f, fadeDuration).SetUpdate(true); // 게임이 멈춰도(Time.timeScale = 0) 페이드는 정상 속도로 재생됩니다.
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    /// <summary>IUIWindow 구현. UICanvas.CloseUI()가 호출합니다 - 직접 호출하지 말고 ToggleCharacterInfo()나
    /// UICanvas.Instance.CloseUI(gameObject)를 쓰세요. 닫히는 순간 커서를 무조건 다시 잠그고 숨겨서
    /// (UIInventory.Close() 참고 - 열기 직전 상태를 "복원"하는 대신 항상 게임플레이 기본 상태로
    /// 되돌리는 방식으로 통일했습니다) 클릭으로 닫든 U 키로 닫든 항상 카메라 조작이 바로 돌아옵니다.</summary>
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
    }

    public void ClickExitButton()
    {
        SoundManager.Instance.PlayUIClickSfx();
        UICanvas.Instance.CloseUI(gameObject);
    }

    // ------------------------------------------------------------------
    // 사이드 탭 전환 (CharInfo ↔ SkillInfo)
    // ------------------------------------------------------------------

    public void ClickSideCharButton()
    {
        SoundManager.Instance.PlayUIClickSfx();
        if (isShowingCharInfo) return; // 이미 CharInfo 상태면 무시.
        SetActiveTab(true);
    }

    public void ClickSideSkillButton()
    {
        SoundManager.Instance.PlayUIClickSfx();
        if (!isShowingCharInfo) return; // 이미 SkillInfo 상태면 무시.
        SetActiveTab(false);
    }

    /// <summary>CharInfo/SkillInfo 패널과, 선택 안 된 쪽을 어둡게 표시하는 sideBlack 이미지들을 함께
    /// 갱신합니다. 선택된 쪽의 sideBlack은 꺼서(밝게) 지금 선택된 탭임을 보여주고, 선택 안 된 쪽의
    /// sideBlack은 켜서(어둡게) 비활성 상태임을 보여줍니다.</summary>
    private void SetActiveTab(bool showCharInfo)
    {
        isShowingCharInfo = showCharInfo;

        _uicharInfo.SetActive(showCharInfo);
        _uiSkillInfo.SetActive(!showCharInfo);

        _imgSideCharBlack.gameObject.SetActive(!showCharInfo);
        _imgSideSkillBlack.gameObject.SetActive(showCharInfo);

        if (!showCharInfo)
        {
            if (selectedSkillNode == null && skillNodes != null && skillNodes.Length > 0)
            {
                // SkillInfo 탭을 처음 열었는데 아직 아무 노드도 선택된 적이 없다면, 오른쪽 정보 패널이
                // 비어있지 않도록 0번 노드를 자동으로 선택해줍니다(skillNodes를 연결해둔 경우에만) -
                // SelectSkillNode() 내부에서 RefreshSkillInfo()까지 호출해주므로 따로 갱신할 필요가 없습니다.
                SelectSkillNode(skillNodes[0]);
            }
            else
            {
                // 이미 선택해둔 노드가 있는 상태로 SkillInfo 탭을 다시 열 때도(예: CharInfo로 갔다가
                // 되돌아오거나, 창을 닫았다가 재료를 모아서 다시 열 때) 한 번 갱신해줍니다 - 그 사이
                // 인벤토리/골드가 바뀌었을 수 있는데, 창이 닫혀있는 동안은 HandleInventoryChanged()/
                // HandleGoldChanged()가 isOpen 가드 때문에 갱신을 건너뛰므로 다시 열 때 최신 상태로
                // 맞춰주지 않으면 예전 재료 수량 표시가 그대로 남아있게 됩니다.
                RefreshSkillInfo();
            }
        }
    }

    // ------------------------------------------------------------------
    // CharInfo - 스탯/경험치 표시
    // ------------------------------------------------------------------

    /// <summary>플레이어 닉네임을 표시합니다. 아직 닉네임을 들고 있는 데이터가 없어서 RefreshCharInfo()가
    /// 자동으로 호출해주지는 않습니다 - 나중에 닉네임 시스템이 생기면 여기(또는 RefreshCharInfo() 안)에서
    /// 이 함수를 호출해주세요.</summary>
    public void SetPlayerName(string playerName)
    {
        if (_txtPlayerName != null) _txtPlayerName.text = playerName;
    }

    private void RefreshCharInfo()
    {
        if (playerStats == null) return;

        _txtCharLevel.text = $"Lv.{playerStats.level}";
        _txtCharHP.text = Mathf.RoundToInt(playerStats.MaxHP).ToString();
        _txtCharMP.text = Mathf.RoundToInt(playerStats.MaxMP).ToString();
        _txtCharATK.text = Mathf.RoundToInt(playerStats.TotalAttackPower).ToString();
        _txtCharDEF.text = Mathf.RoundToInt(playerStats.TotalDefense).ToString();
        _txtCharCritRate.text = $"{playerStats.TotalCritRate:F1}%";
        _txtCharCritDamage.text = $"{playerStats.TotalCritDamage:F1}%";
        _txtCharCurrentExp.text = playerStats.currentExp.ToString();
        _txtCharMaxExp.text = playerStats.expToNextLevel.ToString();

        if (_sliderExp != null)
        {
            _sliderExp.value = playerStats.expToNextLevel > 0
                ? Mathf.Clamp01((float)playerStats.currentExp / playerStats.expToNextLevel)
                : 0f;
        }

        RefreshBreakthroughUI();
    }

    // ------------------------------------------------------------------
    // CharInfo - 한계돌파
    // ------------------------------------------------------------------

    /// <summary>다음으로 돌파해야 할 한계돌파 단계의 인덱스를 찾습니다(breakthroughRequirements/
    /// PlayerStats.breakthroughLevels와 같은 인덱스). 아직 완료하지 않은 것 중 가장 낮은 단계를
    /// 돌려주고, 전부 완료했다면 -1을 돌려줍니다. 레벨이 아직 그 돌파 레벨에 도달하지 않았어도
    /// (예: 15레벨인데 다음 차례가 20레벨 돌파) 미리 보여주기 위한 용도라, 여기서는 레벨 도달 여부를
    /// 따지지 않습니다 - 실제로 지금 돌파를 실행할 수 있는지는 PlayerStats.IsAwaitingBreakthrough로
    /// 따로 확인합니다.</summary>
    private int GetPendingBreakthroughIndex()
    {
        if (playerStats == null) return -1;

        for (int i = 0; i < playerStats.breakthroughLevels.Length; i++)
        {
            if (!playerStats.HasCompletedBreakthrough(playerStats.breakthroughLevels[i])) return i;
        }
        return -1;
    }

    private void RefreshBreakthroughUI()
    {
        int index = GetPendingBreakthroughIndex();
        if (index < 0 || breakthroughRequirements == null || index >= breakthroughRequirements.Length)
        {
            // 더 이상 돌파할 게 없거나(전부 완료), breakthroughRequirements 배열 설정이 모자란 경우입니다.
            if (index >= 0 && (breakthroughRequirements == null || index >= breakthroughRequirements.Length))
            {
                Debug.LogWarning($"[UICharacterInfo] breakthroughRequirements 배열에 {index}번째 항목이 없습니다. " +
                                  "PlayerStats.breakthroughLevels 길이에 맞춰 인스펙터에서 채워주세요.", this);
            }

            SetRequirementText(_txtBreakThroughRequirements1Count, 0, 0);
            SetRequirementText(_txtBreakThroughRequirements2Count, 0, 0);
            _txtBreakThroughRequiredGold.text = "-";
            return;
        }

        BreakthroughRequirement req = breakthroughRequirements[index];

        if (_imgBreakThroughRequirements1 != null && req.material1 != null)
        {
            _imgBreakThroughRequirements1.sprite = req.material1.icon;
        }
        if (_imgBreakThroughRequirements2 != null && req.material2 != null)
        {
            _imgBreakThroughRequirements2.sprite = req.material2.icon;
        }

        int owned1 = req.material1 != null ? PlayerInventory.Instance.GetItemCount(req.material1) : 0;
        int owned2 = req.material2 != null ? PlayerInventory.Instance.GetItemCount(req.material2) : 0;
        SetRequirementText(_txtBreakThroughRequirements1Count, owned1, req.material1Amount);
        SetRequirementText(_txtBreakThroughRequirements2Count, owned2, req.material2Amount);

        _txtBreakThroughRequiredGold.text = req.requiredGold.ToString();
    }

    /// <summary>"x{필요수량}" 형식으로 표시하고, 보유 수량(owned)이 필요 수량(required)보다 부족하면
    /// 빨간색, 충분하면 흰색으로 칠합니다.</summary>
    private static void SetRequirementText(TextMeshProUGUI text, int owned, int required)
    {
        if (text == null) return;
        text.text = $"x{required}";
        text.color = owned >= required ? Color.white : Color.red;
    }

    public void ClickBreakThroughButton()
    {
        SoundManager.Instance.PlayUIClickSfx();
        if (playerStats == null) return;

        int index = GetPendingBreakthroughIndex();
        if (index < 0)
        {
            Debug.LogWarning("[UICharacterInfo] 지금은 한계돌파할 단계가 없습니다(이미 모든 한계돌파를 마쳤습니다).", this);
            return;
        }

        if (!playerStats.IsAwaitingBreakthrough)
        {
            // 아직 그 레벨(예: 20레벨)에 도달하지 않았습니다 - 재료/골드가 충분해도 레벨이 안 됐으면 돌파할 수 없습니다.
            Debug.LogWarning($"[UICharacterInfo] 아직 한계돌파 레벨({playerStats.breakthroughLevels[index]})에 도달하지 않았습니다.", this);
            return;
        }

        if (breakthroughRequirements == null || index >= breakthroughRequirements.Length)
        {
            Debug.LogWarning($"[UICharacterInfo] breakthroughRequirements 배열에 {index}번째 항목이 없습니다.", this);
            return;
        }

        BreakthroughRequirement req = breakthroughRequirements[index];
        if (!HasEnoughMaterialsAndGold(req.material1, req.material1Amount, req.material2, req.material2Amount, req.requiredGold))
        {
            // 재료/골드가 부족합니다 - 이미 빨간 텍스트로 부족함을 보여주고 있으므로 조용히 무시합니다.
            return;
        }

        if (req.material1 != null && req.material1Amount > 0)
        {
            PlayerInventory.Instance.RemoveItem(req.material1, req.material1Amount);
        }
        if (req.material2 != null && req.material2Amount > 0)
        {
            PlayerInventory.Instance.RemoveItem(req.material2, req.material2Amount);
        }
        if (req.requiredGold > 0)
        {
            PlayerCurrency.Instance.SpendGold(req.requiredGold);
        }

        playerStats.Breakthrough();

        RefreshCharInfo(); // 레벨/스탯/다음 돌파 단계 표시까지 한 번에 최신 상태로 갱신합니다.
    }

    /// <summary>재료 2종 + 골드가 충분한지 확인합니다. 한계돌파(BreakthroughRequirement)와 스킬 노드 해제
    /// (SkillTreeData)가 이 하나의 함수를 공유합니다 - 둘 다 "재료1/수량, 재료2/수량, 골드" 구조가 같기 때문입니다.
    /// material이 null이거나 필요 수량이 0 이하면 그 재료는 조건에서 제외됩니다(안 써도 되는 슬롯).</summary>
    private static bool HasEnoughMaterialsAndGold(LootItemData material1, int material1Amount, LootItemData material2, int material2Amount, int requiredGold)
    {
        bool material1Ok = material1 == null || material1Amount <= 0
            || PlayerInventory.Instance.GetItemCount(material1) >= material1Amount;
        bool material2Ok = material2 == null || material2Amount <= 0
            || PlayerInventory.Instance.GetItemCount(material2) >= material2Amount;
        bool goldOk = PlayerCurrency.Instance.gold >= requiredGold;

        return material1Ok && material2Ok && goldOk;
    }

    // ------------------------------------------------------------------
    // SkillInfo - 스킬 트리 노드 선택/표시
    // ------------------------------------------------------------------

    /// <summary>SkillTreeNode.OnClickNode()가 호출합니다. 이전에 선택돼있던 노드는 선택 해제하고,
    /// 새로 선택된 노드의 정보를 오른쪽 패널에 표시합니다.</summary>
    public void SelectSkillNode(SkillTreeNode node)
    {
        if (node == null || node.data == null) return;

        if (selectedSkillNode != null) selectedSkillNode.SetSelected(false);
        selectedSkillNode = node;
        selectedSkillNode.SetSelected(true);

        RefreshSkillInfo();
    }

    private void RefreshSkillInfo()
    {
        if (selectedSkillNode == null || selectedSkillNode.data == null) return;
        SkillTreeData data = selectedSkillNode.data;

        if (_imgSelectedSkill != null && data.icon != null) _imgSelectedSkill.sprite = data.icon;
        _txtSkillName.text = data.displayName;
        _txtSkillType.text = data.skillType;
        _txtSkillInfo.text = data.description;

        RefreshSkillUpgradeUI();
    }

    private void RefreshSkillUpgradeUI()
    {
        if (selectedSkillNode == null || selectedSkillNode.data == null) return;
        SkillTreeData data = selectedSkillNode.data;

        if (_imgSkillUpgradeRequirements1 != null && data.material1 != null)
        {
            _imgSkillUpgradeRequirements1.sprite = data.material1.icon;
        }
        if (_imgSkillUpgradeRequirements2 != null && data.material2 != null)
        {
            _imgSkillUpgradeRequirements2.sprite = data.material2.icon;
        }

        if (selectedSkillNode.IsUnlocked)
        {
            // 이미 해제된 노드는 재료 요구량 대신 "OK"를 보여주고 UNLOCK/UPGRADE 버튼을 비활성화합니다.
            // 패시브/기본공격/스킬/필살기 기본 4개 노드는 처음부터 IsUnlocked = true(startUnlocked)로
            // 시작하므로 이 분기를 항상 타서, 선택하면 정보만 보이고 버튼은 계속 비활성화 상태로
            // 남습니다 - 기본 스킬 자체를 강화하는 기능은 포트폴리오 범위에서 제외했습니다.
            SetCompletedText(_txtSkillUpgradeRequirements1Count);
            SetCompletedText(_txtSkillUpgradeRequirements2Count);
            if (_txtSkillUpgradeRequiredGold != null) _txtSkillUpgradeRequiredGold.text = "-";
            if (_btnSkillUpgrade != null) _btnSkillUpgrade.interactable = false;
            return;
        }

        int owned1 = data.material1 != null ? PlayerInventory.Instance.GetItemCount(data.material1) : 0;
        int owned2 = data.material2 != null ? PlayerInventory.Instance.GetItemCount(data.material2) : 0;
        SetRequirementText(_txtSkillUpgradeRequirements1Count, owned1, data.material1Amount);
        SetRequirementText(_txtSkillUpgradeRequirements2Count, owned2, data.material2Amount);

        if (_txtSkillUpgradeRequiredGold != null) _txtSkillUpgradeRequiredGold.text = data.requiredGold.ToString();
        if (_btnSkillUpgrade != null) _btnSkillUpgrade.interactable = true;
    }

    private static void SetCompletedText(TextMeshProUGUI text)
    {
        if (text == null) return;
        text.text = "OK";
        text.color = Color.white;
    }

    /// <summary>skillNodes 전체를 훑어서, 아직 해제되지 않았는데 재료/골드가 이미 충분해서 지금 바로
    /// 해제할 수 있는 노드마다 느낌표 알림(SkillTreeNode.unlockableNotification)을 켭니다. 그런 노드가
    /// 하나라도 있으면 스킬 사이드탭 버튼(_imgSideSkillNotification)에도 느낌표를 켭니다. 기본 4개 노드는
    /// 항상 IsUnlocked = true(startUnlocked)라 이 조건에 걸리지 않으므로 따로 제외 처리가 필요 없습니다.
    /// Open()과 HandleInventoryChanged()/HandleGoldChanged()(창이 열려있는 동안), ClickSkillUpgradeButton()
    /// (해제 직후, 재료/골드 소모가 0이라 이벤트가 안 터지는 경우까지 대비)에서 호출합니다.</summary>
    private void RefreshSkillNodeNotifications()
    {
        if (skillNodes == null) return;

        bool anyUnlockable = false;

        foreach (SkillTreeNode node in skillNodes)
        {
            if (node == null || node.data == null) continue;

            bool canUnlock = !node.IsUnlocked && HasEnoughMaterialsAndGold(
                node.data.material1, node.data.material1Amount,
                node.data.material2, node.data.material2Amount,
                node.data.requiredGold);

            node.SetUnlockableNotification(canUnlock);
            if (canUnlock) anyUnlockable = true;
        }

        if (_imgSideSkillNotification != null) _imgSideSkillNotification.SetActive(anyUnlockable);
    }

    /// <summary>UNLOCK 버튼 OnClick에 연결하세요. 현재 선택된 스킬 노드를 재료/골드를 확인/소모한 뒤
    /// 해제합니다(ClickBreakThroughButton과 완전히 같은 방식).</summary>
    public void ClickSkillUpgradeButton()
    {
        SoundManager.Instance.PlayUIClickSfx();
        if (selectedSkillNode == null || selectedSkillNode.data == null)
        {
            Debug.LogWarning("[UICharacterInfo] 선택된 스킬 노드가 없습니다. 먼저 트리에서 노드를 클릭하세요.", this);
            return;
        }

        if (selectedSkillNode.IsUnlocked)
        {
            Debug.LogWarning($"[UICharacterInfo] '{selectedSkillNode.data.displayName}'은 이미 해제되어 있습니다.", this);
            return;
        }

        SkillTreeData data = selectedSkillNode.data;
        if (!HasEnoughMaterialsAndGold(data.material1, data.material1Amount, data.material2, data.material2Amount, data.requiredGold))
        {
            // 재료/골드가 부족합니다 - 이미 빨간 텍스트로 부족함을 보여주고 있으므로 조용히 무시합니다.
            return;
        }

        if (data.material1 != null && data.material1Amount > 0)
        {
            PlayerInventory.Instance.RemoveItem(data.material1, data.material1Amount);
        }
        if (data.material2 != null && data.material2Amount > 0)
        {
            PlayerInventory.Instance.RemoveItem(data.material2, data.material2Amount);
        }
        if (data.requiredGold > 0)
        {
            PlayerCurrency.Instance.SpendGold(data.requiredGold);
        }

        selectedSkillNode.MarkUnlocked();
        ApplySkillUpgradeEffect(data.skillId);
        RefreshSkillUpgradeUI();
        RefreshSkillNodeNotifications(); // 재료/골드가 0이라 OnInventoryChanged/OnGoldChanged가 안 터지는 경우까지 대비해 직접 갱신합니다.
    }

    /// <summary>방금 해제된 노드(skillId)에 맞는 실제 게임플레이 효과를 PlayerStats에 켭니다. 강화 4종
    /// (기본공격강화/패시브강화/스킬강화/필살기강화)의 skillId는 SkillTreeData 애셋을 만들 때 아래 문자열과
    /// 정확히 똑같이 입력해주세요 - 다르면 이 해제가 그냥 조용히 아무 효과도 켜지 않습니다(트리 표시/재료
    /// 소모는 정상적으로 이뤄지고 UNLOCK 상태도 저장되지만, 실제 스탯 변화만 없는 상태가 됩니다).
    /// 패시브/기본공격/스킬/필살기 "기본" 4개 노드(강화가 아닌 원본)는 처음부터 해제되어 있어서
    /// 애초에 이 함수가 호출될 일이 없으므로 skillId를 안 정해줘도 됩니다.</summary>
    private void ApplySkillUpgradeEffect(string skillId)
    {
        if (playerStats == null) return;

        switch (skillId)
        {
            case "basic_attack_upgrade":
                playerStats.UnlockBasicAttackUpgrade();
                break;
            case "passive_upgrade":
                playerStats.UnlockPassiveUpgrade();
                break;
            case "skill_upgrade":
                playerStats.UnlockSkillUpgrade();
                break;
            case "ult_upgrade":
                playerStats.UnlockUltUpgrade();
                break;
            default:
                Debug.LogWarning($"[UICharacterInfo] skillId '{skillId}'에 대응하는 강화 효과가 없습니다 - " +
                                  "이 노드가 4개 강화 노드 중 하나라면 SkillTreeData의 Skill Id를 " +
                                  "\"basic_attack_upgrade\"/\"passive_upgrade\"/\"skill_upgrade\"/\"ult_upgrade\" " +
                                  "중 하나로 맞춰주세요.", this);
                break;
        }
    }

    // ------------------------------------------------------------------
    // 이벤트 구독 - 창이 열려있는 동안 재료/골드가 바뀌면 실시간으로 반영
    // ------------------------------------------------------------------

    private void HandleInventoryChanged()
    {
        if (!isOpen) return;
        RefreshBreakthroughUI();
        RefreshSkillUpgradeUI();
        RefreshSkillNodeNotifications();
    }

    private void HandleGoldChanged(int gold)
    {
        if (!isOpen) return;
        RefreshBreakthroughUI();
        RefreshSkillUpgradeUI();
        RefreshSkillNodeNotifications();
    }
}