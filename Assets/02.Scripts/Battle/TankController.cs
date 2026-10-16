using System.Collections;
using Photon.Pun;
using UnityEngine;

//네트워크 탱크 조작 (Resources/NetTank 프리팹에 붙임)
//내 턴일 때만 : 이동(거리 제한) → 조준 → 행동 1번(발사 / 회복 / 정찰) → 턴 종료
//IPunObservable : 포탑 회전, 포신 각도를 다른 사람에게 보냄
public class TankController : MonoBehaviourPun, IPunObservable
{
    public static TankController Local; //내 탱크

    [Header("이동")]
    public float moveSpeed = 12f;
    public float rotSpeed = 50f;
    public float maxMoveDistance = 40f;  //한 턴에 움직일 수 있는 거리

    [Header("조준")]
    public Transform turret;
    public Transform cannon;
    public Transform firePos;
    public float turretRotSpeed = 5f;
    public float pitchPerScroll = 50f;   //마우스 휠 한 칸(0.1) = 5도
    public float minPitch = -45f;        //포신을 가장 높이 든 각도 (X축이 -일수록 위)
    public float maxPitch = 5f;

    [Header("발사")]
    public float minShellSpeed = 20f;
    public float maxShellSpeed = 90f;
    public float chargeSpeed = 0.6f;     //1초에 파워 60% 충전
    public float laserSpeed = 200f;
    public float antiAirHeight = 120f;   //대공탄이 떨어지기 시작하는 높이

    [Header("사용 횟수 (게임 전체)")]
    public int laserCount = 3;
    public int antiAirCount = 3;
    public int healCount = 2;
    public int healAmount = 30;

    [Header("기타")]
    public Renderer trackRenderer;       //궤도 텍스처 스크롤
    public Renderer minimapIcon;         //미니맵에만 보이는 아이콘
    public AudioClip fireSfx;

    public TankHealth Health { get; private set; }
    public ShellType SelectedShell { get; private set; } = ShellType.Basic;
    public float MoveLeft { get; private set; }
    public float Power { get; private set; }
    public bool IsCharging { get; private set; }
    public bool ActionUsed { get; private set; }
    public string OwnerName => photonView.Owner != null ? photonView.Owner.NickName : "?";
    public Color TankColor { get; private set; } = Color.white;

    //조작 가능 : 내 탱크 + 내 턴 + 아직 행동 안 함 + 살아 있음 + 큰 지도가 닫혀 있음
    bool CanControl =>
        photonView.IsMine && BattleManager.Instance != null && BattleManager.Instance.IsMyTurn &&
        !ActionUsed && Health.IsAlive && !MinimapController.IsBigMapOpen;

    Rigidbody rb;
    AudioSource sfx;
    float h, v;
    float pitch;
    float trackOffset;
    RaycastHit hit;
    //원격 탱크가 받은 값
    float netTurretY, netPitch;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        sfx = GetComponent<AudioSource>();
        Health = GetComponent<TankHealth>();
        rb.centerOfMass = new Vector3(0f, -0.5f, 0f); //무게중심을 낮게 (넘어지지 않게)

        pitch = NormalizeAngle(cannon.localEulerAngles.x);
        BattleManager.Tanks.Add(this);

        if (photonView.IsMine)
            Local = this;
        else
            rb.isKinematic = true; //남의 탱크는 물리 대신 받은 위치로만 움직임 (PhotonTransformView)
    }

    void Start()
    {
        //방에서 고른 색으로 칠하기 (궤도는 원래 색 유지)
        if (photonView.Owner != null)
            TankColor = RoomPanelUI.GetColor(photonView.Owner);
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (r == trackRenderer || r == minimapIcon) continue;
            if (r.GetComponentInParent<Canvas>() != null) continue;
            foreach (Material m in r.materials)
                if (m.HasProperty("_Color")) m.color = TankColor;
        }
        minimapIcon.material.color = TankColor;
        minimapIcon.transform.localScale = Vector3.one * (photonView.IsMine ? 26f : 20f); //내 아이콘을 조금 크게
    }

    void OnDestroy()
    {
        BattleManager.Tanks.Remove(this);
        if (Local == this) Local = null;
    }

    #region 입력 (내 탱크만)
    void Update()
    {
        if (!photonView.IsMine)
        {
            //원격 탱크 : 받은 포탑/포신 각도로 부드럽게 회전
            turret.localRotation = Quaternion.Slerp(turret.localRotation, Quaternion.Euler(0f, netTurretY, 0f), Time.deltaTime * 10f);
            cannon.localRotation = Quaternion.Slerp(cannon.localRotation, Quaternion.Euler(netPitch, 0f, 0f), Time.deltaTime * 10f);
            return;
        }

        h = v = 0f;
        if (!CanControl) return;

        h = Input.GetAxis("Horizontal");
        v = MoveLeft > 0f ? Input.GetAxis("Vertical") : 0f;

        //궤도 텍스처 스크롤 (TankAnim과 같은 방식)
        if (trackRenderer != null && Mathf.Abs(v) > 0.01f)
        {
            trackOffset += Time.deltaTime * 2f * v;
            trackRenderer.material.SetTextureOffset("_MainTex", new Vector2(0f, trackOffset));
            trackRenderer.material.SetTextureOffset("_BumpMap", new Vector2(0f, trackOffset));
        }

        AimTurret();

        //마우스 휠 : 포신 각도
        pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse ScrollWheel") * pitchPerScroll, minPitch, maxPitch);
        cannon.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        //숫자키 : 포탄 선택
        if (Input.GetKeyDown(KeyCode.Alpha1)) SelectShell(ShellType.Basic);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SelectShell(ShellType.Laser);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SelectShell(ShellType.AntiAir);
        if (Input.GetKeyDown(KeyCode.H)) DoHeal();
        if (Input.GetKeyDown(KeyCode.R)) DoReveal();

        HandleFireInput();
    }

    void FixedUpdate()
    {
        if (!photonView.IsMine || !CanControl) return;

        //회전은 자유, 앞뒤 이동만 거리 소모
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, h * rotSpeed * Time.fixedDeltaTime, 0f));
        if (Mathf.Abs(v) > 0.01f)
        {
            Vector3 delta = transform.forward * v * moveSpeed * Time.fixedDeltaTime;
            rb.MovePosition(rb.position + delta);
            MoveLeft = Mathf.Max(0f, MoveLeft - delta.magnitude);
        }
    }

    //TurretCtrl과 같은 방식 : 마우스가 가리키는 지형 쪽으로 포탑 회전
    void AimTurret()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, 1 << 8)) //8번 레이어 = TERRAIN
        {
            Vector3 relative = turret.InverseTransformPoint(hit.point);
            float angle = Mathf.Atan2(relative.x, relative.z) * Mathf.Rad2Deg;
            turret.Rotate(0f, angle * Time.deltaTime * turretRotSpeed, 0f);
        }
    }

    void HandleFireInput()
    {
        switch (SelectedShell)
        {
            case ShellType.Basic:
                //Space를 누르고 있으면 파워 충전, 떼면 발사 (가득 차면 자동 발사)
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    IsCharging = true;
                    Power = 0f;
                }
                if (IsCharging && Input.GetKey(KeyCode.Space))
                {
                    Power = Mathf.Min(1f, Power + chargeSpeed * Time.deltaTime);
                    if (Power >= 1f) FireBasic();
                }
                if (IsCharging && Input.GetKeyUp(KeyCode.Space))
                    FireBasic();
                break;

            case ShellType.Laser:
                if (Input.GetKeyDown(KeyCode.Space)) FireLaser();
                break;

            case ShellType.AntiAir:
                //큰 미니맵을 열고, 클릭한 곳으로 떨어뜨림
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    MinimapController.Instance.OpenBigMap(FireAntiAir);
                    BattleUI.Instance.ShowNotice("지도에서 목표를 클릭하세요 (우클릭 / Esc : 취소)");
                }
                break;
        }
    }
    #endregion

    #region 행동 (UI 버튼에서도 호출)
    public void SelectShell(ShellType type)
    {
        if (!CanControl || IsCharging) return;
        if (type == ShellType.Laser && laserCount <= 0) return;
        if (type == ShellType.AntiAir && antiAirCount <= 0) return;
        SelectedShell = type;
    }

    public void DoHeal()
    {
        if (!CanControl || healCount <= 0 || Health.Hp >= Health.maxHp) return;
        healCount--;
        UseAction();
        Health.Heal(healAmount);
        BattleManager.Instance.AnnounceAction($"{OwnerName} 회복! (+{healAmount})");
        StartCoroutine(EndTurnAfter(1.5f));
    }

    public void DoReveal()
    {
        if (!CanControl) return;
        UseAction();
        //내 다음 턴이 올 때까지 미니맵에 적 위치 표시
        MinimapController.Instance.SetReveal(true);
        BattleManager.Instance.AnnounceAction($"{OwnerName} 정찰! 적 위치를 확인합니다");
        StartCoroutine(EndTurnAfter(1.5f));
    }

    public void SkipTurn()
    {
        if (!CanControl) return;
        UseAction();
        StartCoroutine(EndTurnAfter(0f));
    }

    //제한 시간이 끝났을 때 (BattleManager가 호출)
    public void OnTimeUp()
    {
        if (ActionUsed) return; //이미 쏴서 포탄이 날아가는 중
        IsCharging = false;
        UseAction();
        StartCoroutine(EndTurnAfter(0f));
    }

    public void OnMyTurnStart()
    {
        MoveLeft = maxMoveDistance;
        ActionUsed = false;
        IsCharging = false;
        Power = 0f;
        if ((SelectedShell == ShellType.Laser && laserCount <= 0) || (SelectedShell == ShellType.AntiAir && antiAirCount <= 0))
            SelectedShell = ShellType.Basic;
    }

    void UseAction()
    {
        ActionUsed = true;
        h = v = 0f;
    }

    IEnumerator EndTurnAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        BattleManager.Instance.RequestEndTurn();
    }
    #endregion

    #region 발사
    void FireBasic()
    {
        IsCharging = false;
        float speed = Mathf.Lerp(minShellSpeed, maxShellSpeed, Power);
        SendFire(ShellType.Basic, firePos.position, firePos.forward * speed);
    }

    void FireLaser()
    {
        laserCount--;
        SendFire(ShellType.Laser, firePos.position, firePos.forward * laserSpeed);
    }

    void FireAntiAir(Vector3 target)
    {
        if (!CanControl) return;
        antiAirCount--;
        //목표 바로 위 하늘에서 아래로 떨어뜨림
        SendFire(ShellType.AntiAir, target + Vector3.up * antiAirHeight, Vector3.down * 30f);
    }

    void SendFire(ShellType type, Vector3 pos, Vector3 velocity)
    {
        UseAction();
        //RpcTarget.All : 나를 포함한 모두가 같은 위치/속도로 포탄을 만듦
        photonView.RPC(nameof(RPC_Fire), RpcTarget.All, (byte)type, pos, velocity);
    }

    [PunRPC]
    void RPC_Fire(byte type, Vector3 pos, Vector3 velocity)
    {
        if (fireSfx != null && (ShellType)type != ShellType.AntiAir)
            sfx.PlayOneShot(fireSfx, 1f);
        //내가 쏜 포탄일 때만 authority = true
        BattleManager.Instance.SpawnShell(this, (ShellType)type, pos, velocity, photonView.IsMine);
    }

    //내 포탄이 맞았을 때 (Shell이 호출, 쏜 사람 클라이언트에서만)
    public void OnShellImpact(ShellType type, Vector3 point)
    {
        BattleManager.Instance.ReportImpact(type, point, photonView.OwnerActorNr);
        StartCoroutine(EndTurnAfter(BattleManager.Instance.impactHoldTime));
    }
    #endregion

    //폭발 지점까지의 거리 (탱크 콜라이더 표면 기준)
    public float DistanceTo(Vector3 point)
    {
        float min = Vector3.Distance(transform.position, point);
        foreach (Collider c in GetComponentsInChildren<Collider>())
            min = Mathf.Min(min, Vector3.Distance(c.ClosestPoint(point), point));
        return min;
    }

    public void SetIconVisible(bool visible)
    {
        minimapIcon.enabled = visible;
    }

    //포탑 Y 회전과 포신 각도를 동기화
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(turret.localEulerAngles.y);
            stream.SendNext(pitch);
        }
        else
        {
            netTurretY = (float)stream.ReceiveNext();
            netPitch = (float)stream.ReceiveNext();
        }
    }

    static float NormalizeAngle(float a)
    {
        return a > 180f ? a - 360f : a;
    }
}
