using UnityEngine;

/// <summary>
/// 개별 가구 오브젝트에 부착되어 이동, 회전, 충돌 감지 및 선택 시각화(외곽선) 기능을 수행하는 클래스입니다.
/// </summary>
public class FurnitureDragger : MonoBehaviour
{
    [Header("Settings")]
    public Vector3 overlapBoxSize = new Vector3(1f, 1f, 1f);

    private float yOffset;
    private LayerMask collisionLayerMask;

    [Header("Visual Feedback (Outline)")]
    private Renderer objectRenderer;
    public Color outlineColor = Color.yellow; 
    [Range(0f, 0.1f)] public float outlineWidth = 0.02f; 

    void Awake()
    {
        collisionLayerMask = LayerMask.GetMask("Wall", "Furniture");
        objectRenderer = GetComponent<Renderer>();
        Deselect(); // 초기화 시 외곽선 끄기
    }

    // --------------------------------------------------------------------------
    // 공개 API (FurnitureManager에서 호출)
    // --------------------------------------------------------------------------

    /// <summary>
    /// 가구를 선택 상태로 전환하고 외곽선 효과를 활성화합니다. (Shader에 _OutlineWidth 프로퍼티 필요)
    /// </summary>
    public void Select()
    {
        yOffset = transform.position.y;
        if (objectRenderer != null)
        {
            objectRenderer.material.SetFloat("_OutlineWidth", outlineWidth);
            objectRenderer.material.SetColor("_OutlineColor", outlineColor);
        }
    }

    /// <summary>
    /// 가구 선택을 해제하고 외곽선 효과를 비활성화합니다.
    /// </summary>
    public void Deselect()
    {
        if (objectRenderer != null)
        {
            objectRenderer.material.SetFloat("_OutlineWidth", 0f);
        }
    }

    /// <summary>
    /// 충돌이 발생하지 않는 경우에만 목표 위치로 이동합니다.
    /// </summary>
    public void MoveTo(Vector3 targetPoint)
    {
        Vector3 targetPos = targetPoint;
        targetPos.y = yOffset; // Y축 고정

        if (!IsCollidingAt(targetPos))
        {
            transform.position = targetPos;
        }
    }

    public void SetRotation(float yAngle)
    {
        Vector3 currentEulerAngles = transform.eulerAngles;
        transform.eulerAngles = new Vector3(currentEulerAngles.x, yAngle, currentEulerAngles.z);
    }

    // --------------------------------------------------------------------------
    // 충돌 감지 로직
    // --------------------------------------------------------------------------

    private bool IsCollidingAt(Vector3 position)
    {
        Collider[] hitColliders = Physics.OverlapBox(
            position,
            overlapBoxSize * 0.5f,
            transform.rotation,
            collisionLayerMask
        );

        foreach (var col in hitColliders)
        {
            if (col.gameObject != this.gameObject)
                return true;
        }
        return false;
    }

    // --------------------------------------------------------------------------
    // 에디터 디버깅
    // --------------------------------------------------------------------------

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, overlapBoxSize);
    }
}