using TMPro;
using UnityEngine;
using UnityEngine.UI;

//방 화면의 플레이어 한 칸 (RoomPanel 안에 4개 배치)
public class PlayerSlot : MonoBehaviour
{
    public Image colorImage;    //탱크 색상 표시
    public TMP_Text nameText;
    public TMP_Text stateText;  //방장 / 준비 완료 / 대기 중
    public Image background;

    static readonly Color normalBg = new Color(1f, 1f, 1f, 0.85f);
    static readonly Color localBg = new Color(1f, 0.95f, 0.6f, 0.95f); //내 칸은 노란색으로 강조
    static readonly Color emptyBg = new Color(1f, 1f, 1f, 0.3f);

    public void Show(string nickname, string state, Color tankColor, bool isLocal)
    {
        nameText.text = isLocal ? nickname + " (나)" : nickname;
        stateText.text = state;
        colorImage.enabled = true;
        colorImage.color = tankColor;
        background.color = isLocal ? localBg : normalBg;
    }

    public void ShowEmpty()
    {
        nameText.text = "빈 자리";
        stateText.text = "";
        colorImage.enabled = false;
        background.color = emptyBg;
    }
}
