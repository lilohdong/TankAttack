using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

//연습장 진행 (scTraining의 TrainingManager 오브젝트)
//BattleManager(trainingMode 체크)가 오프라인 모드로 "내 턴"만 계속 돌려주고,
//여기서는 단계별 안내 + 표적 배치 + 단계 완료 확인만 함 → 배틀 코드를 그대로 재사용
public class TrainingManager : MonoBehaviour
{
    enum Step { Move, Basic, Laser, Reveal, AntiAir, Heal, Done }

    //단계별 안내 문구 (Step 순서와 같음)
    static readonly string[] titles =
    {
        "이동하기",
        "기본탄 쏘기",
        "레이저로 벽 부수기",
        "정찰하기",
        "대공탄으로 공격",
        "회복하기"
    };
    static readonly string[] descriptions =
    {
        "방향키(WASD)로 탱크를 움직여 보세요.\n앞뒤로 움직이면 '이동' 바가 줄어듭니다.\n한 턴에 갈 수 있는 거리는 정해져 있어요.",
        "빨간 표적 탱크를 기본탄으로 맞히세요.\n마우스 : 포탑 회전 / 휠 : 포신 각도\nSpace를 누르고 있으면 파워가 모이고,\n떼면 발사!",
        "2번 키로 레이저를 고르고 Space로 발사!\n레이저는 직선으로 날아가고\n지형을 크게 파냅니다.\n포신을 수평으로 맞춰 회색 벽을 맞히세요.",
        "맵 반대편 산 너머에 표적이 숨어 있어요.\nR 키(또는 정찰 버튼)로 정찰하면\n미니맵에 적 위치가 표시됩니다.",
        "3번 키로 대공탄 선택 후 Space를 누르면\n큰 지도가 열립니다.\n지도에서 빨간 표적 아이콘을 클릭하면\n그 위 하늘에서 포탄이 떨어집니다.",
        "적의 포격을 맞았습니다!\nH 키(또는 회복 버튼)로 회복하세요.\n회복은 한 게임에 2번만 쓸 수 있어요."
    };

    [Header("표적 프리팹")]
    public TrainingTarget targetPrefab;  //탱크 모양 표적 (기본탄, 대공탄 단계)
    public TrainingTarget wallPrefab;    //레이저 단계의 벽

    [Header("표적 체력 (포탄 피해 : 기본탄 35, 레이저 30, 대공탄 20 → 가장자리일수록 약해짐)")]
    public int basicTargetHp = 25;
    public int wallHp = 30;
    public int antiAirTargetHp = 15;

    [Header("표적 위치 (내 탱크 기준, m)")]
    public Vector2 basicTargetOffset = new Vector2(12f, 45f); //x : 오른쪽, y : 앞쪽
    public Vector2 wallOffset = new Vector2(-8f, 25f);

    [Header("기타")]
    public float moveGoal = 8f;          //이 거리만큼 움직이면 1단계 완료
    public int healStepDamage = 40;      //회복 단계에서 받는 피해
    public float nextStepDelay = 1.5f;   //성공 후 다음 단계까지

    [Header("UI")]
    public GameObject guidePanel;
    public TMP_Text stepText;
    public TMP_Text titleText;
    public TMP_Text descText;
    public Button skipButton;

    Step step = Step.Move;
    bool started;
    bool completing;              //성공 → 다음 단계로 넘어가는 중
    TrainingTarget current;       //지금 단계의 표적
    ShellType lastShell;          //마지막으로 터진 포탄 종류
    int prevHp;
    Vector3 origin, forward, right, center;

    void Start()
    {
        guidePanel.SetActive(false);
        skipButton.onClick.AddListener(OnClickSkip);
        BattleManager.Exploded += OnExploded;
    }

    void OnDestroy()
    {
        //static 이벤트라서 씬이 바뀌어도 남아 있음 → 꼭 빼줘야 함
        BattleManager.Exploded -= OnExploded;
    }

    void Update()
    {
        BattleManager bm = BattleManager.Instance;
        TankController me = TankController.Local;

        //첫 턴이 시작되면 연습 시작
        if (!started)
        {
            if (bm.Phase == TurnPhase.Playing && me != null)
                Begin(me);
            return;
        }

        if (bm.Phase == TurnPhase.GameOver || me == null)
        {
            guidePanel.SetActive(false);
            return;
        }

        //연습장에서는 특수 포탄/회복을 다 써도 다시 채워줌
        if (me.laserCount <= 0) me.laserCount = 3;
        if (me.antiAirCount <= 0) me.antiAirCount = 3;
        if (me.healCount <= 0) me.healCount = 2;

        if (!completing)
        {
            switch (step)
            {
                case Step.Move:
                    if (bm.IsMyTurn && me.MoveLeft <= me.maxMoveDistance - moveGoal)
                        Complete();
                    break;
                case Step.Reveal:
                    if (MinimapController.Instance.IsRevealing)
                        Complete();
                    break;
                case Step.Heal:
                    if (me.Health.Hp > prevHp)
                        Complete();
                    break;
            }
        }
        prevHp = me.Health.Hp;
    }

    void Begin(TankController me)
    {
        started = true;

        //표적 위치의 기준 : 시작할 때 내 탱크 위치와 방향
        origin = me.transform.position;
        forward = Flat(me.transform.forward);
        right = Vector3.Cross(Vector3.up, forward);
        Terrain terrain = BattleManager.Instance.terrain;
        center = terrain.transform.position + new Vector3(terrain.terrainData.size.x, 0f, terrain.terrainData.size.z) * 0.5f;

        guidePanel.SetActive(true);
        EnterStep(Step.Move);
    }

    #region 단계
    void EnterStep(Step next)
    {
        step = next;
        completing = false;

        if (step == Step.Done)
        {
            ClearTarget();
            guidePanel.SetActive(false);
            BattleManager.Instance.EndTraining("연습 완료!", "이제 실전에서 다른 플레이어와 대결해 보세요!");
            return;
        }

        int index = (int)step;
        stepText.text = $"연습 {index + 1} / {titles.Length}";
        titleText.text = titles[index];
        descText.text = descriptions[index];

        switch (step)
        {
            case Step.Move:
                ClearTarget();
                break;
            case Step.Basic:
            case Step.Laser:
            case Step.Reveal:
                SpawnTarget(step);
                break;
            case Step.AntiAir:
                //정찰 단계를 건너뛰었어도 표적이 보이도록
                if (current == null) SpawnTarget(step);
                MinimapController.Instance.SetReveal(true);
                break;
            case Step.Heal:
                ClearTarget();
                MinimapController.Instance.SetReveal(false);
                HitPlayer();
                break;
        }
    }

    void Complete()
    {
        completing = true;
        BattleUI.Instance.ShowNotice("성공!", nextStepDelay);
        StartCoroutine(NextStepAfter(nextStepDelay));
    }

    IEnumerator NextStepAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        EnterStep(step + 1);
    }

    void OnClickSkip()
    {
        EventSystem.current.SetSelectedGameObject(null); //Space를 눌렀을 때 버튼이 또 눌리지 않게
        if (completing || step == Step.Done) return;
        StopAllCoroutines();
        EnterStep(step + 1);
    }

    //회복 단계 : 내 탱크 옆에서 폭발 + 피해 (회복할 수 있는 상태로 만듦)
    void HitPlayer()
    {
        TankController me = TankController.Local;
        if (me == null || me.Health.Hp < me.Health.maxHp) return; //이미 다쳐 있으면 그대로
        BattleManager bm = BattleManager.Instance;
        if (bm.explosionPrefab != null)
        {
            GameObject fx = Instantiate(bm.explosionPrefab, me.transform.position + right * 4f, Quaternion.identity);
            fx.transform.localScale *= 1.5f;
            Destroy(fx, 2f);
        }
        me.Health.TakeDamage(Mathf.Min(healStepDamage, me.Health.Hp - 10));
        prevHp = me.Health.Hp;
    }
    #endregion

    #region 표적
    void SpawnTarget(Step forStep)
    {
        ClearTarget();

        TrainingTarget prefab = targetPrefab;
        int hp = basicTargetHp;
        Vector3 pos;
        switch (forStep)
        {
            case Step.Laser:
                prefab = wallPrefab;
                hp = wallHp;
                pos = origin + right * wallOffset.x + forward * wallOffset.y;
                break;
            case Step.Reveal:
            case Step.AntiAir:
                //맵 가운데를 기준으로 내 반대편 (가운데 큰 산에 가려서 안 보임)
                hp = antiAirTargetHp;
                pos = center * 2f - origin;
                break;
            default:
                pos = origin + right * basicTargetOffset.x + forward * basicTargetOffset.y;
                break;
        }

        Terrain terrain = BattleManager.Instance.terrain;
        pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y - 0.5f; //경사에서 뜨지 않게 살짝 묻음
        ClearRocksNear(pos, 10f);

        TrainingTarget target = Instantiate(prefab, pos, Quaternion.LookRotation(Flat(origin - pos)));
        target.Destructible.hp = hp;
        target.Destructible.Broken += d => OnTargetBroken(target);
        current = target;
    }

    //정찰 단계에 세운 표적을 대공탄 단계에서 부수므로, 세운 단계가 아니라 "지금 단계" 기준으로 판단
    void OnTargetBroken(TrainingTarget target)
    {
        if (target != current) return;
        current = null;
        Step forStep = step;

        ShellType need = forStep == Step.Laser ? ShellType.Laser : forStep == Step.Basic ? ShellType.Basic : ShellType.AntiAir;
        if (lastShell == need && forStep != Step.Reveal)
        {
            Complete();
            return;
        }
        //다른 포탄으로 부쉈으면(또는 정찰 전에 부쉈으면) 다시 세움
        string name = BattleManager.Instance.GetSettings(need).displayName;
        string msg = forStep == Step.Reveal ? "먼저 정찰부터 해보세요! 표적을 다시 세웁니다" : $"{name}(으)로 맞혀야 해요! 표적을 다시 세웁니다";
        BattleUI.Instance.ShowNotice(msg, 2.5f);
        StartCoroutine(RespawnAfter(2.5f, forStep));
    }

    IEnumerator RespawnAfter(float delay, Step forStep)
    {
        yield return new WaitForSeconds(delay);
        if (step == forStep && current == null)
            SpawnTarget(forStep);
    }

    void ClearTarget()
    {
        if (current != null)
            Destroy(current.gameObject);
        current = null;
    }

    //표적 자리에 있는 바위는 치움 (표적이 바위에 가려지지 않게)
    void ClearRocksNear(Vector3 pos, float radius)
    {
        foreach (Destructible d in Destructible.All.ToArray())
        {
            if (d.GetComponent<TrainingTarget>() != null) continue;
            Vector3 diff = d.transform.position - pos;
            diff.y = 0f;
            if (diff.magnitude < radius)
                Destroy(d.gameObject);
        }
    }
    #endregion

    void OnExploded(ShellType type, Vector3 point)
    {
        lastShell = type;
    }

    //높이를 뺀 수평 방향 (길이 1)
    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.normalized;
    }
}
