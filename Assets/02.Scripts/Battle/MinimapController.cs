using System;
using UnityEngine;
using UnityEngine.UI;

//우측 상단 미니맵 + 대공탄 목표를 고르는 큰 지도
//MinimapCamera(위에서 내려다보는 직교 카메라)가 RenderTexture에 그리고, RawImage로 화면에 표시
//탱크 아이콘은 MinimapIcon 레이어라서 미니맵 카메라에만 보임
public class MinimapController : MonoBehaviour
{
    public static MinimapController Instance;
    public static bool IsBigMapOpen => Instance != null && Instance.bigMapPanel.activeSelf;

    public Camera minimapCamera;
    public RawImage smallMap;
    public RawImage bigMap;
    public GameObject bigMapPanel;
    public GameObject revealLabel;   //"정찰 중" 표시

    public bool IsRevealing { get; private set; }

    RenderTexture renderTexture;
    Action<Vector3> onPick;          //큰 지도를 클릭했을 때 실행할 함수 (대공탄 발사)

    void Awake()
    {
        Instance = this;
        renderTexture = new RenderTexture(512, 512, 16);
        minimapCamera.targetTexture = renderTexture;
        smallMap.texture = renderTexture;
        bigMap.texture = renderTexture;
        bigMapPanel.SetActive(false);
        revealLabel.SetActive(false);
    }

    void Update()
    {
        if (IsBigMapOpen && (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)))
            CloseBigMap();
    }

    void LateUpdate()
    {
        //내 탱크는 항상, 적 탱크는 정찰 중일 때만 아이콘 표시
        foreach (TankController t in BattleManager.Tanks)
        {
            if (t == null) continue;
            bool visible = t.photonView.IsMine || (IsRevealing && t.Health.IsAlive);
            t.SetIconVisible(visible);
        }
    }

    public void SetReveal(bool reveal)
    {
        IsRevealing = reveal;
        revealLabel.SetActive(reveal);
    }

    public void OpenBigMap(Action<Vector3> onPick)
    {
        this.onPick = onPick;
        bigMapPanel.SetActive(true);
    }

    public void CloseBigMap()
    {
        onPick = null;
        bigMapPanel.SetActive(false);
    }

    //큰 지도의 클릭 위치(0~1) → 미니맵 카메라에서 Ray를 쏴서 지형 위의 월드 좌표를 구함
    public void Pick(Vector2 viewport)
    {
        Ray ray = minimapCamera.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, 2000f, 1 << 8)) //8번 = TERRAIN
        {
            Action<Vector3> callback = onPick;
            CloseBigMap();
            callback?.Invoke(hit.point);
        }
    }
}
