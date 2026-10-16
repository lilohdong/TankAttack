using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

//타이틀(닉네임) → 로비(방 목록 / 방 만들기 / 랜덤 참가) → 방
//MonoBehaviourPunCallbacks : Photon 이벤트(접속 성공, 방 입장 등)를 override로 받을 수 있는 클래스
public class LobbyManager : MonoBehaviourPunCallbacks
{
    const string gameVersion = "1.0";   //버전이 다른 클라이언트끼리는 매칭되지 않음
    const int maxPlayers = 4;           //방 최대 인원
    const int maxNicknameLength = 12;
    const string nicknameKey = "Nickname"; //PlayerPrefs 저장 키

    [Header("패널")]
    public GameObject titlePanel;
    public GameObject lobbyPanel;
    public GameObject roomPanel;
    public GameObject createRoomPopup;

    [Header("공통")]
    public TMP_Text serverStatusText;   //좌측 상단 "서버 상태 : ..."
    public TMP_Text noticeText;         //안내/오류 메시지

    [Header("타이틀")]
    public TMP_InputField nicknameInput;
    public Button startButton;

    [Header("로비")]
    public Button createRoomButton;
    public Button randomJoinButton;
    public Button backButton;
    public Transform roomListContent;   //Scroll View의 Content
    public RoomListItem roomItemPrefab; //방 목록 한 줄 프리팹
    public GameObject emptyListText;    //"열린 방이 없습니다"

    [Header("방 만들기 팝업")]
    public TMP_InputField roomNameInput;
    public Button confirmCreateButton;
    public Button cancelCreateButton;

    //서버에서 받은 방 정보 (방 이름, 정보)
    Dictionary<string, RoomInfo> cachedRooms = new Dictionary<string, RoomInfo>();
    //화면에 만들어진 방 목록 오브젝트
    List<GameObject> roomItems = new List<GameObject>();

    void Awake()
    {
        //방장이 PhotonNetwork.LoadLevel을 호출하면 방 안의 모두가 같은 씬으로 이동
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.GameVersion = gameVersion;
    }

    void Start()
    {
        //이전에 쓰던 닉네임 불러오기
        nicknameInput.text = PlayerPrefs.GetString(nicknameKey, "");
        nicknameInput.characterLimit = maxNicknameLength;

        startButton.onClick.AddListener(OnClickStart);
        createRoomButton.onClick.AddListener(() => createRoomPopup.SetActive(true));
        randomJoinButton.onClick.AddListener(OnClickRandomJoin);
        backButton.onClick.AddListener(OnClickBack);
        confirmCreateButton.onClick.AddListener(OnClickConfirmCreate);
        cancelCreateButton.onClick.AddListener(() => createRoomPopup.SetActive(false));

        createRoomPopup.SetActive(false);
        SetNotice("");

        if (PhotonNetwork.InRoom)
        {
            //배틀이 끝나고 돌아온 경우 : 방 화면을 바로 보여줌
            ShowPanel(roomPanel);
            SetServerStatus("방 안");
        }
        else
        {
            ShowPanel(titlePanel);
            SetServerStatus(PhotonNetwork.IsConnected ? "접속됨" : "접속 안 됨");
        }
    }

    #region 버튼 이벤트
    void OnClickStart()
    {
        string nickname = nicknameInput.text.Trim();
        if (string.IsNullOrEmpty(nickname))
        {
            SetNotice("닉네임을 입력해주세요.");
            return;
        }

        PhotonNetwork.NickName = nickname; //다른 플레이어에게 보이는 이름
        PlayerPrefs.SetString(nicknameKey, nickname);

        startButton.interactable = false; //접속 중 중복 클릭 방지
        SetNotice("");

        if (PhotonNetwork.IsConnectedAndReady)
        {
            //이미 접속된 상태(로비에서 뒤로 왔다가 다시 시작)면 로비만 들어감
            PhotonNetwork.JoinLobby();
        }
        else
        {
            SetServerStatus("접속 중...");
            PhotonNetwork.ConnectUsingSettings(); //PhotonServerSettings의 AppId로 접속
        }
    }

    void OnClickBack()
    {
        //로비에서 나가 타이틀로 (닉네임을 바꿀 수 있도록)
        PhotonNetwork.LeaveLobby();
        ClearRoomList();
        ShowPanel(titlePanel);
        startButton.interactable = true;
    }

    void OnClickConfirmCreate()
    {
        string roomName = roomNameInput.text.Trim();
        //방 이름을 비워두면 자동으로 만들어줌
        if (string.IsNullOrEmpty(roomName))
            roomName = "Room_" + Random.Range(1000, 10000);

        createRoomPopup.SetActive(false);
        SetLobbyButtonsInteractable(false);
        SetNotice($"'{roomName}' 방을 만드는 중...");
        PhotonNetwork.CreateRoom(roomName, CreateRoomOptions());
    }

    void OnClickRandomJoin()
    {
        SetLobbyButtonsInteractable(false);
        SetNotice("참가할 수 있는 방을 찾는 중...");
        //들어갈 방이 있으면 참가, 없으면 새 방을 만듦
        PhotonNetwork.JoinRandomOrCreateRoom(
            expectedMaxPlayers: maxPlayers,
            roomName: "Room_" + Random.Range(1000, 10000),
            roomOptions: CreateRoomOptions());
    }

    //방 목록의 한 줄을 클릭했을 때 (RoomListItem이 호출)
    void OnClickRoomItem(string roomName)
    {
        SetLobbyButtonsInteractable(false);
        SetNotice($"'{roomName}' 방에 참가하는 중...");
        PhotonNetwork.JoinRoom(roomName);
    }
    #endregion

    #region Photon 콜백
    public override void OnConnectedToMaster()
    {
        //처음 접속했을 때 + 방에서 나왔을 때 모두 호출됨
        //로비에 들어가야 방 목록(OnRoomListUpdate)을 받을 수 있음
        SetServerStatus("접속됨");
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        ClearRoomList();
        ShowPanel(lobbyPanel);
        SetLobbyButtonsInteractable(true);
        SetServerStatus("로비");
        SetNotice("");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        ClearRoomList();
        ShowPanel(titlePanel);
        startButton.interactable = true;
        SetServerStatus("접속 안 됨");
        if (cause != DisconnectCause.DisconnectByClientLogic)
            SetNotice($"서버 연결이 끊어졌습니다. ({cause})");
    }

    //방 목록이 바뀔 때마다 호출됨. 바뀐 방들만 전달되므로 직접 모아둬야 함
    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        foreach (RoomInfo info in roomList)
        {
            //사라진 방(삭제, 비공개 전환)은 목록에서 제거
            if (info.RemovedFromList)
                cachedRooms.Remove(info.Name);
            else
                cachedRooms[info.Name] = info;
        }
        RefreshRoomListUI();
    }

    public override void OnJoinedRoom()
    {
        //방을 직접 만든 경우에도 OnJoinedRoom이 호출됨
        createRoomPopup.SetActive(false);
        ShowPanel(roomPanel);
        SetServerStatus($"방 : {PhotonNetwork.CurrentRoom.Name}");
        SetNotice("");
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        SetLobbyButtonsInteractable(true);
        if (returnCode == ErrorCode.GameIdAlreadyExists)
            SetNotice("이미 같은 이름의 방이 있습니다.");
        else
            SetNotice($"방 만들기 실패 : {message}");
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        SetLobbyButtonsInteractable(true);
        switch (returnCode)
        {
            case ErrorCode.GameDoesNotExist: SetNotice("방이 사라졌습니다."); break;
            case ErrorCode.GameFull:         SetNotice("방이 가득 찼습니다."); break;
            case ErrorCode.GameClosed:       SetNotice("이미 게임이 시작된 방입니다."); break;
            default:                         SetNotice($"방 참가 실패 : {message}"); break;
        }
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        SetLobbyButtonsInteractable(true);
        SetNotice($"랜덤 참가 실패 : {message}");
    }
    #endregion

    #region 방 목록 UI
    void RefreshRoomListUI()
    {
        //기존 목록 오브젝트를 지우고 새로 만듦
        foreach (GameObject item in roomItems)
            Destroy(item);
        roomItems.Clear();

        foreach (RoomInfo info in cachedRooms.Values)
        {
            RoomListItem item = Instantiate(roomItemPrefab, roomListContent);
            item.Setup(info, OnClickRoomItem);
            roomItems.Add(item.gameObject);
        }

        emptyListText.SetActive(cachedRooms.Count == 0);
    }

    void ClearRoomList()
    {
        cachedRooms.Clear();
        RefreshRoomListUI();
    }
    #endregion

    RoomOptions CreateRoomOptions()
    {
        return new RoomOptions
        {
            MaxPlayers = maxPlayers,
            IsVisible = true, //방 목록, 랜덤 참가 대상에 포함
            IsOpen = true     //게임 시작 시 false로 바꿔서 중간 참가를 막을 예정
        };
    }

    //패널 하나만 켜고 나머지는 끔
    void ShowPanel(GameObject panel)
    {
        titlePanel.SetActive(panel == titlePanel);
        lobbyPanel.SetActive(panel == lobbyPanel);
        roomPanel.SetActive(panel == roomPanel);
    }

    void SetLobbyButtonsInteractable(bool value)
    {
        createRoomButton.interactable = value;
        randomJoinButton.interactable = value;
        backButton.interactable = value;
    }

    void SetServerStatus(string msg)
    {
        serverStatusText.text = "서버 상태 : " + msg;
    }

    void SetNotice(string msg)
    {
        noticeText.text = msg;
        if (!string.IsNullOrEmpty(msg))
            Debug.Log("[Lobby] " + msg);
    }
}
