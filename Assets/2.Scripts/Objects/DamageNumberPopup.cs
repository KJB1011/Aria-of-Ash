// ============================================================================
// DamageNumberPopup.cs
// ----------------------------------------------------------------------------
// 데미지 숫자 프리팹 하나에 붙이는 컴포넌트입니다. DamageNumberManager가 오브젝트 풀에서
// 꺼내 Play()를 호출해주면, 스스로 위로 떠오르면서 옅어지는 애니메이션을 재생하고 끝나면
// 스스로 DamageNumberManager.ReturnToPool()을 호출해서 반납합니다 - 매니저는 "언제 반납할지"를
// 몰라도 되고, 이 컴포넌트가 자기 생애주기를 알아서 책임지는 구조입니다.
//
// [프리팹 준비]
//   1) 빈 오브젝트를 만들고, 자식으로 3D TextMeshPro(메뉴: GameObject > 3D Object >
//      Text - TextMeshPro. Canvas 안에 들어가는 TextMeshProUGUI가 아니라 월드 스페이스에 직접
//      떠 있는 3D 버전입니다)를 추가하세요. Alignment는 Center/Middle을 추천합니다.
//   2) 루트 오브젝트에 이 스크립트(DamageNumberPopup)를 붙이고, Label에 위에서 만든
//      TextMeshPro를 연결하세요 (비워두면 자식에서 자동으로 찾습니다).
//   3) TextMeshPro 컴포넌트 자체에 설정해둔 Font Size가 "치명타가 아닐 때"의 기준 크기로
//      쓰입니다 - Awake에서 그 값을 기억해뒀다가, 치명타로 표시할 때는 critFontScale을 곱해서
//      더 크게 보여줍니다.
//   4) 완성되면 Assets/Resources/HUD/ 폴더 아래에 "DamageNumber"라는 이름으로 프리팹을
//      저장하세요 (DamageNumberManager가 기본으로 이 이름을 찾습니다).
//   5) 씬에 배치할 필요 없습니다 - DamageNumberManager가 Resources에서 알아서 불러와 풀링합니다.
// ============================================================================

using System.Collections;
using TMPro;
using UnityEngine;

public class DamageNumberPopup : MonoBehaviour, IPoolable
{
    [Header("참조")]
    [Tooltip("비워두면 자식에서 자동으로 찾습니다.")]
    public TextMeshPro label;

    [Header("애니메이션")]
    [Tooltip("생성된 뒤 이 시간(초)이 지나면 애니메이션이 끝나고 자동으로 풀에 반납됩니다.")]
    public float lifetime = 0.8f;
    [Tooltip("위로 떠오르는 총 거리(미터).")]
    public float riseDistance = 1.2f;
    [Tooltip("진행도(0~1)에 따라 얼마나 떠올랐는지의 곡선. 기본값은 처음에 빠르게 튀어오르고 " +
              "점점 느려지는 모양입니다.")]
    public AnimationCurve riseCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 3f),
        new Keyframe(1f, 1f, 0f, 0f));
    [Tooltip("진행도(0~1)에 따른 알파값. 기본값은 끝 무렵(70%~100%)에 빠르게 사라지는 모양입니다.")]
    public AnimationCurve alphaCurve = new AnimationCurve(
        new Keyframe(0f, 1f), new Keyframe(0.7f, 1f), new Keyframe(1f, 0f));
    [Tooltip("같은 자리에 여러 숫자가 거의 동시에 뜰 때(콤보, 범위 공격 등) 서로 겹치지 않도록 " +
              "좌우/앞뒤로 무작위로 흩뿌리는 범위(미터). 0으로 두면 흩뿌리지 않습니다.")]
    public float randomHorizontalJitter = 0.35f;

    [Header("색상")]
    [Tooltip("몬스터가 일반 데미지를 입었을 때 색")]
    public Color enemyNormalColor = Color.white;
    [Tooltip("몬스터가 치명타로 맞았을 때 색")]
    public Color enemyCritColor = new Color(1f, 0.55f, 0.1f);
    [Tooltip("플레이어가 데미지를 입었을 때 색 (몬스터는 아직 치명타가 없어서 구분 없이 이 색 하나만 씁니다)")]
    public Color playerDamageColor = new Color(1f, 0.25f, 0.2f);
    [Tooltip("치명타로 표시할 때, 기준 폰트 크기(TextMeshPro에 설정해둔 값)에 곱해줄 배율")]
    public float critFontScale = 1.35f;

    private float baseFontSize;
    private Camera mainCamera;
    private Coroutine animateRoutine;

    private void Awake()
    {
        if (label == null) label = GetComponentInChildren<TextMeshPro>(true);
        if (label != null) baseFontSize = label.fontSize;
        else Debug.LogWarning("[DamageNumberPopup] TextMeshPro(Label)를 찾을 수 없습니다. 프리팹 구성을 확인해주세요.", this);
    }

    /// <summary>DamageNumberManager 전용 진입점입니다. 텍스트/색/크기를 설정하고 상승+페이드
    /// 애니메이션을 시작합니다. 호출 시점의 transform.position(스폰 위치)을 시작점으로 씁니다.</summary>
    public void Play(float amount, bool isCrit, DamageNumberTeam team)
    {
        if (label == null) return;

        mainCamera = Camera.main;

        label.text = Mathf.Max(0, Mathf.RoundToInt(amount)).ToString();
        label.fontSize = baseFontSize * (isCrit ? critFontScale : 1f);

        Color color = team == DamageNumberTeam.Player
            ? playerDamageColor
            : (isCrit ? enemyCritColor : enemyNormalColor);
        color.a = 1f;
        label.color = color;

        if (randomHorizontalJitter > 0f)
        {
            Vector3 jitter = new Vector3(
                Random.Range(-randomHorizontalJitter, randomHorizontalJitter), 0f,
                Random.Range(-randomHorizontalJitter, randomHorizontalJitter));
            transform.position += jitter;
        }

        if (mainCamera != null) transform.rotation = mainCamera.transform.rotation;

        if (animateRoutine != null) StopCoroutine(animateRoutine);
        animateRoutine = StartCoroutine(AnimateAndRelease());
    }

    /// <summary>IPoolable 구현. 풀에서 꺼내져 활성화된 직후 호출됩니다. 실제 값 채우기는 뒤이어
    /// 호출되는 Play()가 담당하므로, 여기서는 이전 사용의 흔적(진행 중이던 코루틴/스케일)만 정리합니다.</summary>
    public void OnGetFromPool()
    {
        if (animateRoutine != null)
        {
            StopCoroutine(animateRoutine);
            animateRoutine = null;
        }
        transform.localScale = Vector3.one;
    }

    /// <summary>IPoolable 구현. 풀로 반납되어 비활성화되기 직전 호출됩니다. 애니메이션 코루틴이
    /// 남아있다면 정리해서, 다음에 재사용될 때 옛 코루틴이 뒤늦게 끼어들지 않도록 합니다.</summary>
    public void OnReleaseToPool()
    {
        if (animateRoutine != null)
        {
            StopCoroutine(animateRoutine);
            animateRoutine = null;
        }
    }

    private IEnumerator AnimateAndRelease()
    {
        Vector3 startPosition = transform.position;
        float elapsed = 0f;

        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / lifetime);

            transform.position = startPosition + Vector3.up * riseDistance * riseCurve.Evaluate(t);
            if (mainCamera != null) transform.rotation = mainCamera.transform.rotation;

            if (label != null)
            {
                Color c = label.color;
                c.a = alphaCurve.Evaluate(t);
                label.color = c;
            }

            yield return null;
        }

        animateRoutine = null;
        DamageNumberManager.Instance.ReturnToPool(gameObject);
    }
}