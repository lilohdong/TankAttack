using System.Collections;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

//배틀 화면 UI : 턴/시간, 내 체력·이동·파워, 포탄/행동 버튼, 알림, 결과 화면
public class BattleUI : MonoBehaviour
{
    public static BattleUI Instance;

    [Header("상단")]
    public TMP_Text turnText;
    public TMP_Text timerText;
    public TMP_Text noticeText;
    public CanvasGroup noticeGroup;     //알림 글자 + 반투명 배경 (같이 나타나고 사라짐)

    [Header("내 상태 (내 턴일 때만 보이는 조작 패널)")]
    public GameObject controlPanel;
    public Image hpFill;
    public TMP_Text hpText;
    public Image moveFill;
    public Image powerFill;

    [Header("포탄 / 행동 버튼")]
    public Button basicButton;
    public Button laserButton;
    public TMP_Text laserText;
    public Button antiAirButton;
    public TMP_Text antiAirText;
    public Button healButton;
    public TMP_Text healText;
    public Button revealButton;
    public Button endTurnButton;
    public Button leaveButton;

    [Header("결과 화면")]
    public GameObject gameOverPanel;
    public TMP_Text resultText;
    public TMP_Text resultDetailText;
    public Button returnButton;
    public TMP_Text returnWaitText;

    static readonly Color normalColor = new Color(0.934f, 0.559f, 0.559f, 1f);
    static readonly Color selectedColor = new Color(1f, 0.85f, 0.3f, 1f);
    Coroutine noticeRoutine;

    void Awake()
    {
        Instance = this;
        gameOverPanel.SetActive(false);
        noticeText.text = "";
        noticeGroup.alpha = 0f;

        basicButton.onClick.AddListener(() => OnClick(t => t.SelectShell(ShellType.Basic)));
        laserButton.onClick.AddListener(() => OnClick(t => t.SelectShell(ShellType.Laser)));
        antiAirButton.onClick.AddListener(() => OnClick(t => t.SelectShell(ShellType.AntiAir)));
        healButton.onClick.AddListener(() => OnClick(t => t.DoHeal()));
        revealButton.onClick.AddListener(() => OnClick(t => t.DoReveal()));
        endTurnButton.onClick.AddListener(() => OnClick(t => t.SkipTurn()));
        leaveButton.onClick.AddListener(() => BattleManager.Instance.LeaveBattle());
        returnButton.onClick.AddListener(() => BattleManager.Instance.ReturnToRoom());
    }

    //버튼을 누른 뒤 선택을 풀어줌 (안 풀면 Space를 눌렀을 때 버튼이 또 눌림)
    void OnClick(System.Action<TankController> action)
    {
        if (TankController.Local != null) action(TankController.Local);
        EventSystem.current.SetSelectedGameObject(null);
    }

    void Update()
    {
        BattleManager bm = BattleManager.Instance;
        TankController me = TankController.Local;

        //상단 : 누구 턴인지 + 남은 시간
        switch (bm.Phase)
        {
            case TurnPhase.Waiting:
                turnText.text = "다른 플레이어를 기다리는 중...";
                timerText.text = "";
                break;
            case TurnPhase.GameOver:
                turnText.text = "게임 종료";
                timerText.text = "";
                break;
            default:
                TankController cur = bm.FindTank(bm.CurrentActor);
                string name = cur != null ? cur.OwnerName : "?";
                turnText.text = bm.CurrentActor == PhotonNetwork.LocalPlayer.ActorNumber ? "내 턴" : $"{name} 님의 턴";
                timerText.text = bm.Phase == TurnPhase.Playing ? Mathf.CeilToInt(bm.TimeLeft).ToString() : "";
                timerText.color = bm.TimeLeft <= 10f ? new Color(1f, 0.35f, 0.3f) : Color.white;
                break;
        }

        if (me == null) return;

        //내 체력은 항상 표시
        hpFill.fillAmount = (float)me.Health.Hp / me.Health.maxHp;
        hpText.text = $"HP {me.Health.Hp} / {me.Health.maxHp}";

        //조작 패널은 내 턴에 행동하기 전까지만
        bool myControl = bm.IsMyTurn && !me.ActionUsed && me.Health.IsAlive;
        controlPanel.SetActive(myControl);
        if (!myControl) return;

        moveFill.fillAmount = me.MoveLeft / me.maxMoveDistance;
        powerFill.fillAmount = me.Power;

        laserText.text = $"2. 레이저 ({me.laserCount})";
        antiAirText.text = $"3. 대공탄 ({me.antiAirCount})";
        healText.text = $"H. 회복 ({me.healCount})";
        laserButton.interactable = me.laserCount > 0;
        antiAirButton.interactable = me.antiAirCount > 0;
        healButton.interactable = me.healCount > 0 && me.Health.Hp < me.Health.maxHp;

        //선택된 포탄 버튼은 노란색
        SetSelected(basicButton, me.SelectedShell == ShellType.Basic);
        SetSelected(laserButton, me.SelectedShell == ShellType.Laser);
        SetSelected(antiAirButton, me.SelectedShell == ShellType.AntiAir);
    }

    void SetSelected(Button b, bool selected)
    {
        b.image.color = selected ? selectedColor : normalColor;
    }

    public void ShowNotice(string message, float duration = 2f)
    {
        if (noticeRoutine != null) StopCoroutine(noticeRoutine);
        noticeRoutine = StartCoroutine(NoticeRoutine(message, duration));
    }

    IEnumerator NoticeRoutine(string message, float duration)
    {
        noticeText.text = message;
        noticeGroup.alpha = 1f;
        yield return new WaitForSeconds(duration);
        //0.5초 동안 서서히 사라짐
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            noticeGroup.alpha = 1f - t / 0.5f;
            yield return null;
        }
        noticeGroup.alpha = 0f;
    }

    public void ShowGameOver(string result, string detail)
    {
        controlPanel.SetActive(false);
        gameOverPanel.SetActive(true);
        resultText.text = result;
        resultDetailText.text = detail;
        //방장만 "방으로 돌아가기" 버튼, 나머지는 기다림
        returnButton.gameObject.SetActive(PhotonNetwork.IsMasterClient);
        returnWaitText.gameObject.SetActive(!PhotonNetwork.IsMasterClient);
    }
}
