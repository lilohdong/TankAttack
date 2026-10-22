using UnityEngine;

//연습장 표적 (탱크 모양 표적, 레이저로 부술 벽)
//포탄 피해와 부서지는 처리는 Destructible이 함 (배틀 맵의 바위와 같은 방식)
//여기서는 색칠 + 미니맵 아이콘만 담당하고, 부서지면 TrainingManager가 Destructible.Broken으로 알게 됨
[RequireComponent(typeof(Destructible))]
public class TrainingTarget : MonoBehaviour
{
    public Color color = new Color(0.85f, 0.2f, 0.2f);
    public Renderer minimapIcon; //정찰 중일 때만 미니맵에 보임 (없으면 미니맵에 안 나옴)

    public Destructible Destructible { get; private set; }

    void Awake()
    {
        Destructible = GetComponent<Destructible>();
    }

    void Start()
    {
        //TankController와 같은 방식으로 전체를 한 색으로 칠함
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (r == minimapIcon) continue;
            foreach (Material m in r.materials)
                if (m.HasProperty("_Color")) m.color = color;
        }
        if (minimapIcon != null)
            minimapIcon.material.color = color;
    }

    void LateUpdate()
    {
        //적 탱크 아이콘처럼 정찰 중일 때만 보이게
        if (minimapIcon != null)
            minimapIcon.enabled = MinimapController.Instance != null && MinimapController.Instance.IsRevealing;
    }
}
