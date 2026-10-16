using UnityEngine;

//이 카메라가 그릴 때만 안개를 끔 (MinimapCamera에 붙임)
//미니맵 카메라는 맵 위 높은 곳에 있어서, 안개가 켜져 있으면 지도가 뿌옇게 보임
[RequireComponent(typeof(Camera))]
public class NoFogCamera : MonoBehaviour
{
    bool fogBefore;

    void OnPreRender()
    {
        fogBefore = RenderSettings.fog;
        RenderSettings.fog = false;
    }

    void OnPostRender()
    {
        RenderSettings.fog = fogBefore;
    }
}
