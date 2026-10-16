using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum TurnPhase
{
    Waiting,    //모두의 탱크가 생길 때까지 대기
    Playing,    //현재 턴 플레이어가 이동/조준 중
    Resolving,  //행동 후 결과(포탄 비행, 폭발)를 보는 중
    GameOver
}

//배틀 진행 관리 (scBattleField의 BattleManager 오브젝트, PhotonView 필요)
//턴 순서는 방장(MasterClient)이 정해서 RPC로 모두에게 알림
public class BattleManager : MonoBehaviourPunCallbacks
{
    public static BattleManager Instance;
    public static readonly List<TankController> Tanks = new List<TankController>(); //살아있는/죽은 모든 탱크

    [Header("턴")]
    public float turnTime = 40f;        //한 턴 제한 시간
    public float impactHoldTime = 2.5f; //포탄이 터진 뒤 그 자리를 보여주는 시간

    [Header("스폰")]
    public string tankPrefabName = "NetTank"; //Resources 폴더 안의 프리팹 이름
    public float spawnRadius = 120f;          //맵 가운데에서 떨어진 거리

    [Header("포탄")]
    public Shell shellPrefab;
    public GameObject explosionPrefab;
    public ShellSettings basicShell;
    public ShellSettings laserShell;
    public ShellSettings antiAirShell;

    [Header("참조")]
    public Terrain terrain;
    public TerrainDeformer terrainDeformer;
    public BattleCamera battleCamera;

    public TurnPhase Phase { get; private set; } = TurnPhase.Waiting;
    public int CurrentActor { get; private set; } = -1;   //지금 턴인 플레이어의 ActorNumber
    public int TurnNumber { get; private set; }
    public bool IsMyTurn => Phase == TurnPhase.Playing && CurrentActor == PhotonNetwork.LocalPlayer.ActorNumber;
    public float TimeLeft => Mathf.Max(0f, turnTime - (float)(PhotonNetwork.Time - turnStartTime));

    double turnStartTime;
    int startPlayerCount;
    int endRequestedTurn = -1;
    bool returning;

    void Awake()
    {
        Instance = this;
        Tanks.Clear();
    }

    void Start()
    {
        if (PhotonNetwork.InRoom)
        {
            SpawnMyTank();
        }
        else
        {
            //scBattleField에서 바로 Play한 경우 : 오프라인 모드로 혼자 테스트
            //OfflineMode = true → OnConnectedToMaster → CreateRoom → OnJoinedRoom → SpawnMyTank
            PhotonNetwork.NickName = PlayerPrefs.GetString("Nickname", "Player");
            PhotonNetwork.OfflineMode = true;
        }
    }

    public override void OnConnectedToMaster()
    {
        if (PhotonNetwork.OfflineMode)
            PhotonNetwork.CreateRoom("Offline");
    }

    public override void OnJoinedRoom()
    {
        if (PhotonNetwork.OfflineMode)
            SpawnMyTank();
    }

    #region 스폰
    void SpawnMyTank()
    {
        //오프라인 테스트일 때는 방 화면을 거치지 않았으므로 저장된 색을 사용
        if (!PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(RoomPanelUI.ColorKey))
        {
            ColorUtility.TryParseHtmlString("#" + PlayerPrefs.GetString("TankColor", "34CF57"), out Color c);
            PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { RoomPanelUI.ColorKey, new Vector3(c.r, c.g, c.b) } });
        }

        //들어온 순서대로 맵 가운데를 기준으로 네 방향에 배치
        Player[] players = PhotonNetwork.PlayerList;
        int index = System.Array.IndexOf(players, PhotonNetwork.LocalPlayer);
        float angle = (45f + 90f * index) * Mathf.Deg2Rad;
        Vector3 center = terrain.transform.position + new Vector3(terrain.terrainData.size.x, 0f, terrain.terrainData.size.z) * 0.5f;
        Vector3 pos = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spawnRadius;
        pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y + 2f;
        Quaternion rot = Quaternion.LookRotation(new Vector3(center.x - pos.x, 0f, center.z - pos.z));

        //PhotonNetwork.Instantiate : 방 안의 모두에게 같은 탱크가 생기고, 주인은 나
        GameObject tank = PhotonNetwork.Instantiate(tankPrefabName, pos, rot);
        battleCamera.FollowTank(tank.transform);

        if (PhotonNetwork.IsMasterClient)
            StartCoroutine(WaitForTanksAndStart());
    }

    //방장 : 모든 플레이어의 탱크가 생기면 첫 턴 시작
    IEnumerator WaitForTanksAndStart()
    {
        float waited = 0f;
        while (Tanks.Count < PhotonNetwork.CurrentRoom.PlayerCount && waited < 10f)
        {
            waited += Time.deltaTime;
            yield return null;
        }
        yield return new WaitForSeconds(1.5f);
        startPlayerCount = PhotonNetwork.CurrentRoom.PlayerCount;
        StartNextTurn();
    }
    #endregion

    #region 턴 (방장이 결정)
    void StartNextTurn()
    {
        if (!PhotonNetwork.IsMasterClient || Phase == TurnPhase.GameOver) return;
        if (CheckGameOver()) return;

        //살아있는 탱크의 주인을 ActorNumber 순서로 정렬
        List<int> order = new List<int>();
        foreach (TankController t in Tanks)
            if (t != null && t.Health.IsAlive && t.photonView.Owner != null)
                order.Add(t.photonView.OwnerActorNr);
        if (order.Count == 0) return;
        order.Sort();

        //지금 턴인 사람 다음 번호, 없으면 처음으로
        int next = order[0];
        foreach (int actor in order)
        {
            if (actor > CurrentActor)
            {
                next = actor;
                break;
            }
        }
        photonView.RPC(nameof(RPC_StartTurn), RpcTarget.All, next, TurnNumber + 1, PhotonNetwork.Time);
    }

    [PunRPC]
    void RPC_StartTurn(int actor, int turnNumber, double startTime)
    {
        CurrentActor = actor;
        TurnNumber = turnNumber;
        turnStartTime = startTime;
        Phase = TurnPhase.Playing;

        TankController tank = FindTank(actor);
        if (tank != null)
            battleCamera.FollowTank(tank.transform);

        if (actor == PhotonNetwork.LocalPlayer.ActorNumber)
        {
            //정찰 효과는 내 다음 턴이 올 때까지만
            MinimapController.Instance.SetReveal(false);
            if (TankController.Local != null)
                TankController.Local.OnMyTurnStart();
            BattleUI.Instance.ShowNotice("내 턴입니다!", 2f);
        }
        else if (tank != null)
        {
            BattleUI.Instance.ShowNotice($"{tank.OwnerName} 님의 턴", 2f);
        }
    }

    //내 턴을 끝냄 (TankController가 호출)
    public void RequestEndTurn()
    {
        if (endRequestedTurn == TurnNumber || Phase == TurnPhase.GameOver) return;
        endRequestedTurn = TurnNumber;
        Phase = TurnPhase.Resolving;
        photonView.RPC(nameof(RPC_EndTurn), RpcTarget.MasterClient, TurnNumber);
    }

    [PunRPC]
    void RPC_EndTurn(int turnNumber)
    {
        if (turnNumber != TurnNumber) return; //이미 넘어간 턴
        StartNextTurn();
    }

    void Update()
    {
        if (Phase == TurnPhase.Playing && IsMyTurn && TimeLeft <= 0f && TankController.Local != null)
            TankController.Local.OnTimeUp();

        //방장 : 턴 주인이 응답이 없으면(튕김 등) 강제로 다음 턴
        if (PhotonNetwork.IsMasterClient && (Phase == TurnPhase.Playing || Phase == TurnPhase.Resolving) &&
            PhotonNetwork.Time - turnStartTime > turnTime + 20f)
            StartNextTurn();
    }
    #endregion

    #region 행동 / 포탄 / 폭발
    //발사, 회복, 정찰 : 모두의 화면에서 타이머를 멈추고 알림 표시
    public void AnnounceAction(string message)
    {
        photonView.RPC(nameof(RPC_Announce), RpcTarget.All, message);
    }

    [PunRPC]
    void RPC_Announce(string message)
    {
        if (Phase == TurnPhase.Playing) Phase = TurnPhase.Resolving;
        BattleUI.Instance.ShowNotice(message, 2f);
    }

    public ShellSettings GetSettings(ShellType type)
    {
        switch (type)
        {
            case ShellType.Laser: return laserShell;
            case ShellType.AntiAir: return antiAirShell;
            default: return basicShell;
        }
    }

    //TankController.RPC_Fire에서 모든 클라이언트가 호출
    public void SpawnShell(TankController owner, ShellType type, Vector3 pos, Vector3 velocity, bool authority)
    {
        if (Phase == TurnPhase.Playing) Phase = TurnPhase.Resolving;
        Shell shell = Instantiate(shellPrefab, pos, Quaternion.LookRotation(velocity));
        shell.Init(owner, type, GetSettings(type), velocity, authority);
        battleCamera.FollowShell(shell.transform); //모두가 포탄을 따라감
        BattleUI.Instance.ShowNotice($"{owner.OwnerName} : {GetSettings(type).displayName} 발사!", 1.5f);
    }

    //쏜 사람이 "여기 맞았다"고 알림
    public void ReportImpact(ShellType type, Vector3 point, int shooterActor)
    {
        photonView.RPC(nameof(RPC_Explode), RpcTarget.All, (byte)type, point, shooterActor);
    }

    [PunRPC]
    void RPC_Explode(byte typeByte, Vector3 point, int shooterActor)
    {
        ShellSettings s = GetSettings((ShellType)typeByte);

        //아직 날고 있는 시각용 포탄 정리 (다른 사람 화면의 포탄은 살짝 다른 곳에 맞았을 수 있음)
        foreach (Shell shell in Shell.Active.ToArray())
            shell.Remove();

        //1. 폭발 이펙트
        if (explosionPrefab != null)
        {
            GameObject fx = Instantiate(explosionPrefab, point, Quaternion.identity);
            fx.transform.localScale *= s.effectScale;
            Destroy(fx, 2f);
        }

        //2. 지형 파괴 (모든 클라이언트가 같은 위치를 같은 양만큼 파서 지형이 같아짐)
        if (s.craterRadius > 0f)
            terrainDeformer.Deform(point, s.craterRadius, s.craterDepth);

        //3. 카메라는 맞은 곳을 잠시 보여줌
        battleCamera.HoldAt(point);

        //4. 피해 : 각자 "내 탱크"만 계산해서 체력을 보냄
        foreach (TankController t in Tanks)
        {
            if (t == null || !t.photonView.IsMine || !t.Health.IsAlive) continue;
            float dist = t.DistanceTo(point);
            if (dist >= s.damageRadius) continue;
            //가까울수록 아프게 (가장자리에서도 최소 30%)
            float rate = Mathf.Lerp(1f, 0.3f, dist / s.damageRadius);
            t.Health.TakeDamage(Mathf.RoundToInt(s.damage * rate));
        }
    }
    #endregion

    #region 승패
    //방장 : 살아남은 탱크가 1대 이하면 게임 끝
    bool CheckGameOver()
    {
        int alive = 0;
        int winner = -1;
        foreach (TankController t in Tanks)
        {
            if (t != null && t.Health.IsAlive && t.photonView.Owner != null)
            {
                alive++;
                winner = t.photonView.OwnerActorNr;
            }
        }
        //혼자 테스트할 때는 내가 죽기 전까지 계속
        bool over = alive == 0 || (startPlayerCount > 1 && alive <= 1);
        if (!over) return false;

        photonView.RPC(nameof(RPC_GameOver), RpcTarget.All, alive == 1 ? winner : -1);
        return true;
    }

    [PunRPC]
    void RPC_GameOver(int winnerActor)
    {
        Phase = TurnPhase.GameOver;
        Player winner = PhotonNetwork.CurrentRoom.GetPlayer(winnerActor);
        bool iWon = winnerActor == PhotonNetwork.LocalPlayer.ActorNumber;
        string result = winner == null ? "무승부" : (iWon ? "승리!" : "패배...");
        string detail = winner == null ? "살아남은 탱크가 없습니다" : $"최후의 생존자 : {winner.NickName}";
        BattleUI.Instance.ShowGameOver(result, detail);

        TankController winTank = FindTank(winnerActor);
        if (winTank != null)
            battleCamera.FollowTank(winTank.transform);

        //방장이 잠시 뒤 모두를 방 화면으로 데려감 (버튼으로 바로 갈 수도 있음)
        if (PhotonNetwork.IsMasterClient)
            StartCoroutine(ReturnAfter(10f));
    }

    IEnumerator ReturnAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnToRoom();
    }

    //방장만 호출 : 모두 scTitle(방 화면)로 돌아감
    public void ReturnToRoom()
    {
        if (returning || !PhotonNetwork.IsMasterClient) return;
        returning = true;

        if (PhotonNetwork.OfflineMode)
        {
            PhotonNetwork.LeaveRoom();
            PhotonNetwork.OfflineMode = false;
            SceneManager.LoadScene("scTitle");
            return;
        }

        PhotonNetwork.CurrentRoom.IsOpen = true; //다시 참가할 수 있게
        //네트워크 탱크와 캐시를 지움 (안 지우면 나중에 들어온 사람에게 예전 탱크가 생김)
        PhotonNetwork.DestroyAll();
        PhotonNetwork.LoadLevel("scTitle"); //AutomaticallySyncScene → 모두 같이 이동
    }
    #endregion

    #region 나가기 / 방장 변경
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (!PhotonNetwork.IsMasterClient || Phase == TurnPhase.GameOver) return;
        //나간 사람의 턴이었으면 바로 다음 턴, 아니면 승패만 확인
        if (otherPlayer.ActorNumber == CurrentActor)
            StartNextTurn();
        else
            CheckGameOver();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        //새 방장이 된 경우, 턴 주인이 이미 나갔으면 이어서 진행
        if (PhotonNetwork.IsMasterClient && FindTank(CurrentActor) == null && Phase != TurnPhase.GameOver)
            StartNextTurn();
    }

    public void LeaveBattle()
    {
        if (returning) return;
        returning = true;
        if (PhotonNetwork.OfflineMode)
        {
            PhotonNetwork.LeaveRoom();
            PhotonNetwork.OfflineMode = false;
            SceneManager.LoadScene("scTitle");
            return;
        }
        PhotonNetwork.LeaveRoom(); //내 탱크는 자동으로 사라짐
        SceneManager.LoadScene("scTitle"); //타이틀로 가면 LobbyManager가 로비로 다시 들어감
    }
    #endregion

    public TankController FindTank(int actor)
    {
        foreach (TankController t in Tanks)
            if (t != null && t.photonView.OwnerActorNr == actor)
                return t;
        return null;
    }
}
