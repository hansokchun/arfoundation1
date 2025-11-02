using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.UI;

public class PlaceObject : MonoBehaviour
{
    public List<GameObject> _prefabs = new List<GameObject>();
    public ARRaycastManager m_RaycastManager;
    static List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();
    public Transform _objectPool;
    Vector2 _centerVec;
    GameObject nowObject;
    PlaneClassification nowTypeTag;
    // Start is called before the first frame update
    void Start()
    {
        _centerVec = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }

    // Update is called once per frame
    void Update()
    {
        if (nowObject != null)
        {
            if (m_RaycastManager.Raycast(_centerVec, s_Hits, TrackableType.PlaneWithinPolygon))
            {
                ARPlane tPlane = s_Hits[0].trackable.GetComponent<ARPlane>();
                Debug.Log($"레이캐스트 성공, 감지된 평면 분류: {tPlane.classification}");

                if (nowTypeTag == tPlane.classification)
                {
                    nowObject.transform.position = s_Hits[0].pose.position;
                    nowObject.transform.localScale = Vector3.one;
                    Debug.Log($"오브젝트 위치 업데이트: {nowObject.transform.position}");
                }
                else
                {
                    nowObject.transform.localScale = Vector3.zero;
                    Debug.Log("평면 분류 불일치 - 오브젝트 숨김");
                }
            }
            else
            {
                nowObject.transform.localScale = Vector3.zero;
                Debug.Log("레이캐스트 실패 - 오브젝트 숨김");
            }
        }
        else
        {
            Debug.Log("현재 오브젝트 없음");
        }
    }

    public void SetObject(int type)
    {
        if (nowObject != null)
        {
            Destroy(nowObject);
            nowObject = null;
            Debug.Log("기존 오브젝트 파괴");
        }

        GameObject tObj = null;
        switch (type)
        {
            case 0:
                tObj = _prefabs[0];
                nowTypeTag = PlaneClassification.Floor;
                Debug.Log("Floor 오브젝트 선택");
                break;
            case 1:
                tObj = _prefabs[1];
                nowTypeTag = PlaneClassification.Table;
                Debug.Log("Table 오브젝트 선택");
                break;
            default:
                Debug.LogWarning("알 수 없는 타입 선택");
                break;
        }

        if (tObj != null)
        {
            nowObject = Instantiate(tObj);
            nowObject.transform.SetParent(_objectPool);
            nowObject.transform.localScale = Vector3.one;
            Debug.Log("오브젝트 생성 및 초기화 완료");
        }
        else
        {
            Debug.LogError("프리팹이 null 입니다!");
        }
    }

    public void SetObjectDone()
    {
        if (nowObject != null)
        {
            Debug.Log($"오브젝트 위치 확정: {nowObject.transform.position}");
            nowObject = null;
        }
        else
        {
            Debug.LogWarning("SetObjectDone 호출했지만 오브젝트가 없음");
        }
    }
}
