// ============================================================================
// MonsterActivationManager.cs
// ----------------------------------------------------------------------------
// 아주 넓은 범위(activationRange)를 기준으로, 플레이어에게서 너무 멀리 떨어진 몬스터들을
// 통째로 비활성화(SetActive(false))해서 최적화하는 매니저입니다. 오브젝트가 비활성화되는 순간
// MonsterFSM.Update()/Animator/NavMeshAgent 등 그 위의 모든 컴포넌트가 통째로 멈추기 때문에,
// 필드에 몬스터가 많이 깔려있는 오픈월드 구조에서 CPU 사용량을 크게 줄여줍니다.
//
// [MonsterFSM.detectRange와는 완전히 다른 개념]
//   MonsterFSM.detectRange는 "몬스터가 플레이어를 알아채고 전투를 시작하는" 좁은 사거리입니다
//   (보통 5~10m). 이 매니저의 activationRange는 그것과 별개로, 훨씬 넓게(예: 40~60m) 잡아서
//   "이 정도면 화면/전투에 아예 등장할 일이 없다"고 판단되는 몬스터만 꺼버리는 용도입니다.
//   activationRange가 모든 몬스터의 detectRange보다 항상 커야 합니다 - 안 그러면 몬스터가
//   플레이어를 감지하기도 전에 꺼져버려서 아예 반응하지 않는 몬스터처럼 보입니다.
//
// [동작 방식 - 몬스터가 스스로 검사하지 못하는 이유]
//   몬스터마다 붙는 MonsterActivation 컴포넌트가 Awake()에서 이 매니저에 스스로 등록하고,
//   파괴될 때(OnDestroy) 등록을 해제합니다. 이 매니저는 checkInterval초마다(매 프레임이 아닙니다 -
//   범위가 넓은 만큼 그렇게 자주 검사할 필요가 없습니다) 등록된 몬스터 전부와 플레이어 사이의
//   거리를 재서 activationRange 안이면 SetActive(true), 밖이면 SetActive(false)로 맞춰줍니다.
//   오브젝트가 한 번 비활성화되면 그 위의 모든 스크립트의 Update()도 같이 멈추기 때문에, "내가 다시
//   범위 안에 들어왔는지"는 몬스터 자신은 확인할 수 없습니다 - 그래서 항상 켜져있는 이 매니저가
//   대신 검사해줘야 합니다.
//
// [다른 시스템과의 관계 - 안심하고 써도 되는 이유]
//   - MonsterSpawner의 동시 생존 제한(maxAliveCount)은 그대로 정상 작동합니다. SetActive(false)는
//     오브젝트를 파괴하는 게 아니라 잠깐 꺼두는 것뿐이라, 스포너 입장에서는 여전히 "살아있는"
//     몬스터로 셉니다(의도한 동작입니다 - 멀리 있다고 새로 스폰되면 몬스터 수가 계속 늘어나기만
//     합니다).
//   - 사망 처리(Destroy(gameObject, dieDelay))는 오브젝트의 활성/비활성 여부와 무관하게 Unity
//     엔진이 예약해둔 시간에 그대로 실행되므로, 죽는 도중에 비활성화되어도 정리는 정확한 시점에
//     이뤄집니다.
//
// [씬 준비]
//   1) 별도로 씬에 미리 배치할 필요 없습니다 - 몬스터에 자동으로 붙는 MonsterActivation.Awake()가
//      이 매니저의 Instance에 처음 접근하는 순간 자동으로 생성됩니다(VFXManager 등과 동일한 방식).
//   2) Activation Range / Check Interval 값을 인스펙터에서 직접 조절하고 싶다면, 빈 오브젝트를
//      만들어 이 스크립트를 미리 씬에 배치해두세요 - 그러면 자동 생성 대신 그 인스턴스와 값을
//      그대로 사용합니다.
//   3) Player를 비워두면 시작 시 태그가 "Player"인 오브젝트를 자동으로 찾습니다.
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonsterActivationManager : MonoBehaviour
{
    [Header("플레이어")]
    [Tooltip("비워두면 시작 시 태그가 Player인 오브젝트를 자동으로 찾습니다.")]
    public Transform player;

    [Header("범위")]
    [Tooltip("플레이어로부터 이 거리(미터)를 벗어난 몬스터는 비활성화됩니다. 모든 몬스터의 detectRange " +
              "(전투 감지 사거리)보다 훨씬 크게(예: 40~60m) 잡아야, 몬스터가 플레이어를 감지하기도 전에 " +
              "꺼져버리는 문제가 생기지 않습니다.")]
    public float activationRange = 50f;

    [Header("검사 주기")]
    [Tooltip("이 시간(초)마다 한 번씩 모든 몬스터와의 거리를 검사합니다. 범위가 넓은 만큼 매 프레임 " +
              "검사할 필요는 없습니다 - 너무 짧게 잡으면(예: 0.1 미만) 몬스터 수가 많을 때 오히려 부담이 " +
              "됩니다. 0.3~1초 정도면 충분합니다.")]
    public float checkInterval = 0.5f;

    [Header("디버그")]
    [Tooltip("켜두면 몬스터가 켜지고 꺼질 때마다 콘솔에 로그를 남깁니다.")]
    public bool debugLog = false;

    private static MonsterActivationManager instance;

    /// <summary>인스턴스를 새로 만들지 않고 "이미 있으면" 그것만 돌려줍니다. 씬 종료/앱 종료 시점처럼
    /// 새로 만들 필요가 없는 정리(해제) 코드에서 Instance 대신 이걸 사용하세요.</summary>
    public static MonsterActivationManager InstanceIfExists => instance;

    public static MonsterActivationManager Instance
    {
        get
        {
            if (instance == null)
            {
                // 씬에 이미 배치해둔 인스턴스가 있으면 그걸 쓰고, 없으면 새로 만듭니다.
                instance = FindFirstObjectByType<MonsterActivationManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("MonsterActivationManager");
                    instance = go.AddComponent<MonsterActivationManager>();
                }
            }
            return instance;
        }
    }

    private readonly List<MonsterActivation> monsters = new List<MonsterActivation>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            // 씬 전환 등으로 인해 두 번째 매니저가 생기면 기존 것을 유지하고 새로 생긴 걸 제거합니다.
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        FindPlayerIfNeeded();
        StartCoroutine(CheckLoop());
    }

    private void FindPlayerIfNeeded()
    {
        if (player != null) return;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }

    private IEnumerator CheckLoop()
    {
        WaitForSeconds wait = new WaitForSeconds(checkInterval);

        while (true)
        {
            yield return wait;

            // 씬 전환 직후 등 플레이어가 아직 없을 수 있어 매번 재시도합니다(찾은 뒤로는 바로 리턴).
            FindPlayerIfNeeded();
            if (player == null) continue;

            for (int i = monsters.Count - 1; i >= 0; i--)
            {
                MonsterActivation monster = monsters[i];
                if (monster == null)
                {
                    monsters.RemoveAt(i); // 안전장치 - 정상 흐름이면 OnDestroy에서 이미 제거됩니다.
                    continue;
                }

                float distance = Vector3.Distance(player.position, monster.transform.position);
                bool shouldBeActive = distance <= activationRange;

                if (monster.gameObject.activeSelf != shouldBeActive)
                {
                    monster.gameObject.SetActive(shouldBeActive);
                    if (debugLog)
                    {
                        Debug.Log($"[MonsterActivationManager] {monster.name}: {(shouldBeActive ? "활성화" : "비활성화")} " +
                                  $"(거리 {distance:F1}m)", monster);
                    }
                }
            }
        }
    }

    /// <summary>MonsterActivation.Awake()에서 자동으로 호출됩니다. 직접 호출할 필요 없습니다.</summary>
    public void Register(MonsterActivation monster)
    {
        if (!monsters.Contains(monster))
        {
            monsters.Add(monster);
        }
    }

    /// <summary>MonsterActivation.OnDestroy()에서 자동으로 호출됩니다. 직접 호출할 필요 없습니다.</summary>
    public void Unregister(MonsterActivation monster)
    {
        monsters.Remove(monster);
    }
}