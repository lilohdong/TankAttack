using UnityEngine;
using UnityEngine.EventSystems;

//큰 지도(RawImage)에 붙임 : 클릭한 위치를 0~1 비율로 바꿔서 MinimapController에 전달
public class BigMapClick : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        RectTransform rt = (RectTransform)transform;
        //Screen Space - Overlay 캔버스라서 카메라는 null
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, eventData.position, null, out Vector2 local))
        {
            Vector2 viewport = new Vector2(
                (local.x - rt.rect.xMin) / rt.rect.width,
                (local.y - rt.rect.yMin) / rt.rect.height);
            MinimapController.Instance.Pick(viewport);
        }
    }
}
