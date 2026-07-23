// ============================================================================
// CircleAreaIndicator.cs
// ----------------------------------------------------------------------------
// 원형 범위를 바닥에 표시하는 간단한 인디케이터.
// 코드에서 즉석으로 GameObject를 만들고 이 컴포넌트를 붙여서 사용하는 용도라
// 별도 프리팹 없이도 동작합니다 (메쉬/머티리얼을 런타임에 직접 생성).
//
// 사용 흐름: Follow(target)로 대상 위치를 계속 따라다니다가, Lock()을 호출하면
// 그 순간의 위치에 고정됩니다. (원신/보스전에서 흔한 "타겟팅 후 고정" 패턴)
// ============================================================================

using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CircleAreaIndicator : MonoBehaviour
{
    public Color color = new Color(1f, 0f, 0f, 0.35f);

    private bool locked;
    private Transform followTarget;

    private void Awake()
    {
        BuildUnitCircleMesh();

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = CreateDefaultMaterial(color);
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    /// <summary>다른 인디케이터 스크립트에서도 재사용할 수 있도록 static으로 뒀습니다.</summary>
    public static Material CreateDefaultMaterial(Color color)
    {
        // 파이프라인(Built-in/URP)에 상관없이 무난하게 동작하는 기본 셰이더.
        // 프로젝트에 맞는 반투명 머티리얼이 따로 있다면 GetComponent<MeshRenderer>().sharedMaterial을
        // 직접 교체해서 쓰셔도 됩니다.
        Material material = new Material(Shader.Find("Sprites/Default"));
        material.color = color;
        return material;
    }

    private void BuildUnitCircleMesh()
    {
        const int segments = 32;
        Vector3[] vertices = new Vector3[segments + 1];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
        for (int i = 0; i < segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = next + 1;
        }

        Mesh mesh = new Mesh { name = "CircleIndicator(unit)" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;
    }

    /// <summary>반지름 1짜리 원 메쉬를 만들어두고 스케일로 반지름을 조절합니다.</summary>
    public void SetRadius(float radius)
    {
        transform.localScale = new Vector3(radius, 1f, radius);
    }

    public void Follow(Transform target)
    {
        followTarget = target;
        locked = false;
    }

    public void Lock()
    {
        locked = true;
        followTarget = null;
    }

    public void PlaceAt(Vector3 groundPosition)
    {
        transform.position = groundPosition;
    }

    private void Update()
    {
        if (locked || followTarget == null) return;

        Vector3 p = followTarget.position;
        p.y = transform.position.y; // 높이는 최초 PlaceAt 기준을 유지
        transform.position = p;
    }
}