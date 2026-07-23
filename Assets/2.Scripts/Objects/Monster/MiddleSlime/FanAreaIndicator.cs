// ============================================================================
// FanAreaIndicator.cs
// ----------------------------------------------------------------------------
// 부채꼴(파이 조각) 범위를 바닥에 표시하는 인디케이터. CircleAreaIndicator처럼
// 런타임에 메쉬를 직접 생성해서 별도 프리팹 없이 동작합니다.
//
// 로컬 +Z축을 "정면(각도 0)"으로 두고 좌우로 halfAngleDeg만큼 펼쳐집니다.
// 그래서 이 오브젝트의 transform.rotation을 원하는 방향으로 미리 맞춰준 뒤 Build()를 호출하세요.
// (예: transform.rotation = Quaternion.LookRotation(플레이어 방향);)
// ============================================================================

using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FanAreaIndicator : MonoBehaviour
{
    public Color color = new Color(1f, 0f, 0f, 0.35f);

    private void Awake()
    {
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = CircleAreaIndicator.CreateDefaultMaterial(color);
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    /// <summary>halfAngleDeg: 정면 기준 좌우로 벌어지는 절반 각도 (전체 각도의 절반). radius: 부채꼴 반지름.</summary>
    public void Build(float halfAngleDeg, float radius)
    {
        int segments = Mathf.Max(4, Mathf.RoundToInt(halfAngleDeg * 2f / 5f)); // 대략 5도 간격으로 분할

        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angleRad = Mathf.Deg2Rad * Mathf.Lerp(-halfAngleDeg, halfAngleDeg, t);
            // 로컬 +Z가 정면(각도 0)이 되도록 sin/cos를 배치합니다.
            vertices[i + 1] = new Vector3(Mathf.Sin(angleRad), 0f, Mathf.Cos(angleRad)) * radius;
        }

        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        Mesh mesh = new Mesh { name = "FanIndicator" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;
    }
}