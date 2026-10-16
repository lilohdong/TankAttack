using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

//방 화면 : 플레이어 목록 / 탱크 색상 / 준비 / 게임 시작 / 나가기
//RoomPanel 오브젝트에 붙임. 패널이 켜질 때마다(OnEnable) 화면을 새로 그림
public class RoomPanelUI : MonoBehaviourPunCallbacks
{
    //플레이어 Custom Properties 키 (다른 스크립트에서도 같은 키를 사용)
    public const string ReadyKey = "Ready";
    public const string ColorKey = "Color"; //Vector3(r, g, b) 0~1

    const string colorPrefsKey = "TankColor"; //PlayerPrefs 저장 키
    const string battleSceneName = "scBattleField";

    [Tooltip("게임 시작에 필요한 최소 인원 (혼자 테스트할 때는 1로)")]
    public int minPlayersToStart = 2;

    [Header("방 정보")]
    public TMP_Text roomTitleText;      //"방 : Room_1234  (2 / 4)"

    [Header("플레이어 슬롯 (4개)")]
    public PlayerSlot[] slots;

    [Header("탱크 색상")]
    public Slider redSlider;
    public Slider greenSlider;
    public Slider blueSlider;
    public Image colorPreview;          //내가 고른 색 미리보기

    [Header("버튼")]
    public Button readyButton;
    public TMP_Text readyButtonText;
    public Button startButton;          //방장에게만 보임
    public Button leaveButton;

    void Awake()
    {
        readyButton.onClick.AddListener(OnClickReady);
        startButton.onClick.AddListener(OnClickStart);
        leaveButton.onClick.AddListener(OnClickLeave);

        //저장해둔 색 불러오기 (처음이면 초록색)
        Color saved = Color.green;
        ColorUtility.TryParseHtmlString("#" + PlayerPrefs.GetString(colorPrefsKey, "34CF57"), out saved);
        redSlider.SetValueWithoutNotify(saved.r);
        greenSlider.SetValueWithoutNotify(saved.g);
        blueSlider.SetValueWithoutNotify(saved.b);

        redSlider.onValueChanged.AddListener(_ => OnColorChanged());
        greenSlider.onValueChanged.AddListener(_ => OnColorChanged());
        blueSlider.onValueChanged.AddListener(_ => OnColorChanged());
    }

    public override void OnEnable()
    {
        base.OnEnable(); //Photon 콜백 등록 (지우면 콜백이 안 옴)
        if (!PhotonNetwork.InRoom) return;

        //방에 들어오면 준비 해제 + 내 색상을 다른 사람에게 알림
        SetMyProperties(false, CurrentColor());
        Refresh();
    }

    #region 버튼 / 슬라이더
    void OnClickReady()
    {
        bool ready = IsReady(PhotonNetwork.LocalPlayer);
        SetMyProperties(!ready, CurrentColor());
    }

    void OnClickStart()
    {
        if (!PhotonNetwork.IsMasterClient || !CanStart()) return;

        //게임 중에는 방 목록에 "게임 중"으로 보이고, 중간 참가는 막음
        PhotonNetwork.CurrentRoom.IsOpen = false;
        startButton.interactable = false;
        //AutomaticallySyncScene = true 이므로 방 안의 모두가 같이 이동
        PhotonNetwork.LoadLevel(battleSceneName);
    }

    void OnClickLeave()
    {
        leaveButton.interactable = false;
        //나가면 LobbyManager.OnConnectedToMaster → 로비로 돌아감
        PhotonNetwork.LeaveRoom();
    }

    void OnColorChanged()
    {
        Color c = CurrentColor();
        colorPreview.color = c;
        PlayerPrefs.SetString(colorPrefsKey, ColorUtility.ToHtmlStringRGB(c));
        //색을 바꾸면 준비는 유지한 채 색만 다시 보냄
        SetMyProperties(IsReady(PhotonNetwork.LocalPlayer), c);
    }
    #endregion

    #region Photon 콜백 : 누가 들어오거나, 나가거나, 정보가 바뀌면 다시 그림
    public override void OnPlayerEnteredRoom(Player newPlayer) { Refresh(); }
    public override void OnPlayerLeftRoom(Player otherPlayer) { Refresh(); }
    public override void OnMasterClientSwitched(Player newMasterClient) { Refresh(); }
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps) { Refresh(); }
    public override void OnLeftRoom() { leaveButton.interactable = true; }
    #endregion

    void Refresh()
    {
        if (!PhotonNetwork.InRoom) return;
        Room room = PhotonNetwork.CurrentRoom;
        roomTitleText.text = $"방 : {room.Name}   ({room.PlayerCount} / {room.MaxPlayers})";

        //ActorNumber 순서(들어온 순서)로 정렬해서 슬롯에 채움
        Player[] players = PhotonNetwork.PlayerList;
        for (int i = 0; i < slots.Length; i++)
        {
            if (i < players.Length)
            {
                Player p = players[i];
                string state = p.IsMasterClient ? "방장" : (IsReady(p) ? "준비 완료" : "대기 중");
                slots[i].Show(p.NickName, state, GetColor(p), p.IsLocal);
            }
            else
            {
                slots[i].ShowEmpty();
            }
        }

        colorPreview.color = CurrentColor();

        //방장은 준비 대신 시작 버튼을 누름
        bool isMaster = PhotonNetwork.IsMasterClient;
        readyButton.gameObject.SetActive(!isMaster);
        startButton.gameObject.SetActive(isMaster);
        startButton.interactable = CanStart();
        readyButtonText.text = IsReady(PhotonNetwork.LocalPlayer) ? "준비 취소" : "준비";
    }

    //방장을 뺀 모두가 준비했고, 최소 인원 이상이면 시작 가능
    bool CanStart()
    {
        if (PhotonNetwork.CurrentRoom.PlayerCount < minPlayersToStart) return false;
        foreach (Player p in PhotonNetwork.PlayerList)
            if (!p.IsMasterClient && !IsReady(p)) return false;
        return true;
    }

    Color CurrentColor()
    {
        return new Color(redSlider.value, greenSlider.value, blueSlider.value);
    }

    void SetMyProperties(bool ready, Color c)
    {
        //Custom Properties는 방 안의 모든 플레이어에게 자동으로 동기화됨
        Hashtable props = new Hashtable
        {
            { ReadyKey, ready },
            { ColorKey, new Vector3(c.r, c.g, c.b) }
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    public static bool IsReady(Player p)
    {
        return p.CustomProperties.TryGetValue(ReadyKey, out object v) && (bool)v;
    }

    //다른 스크립트(배틀필드의 탱크 색칠)에서도 사용
    public static Color GetColor(Player p)
    {
        if (p.CustomProperties.TryGetValue(ColorKey, out object v) && v is Vector3 c)
            return new Color(c.x, c.y, c.z);
        return Color.white;
    }
}
