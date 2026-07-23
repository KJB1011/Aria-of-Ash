// ============================================================================
// MonsterHealthBar.cs
// ----------------------------------------------------------------------------
// 몬스터 머리 위에 체력바(UIMonsterHealthBar 프리팹)를 띄우는 컴포넌트입니다. MonsterFSM을 상속한
// 몬스터(SlimeFSM, WoodGolemFSM 등)에 이 컴포넌트를 추가하면 됩니다.
//
// [MiddleSlimeBoss는 일단 제외]
//   MiddleSlimeBoss는 지금 이 컴포넌트를 붙이지 마세요 - 보스는 나중에 화면 고정형 보스 체력바
//   등 별도 UI로 다룰 가능성이 높아서 우선 제외했습니다. MonsterStats만 있으면 동작하는 구조라
//   기술적으로는 붙여도 컴파일/실행에는 문제없지만, 기획상 지금은 제외해주세요.
//
// [동작 방식 - 몬스터에 직접 자식으로 넣지 않는 이유]
//   체력바를 몬스터 Transform의 자식으로 두면 몬스터가 회전/스케일될 때 같이 회전/스케일되어
//   찌그러지거나 기울어질 수 있습니다(특히 몬스터 크기가 1이 아니거나, 넘어지는 연출 등으로 X/Z축
//   회전이 생기면). 그래서 체력바 프리팹은 Awake()에서 씬 최상위에 별도로 Instantiate해두고,
//   매 프레임(LateUpdate) 위치만 "몬스터 위치 + worldOffset"으로 직접 옮기고, 회전은 카메라를
//   정면으로 바라보도록(빌보드) 강제로 맞춰줍니다 - 부모-자식 관계에 얽매이지 않아 항상 똑바로,
//   같은 크기로 보입니다.
//
// [체력 갱신]
//   MonsterStats에는 별도의 "체력 변경" 이벤트가 없어서, PlayerController가 HP바를 매 프레임
//   갱신하는 것과 같은 방식으로 이 스크립트도 매 프레임 CurrentHP/MaxHP를 읽어와 반영합니다.
//
// [사망 시 처리]
//   MonsterFSM은 체력이 0이 되어도 Die 애니메이션 재생을 위해 dieDelay(기본 2초)초 뒤에야
//   실제로 Destroy(gameObject)합니다. 체력바가 그동안 0으로 채워진 채 계속 떠 있으면 어색하므로,
//   CurrentHP가 0이 되는 즉시 체력바를 숨깁니다(SetActive(false)). 몬스터 오브젝트 자체가 결국
//   파괴되면 OnDestroy()에서 체력바 인스턴스도 함께 파괴해 씬에 남지 않게 합니다.
//
// [씬 준비]
//   1) 몬스터 프리팹(SlimeFSM/WoodGolemFSM 등이 붙은 오브젝트)에 이 컴포넌트를 추가하세요.
//   2) UIMonsterHealthBar.cs가 붙은 프리팹을 Bar Prefab 필드에 연결하세요.
//   3) World Offset으로 몬스터 머리 위 높이를 맞추세요(몬스터마다 키가 다르면 이 값도 다르게
//      맞춰주세요 - 기본값 2는 대략적인 예시일 뿐입니다).
//   4) Target Camera는 비워두면 Camera.main을 자동으로 씁니다. 대화 전용 카메라 등 여러 카메라를
//      쓰는 프로젝트라도, 화면에 실제로 렌더링되는 카메라(Main Camera 태그)를 기준으로 빌보드하면
//      충분합니다.
//   5) 몬스터 이름은 MonsterStats의 Display Name 필드에 입력하세요(예: "슬라임") - 이 컴포넌트가
//      Awake() 시점에 그 값을 그대로 체력바에 반영합니다.
// ============================================================================

using UnityEngine;

[RequireComponent(typeof(MonsterStats))]
public class MonsterHealthBar : MonoBehaviour
{
    [Header("프리팹")]
    [Tooltip("UIMonsterHealthBar.cs가 붙은 World Space Canvas 프리팹입니다.")]
    public UIMonsterHealthBar barPrefab;

    [Header("위치 / 카메라")]
    [Tooltip("몬스터 위치 기준으로 체력바를 띄울 상대 오프셋입니다. 보통 Y값만 몬스터 키 높이만큼 줍니다.")]
    public Vector3 worldOffset = new Vector3(0f, 2f, 0f);
    [Tooltip("체력바가 항상 정면으로 바라볼 카메라입니다. 비워두면 Awake()에서 Camera.main을 자동으로 씁니다.")]
    public Camera targetCamera;

    [Header("표시 옵션")]
    [Tooltip("켜두면 체력이 가득 차 있는 동안(아직 한 번도 맞지 않았을 때)에는 체력바를 숨겼다가, 처음 " +
              "맞는 순간부터 나타납니다. 꺼두면 살아있는 동안 항상 보입니다.")]
    public bool hideWhenFullHealth = false;

    private MonsterStats stats;
    private UIMonsterHealthBar barInstance;
    private Transform barTransform;

    private void Awake()
    {
        stats = GetComponent<MonsterStats>();

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (barPrefab == null)
        {
            Debug.LogWarning($"[MonsterHealthBar] '{name}'에 Bar Prefab이 연결되어 있지 않아 체력바를 표시할 수 없습니다.", this);
            return;
        }

        barInstance = Instantiate(barPrefab);
        barTransform = barInstance.transform;

        // 이름은 대화 중 바뀌지 않으므로(체력과 달리) 여기서 한 번만 반영합니다.
        barInstance.SetName(stats.displayName);
    }

    private void LateUpdate()
    {
        if (barInstance == null) return;

        // 이미 죽었으면(체력 0) 체력바를 숨기고 더 이상 갱신하지 않습니다 - Die 애니메이션이 재생되는
        // dieDelay 동안 체력바만 0으로 채워진 채 떠 있는 어색한 모습을 막습니다.
        if (stats.CurrentHP <= 0f)
        {
            if (barInstance.gameObject.activeSelf) barInstance.gameObject.SetActive(false);
            return;
        }

        float rate = stats.MaxHP > 0f ? stats.CurrentHP / stats.MaxHP : 0f;
        bool shouldShow = !hideWhenFullHealth || rate < 0.999f;

        if (barInstance.gameObject.activeSelf != shouldShow)
        {
            barInstance.gameObject.SetActive(shouldShow);
        }

        if (!shouldShow) return;

        barTransform.position = transform.position + worldOffset;

        if (targetCamera != null)
        {
            // 카메라와 "같은 방향"을 보도록(빌보드) 맞춥니다 - 카메라 쪽으로 마주보게 하려면
            // 반대 방향이 아니라 카메라의 회전값을 그대로 따라가는 쪽이 자연스럽습니다
            // (Canvas 정면이 항상 카메라 렌즈를 향하게 됩니다).
            barTransform.rotation = targetCamera.transform.rotation;
        }

        barInstance.SetHealthRate(rate);
    }

    private void OnDestroy()
    {
        if (barInstance != null)
        {
            Destroy(barInstance.gameObject);
        }
    }
}