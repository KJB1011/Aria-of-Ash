// ============================================================================
// NPCNameplate.cs
// ----------------------------------------------------------------------------
// NPC 머리 위에 이름표(UINPCNameplate 프리팹)를 띄우는 컴포넌트입니다. NPCTalker가 붙은 오브젝트에
// 이 컴포넌트를 추가하면 됩니다. MonsterHealthBar.cs와 완전히 같은 방식(월드에 별도 Instantiate +
// LateUpdate에서 위치/빌보드 갱신)입니다 - 체력바 대신 이름만 표시할 뿐입니다.
//
// [동작 방식 - NPC에 직접 자식으로 넣지 않는 이유]
//   MonsterHealthBar.cs와 같은 이유입니다. 이름표를 NPC Transform의 자식으로 두면 NPC가
//   회전/스케일될 때(대화 중 플레이어를 바라보며 회전하는 것도 포함) 같이 회전/스케일되어 찌그러지거나
//   기울어질 수 있습니다. 그래서 이름표 프리팹은 Awake()에서 씬 최상위에 별도로 Instantiate해두고,
//   매 프레임(LateUpdate) 위치만 "NPC 위치 + worldOffset"으로 직접 옮기고, 회전은 카메라를 정면으로
//   바라보도록(빌보드) 강제로 맞춥니다 - 부모-자식 관계에 얽매이지 않아 NPC가 어떻게 돌아가든 이름표는
//   항상 똑바로, 같은 크기로 보입니다.
//
// [이름 - NPCTalker.npcName을 그대로 사용]
//   상호작용 목록 UI에도 쓰이는 NPCTalker.npcName을 그대로 재사용합니다 - 따로 이름을 또 입력할
//   필요가 없습니다. 게임 도중 바뀌지 않으므로(체력과 달리) Awake() 시점에 한 번만 반영합니다.
//
// [컷씬 중에는 숨김]
//   CutsceneManager.IsAnyCutscenePlaying이 true인 동안(컷씬 재생 중)에는 이름표를 자동으로
//   비활성화합니다 - 눈 마주침 클로즈업처럼 화면에 크게 잡히는 연출에 이름표가 둥둥 떠 있으면
//   어색하기 때문입니다. 별도로 씬에 CutsceneManager를 연결할 필요 없이 이 static 프로퍼티만
//   확인하면 되고(UICanvas.IsUIOpen이 같은 프로퍼티를 확인하는 것과 같은 방식), 컷씬이 끝나면
//   자동으로 다시 나타납니다.
//
// [씬 준비]
//   1) NPCTalker가 붙은 오브젝트에 이 컴포넌트를 추가하세요(NPCTalker가 필수 컴포넌트입니다).
//   2) UINPCNameplate.cs가 붙은 프리팹을 Nameplate Prefab 필드에 연결하세요.
//   3) World Offset으로 NPC 머리 위 높이를 맞추세요(NPC마다 키가 다르면 이 값도 다르게 맞춰주세요 -
//      기본값 2는 대략적인 예시일 뿐입니다).
//   4) Target Camera는 비워두면 Camera.main을 자동으로 씁니다. 대화 전용 카메라 등 여러 카메라를
//      쓰는 프로젝트라도, 화면에 실제로 렌더링되는 카메라(Main Camera 태그)를 기준으로 빌보드하면
//      충분합니다.
// ============================================================================

using UnityEngine;

[RequireComponent(typeof(NPCTalker))]
public class NPCNameplate : MonoBehaviour
{
    [Header("프리팹")]
    [Tooltip("UINPCNameplate.cs가 붙은 World Space Canvas 프리팹입니다.")]
    public UINPCNameplate nameplatePrefab;

    [Header("위치 / 카메라")]
    [Tooltip("NPC 위치 기준으로 이름표를 띄울 상대 오프셋입니다. 보통 Y값만 NPC 키 높이만큼 줍니다.")]
    public Vector3 worldOffset = new Vector3(0f, 2f, 0f);
    [Tooltip("이름표가 항상 정면으로 바라볼 카메라입니다. 비워두면 Awake()에서 Camera.main을 자동으로 씁니다.")]
    public Camera targetCamera;

    private NPCTalker npcTalker;
    private UINPCNameplate nameplateInstance;
    private Transform nameplateTransform;

    private void Awake()
    {
        npcTalker = GetComponent<NPCTalker>();

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (nameplatePrefab == null)
        {
            Debug.LogWarning($"[NPCNameplate] '{name}'에 Nameplate Prefab이 연결되어 있지 않아 이름표를 표시할 수 없습니다.", this);
            return;
        }

        nameplateInstance = Instantiate(nameplatePrefab);
        nameplateTransform = nameplateInstance.transform;

        // 이름은 게임 도중 바뀌지 않으므로 여기서 한 번만 반영합니다.
        nameplateInstance.SetName(npcTalker.npcName);
    }

    private void LateUpdate()
    {
        if (nameplateInstance == null) return;

        // 컷씬 재생 중에는 숨깁니다(파일 상단 [컷씬 중에는 숨김] 참고) - 숨겨진 동안은 위치/빌보드도
        // 갱신할 필요가 없습니다.
        bool shouldShow = !CutsceneManager.IsAnyCutscenePlaying;
        if (nameplateInstance.gameObject.activeSelf != shouldShow)
        {
            nameplateInstance.gameObject.SetActive(shouldShow);
        }

        if (!shouldShow) return;

        nameplateTransform.position = transform.position + worldOffset;

        if (targetCamera != null)
        {
            // 카메라와 "같은 방향"을 보도록(빌보드) 맞춥니다 - MonsterHealthBar.cs와 같은 이유로,
            // 카메라 쪽으로 마주보게 하려면 반대 방향이 아니라 카메라의 회전값을 그대로 따라가는
            // 쪽이 자연스럽습니다(Canvas 정면이 항상 카메라 렌즈를 향하게 됩니다).
            nameplateTransform.rotation = targetCamera.transform.rotation;
        }
    }

    private void OnDestroy()
    {
        if (nameplateInstance != null)
        {
            Destroy(nameplateInstance.gameObject);
        }
    }
}