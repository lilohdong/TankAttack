using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

//탱크 체력 + 머리 위 이름/체력바
//체력은 탱크 주인만 바꾸고, RPC로 모두에게 알림
public class TankHealth : MonoBehaviourPun
{
    public int maxHp = 100;
    public int Hp { get; private set; }
    public bool IsAlive => Hp > 0;

    [Header("머리 위 UI (World Space Canvas)")]
    public Transform hud;
    public TMP_Text nameText;
    public Image hpFill;

    [Header("파괴")]
    public GameObject deathEffect;

    void Awake()
    {
        Hp = maxHp;
    }

    void Start()
    {
        nameText.text = photonView.Owner != null ? photonView.Owner.NickName : "";
        nameText.color = photonView.IsMine ? new Color(1f, 0.9f, 0.3f) : Color.white; //내 탱크는 노란 이름
        RefreshHud();
    }

    void LateUpdate()
    {
        //체력바가 항상 카메라를 보게 (빌보드)
        if (Camera.main != null)
            hud.rotation = Camera.main.transform.rotation;
    }

    //내 탱크가 맞았을 때 (BattleManager.RPC_Explode에서 내 탱크만 호출)
    public void TakeDamage(int damage)
    {
        if (!photonView.IsMine || !IsAlive) return;
        photonView.RPC(nameof(RPC_SetHp), RpcTarget.All, Mathf.Max(0, Hp - damage));
    }

    public void Heal(int amount)
    {
        if (!photonView.IsMine || !IsAlive) return;
        photonView.RPC(nameof(RPC_SetHp), RpcTarget.All, Mathf.Min(maxHp, Hp + amount));
    }

    [PunRPC]
    void RPC_SetHp(int hp)
    {
        int diff = hp - Hp;
        bool wasAlive = IsAlive;
        Hp = hp;
        RefreshHud();

        if (diff < 0 && BattleUI.Instance != null)
            BattleUI.Instance.ShowNotice($"{nameText.text}  {diff}");

        if (wasAlive && !IsAlive)
            Die();
    }

    void Die()
    {
        //검게 그을린 색으로 바꾸고 폭발
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (r.GetComponentInParent<Canvas>() != null) continue;
            foreach (Material m in r.materials)
                if (m.HasProperty("_Color")) m.color = new Color(0.15f, 0.15f, 0.15f);
        }
        if (deathEffect != null)
        {
            GameObject fx = Instantiate(deathEffect, transform.position + Vector3.up * 3f, Quaternion.identity);
            fx.transform.localScale *= 3f;
            Destroy(fx, 3f);
        }
        hud.gameObject.SetActive(false);

        if (BattleUI.Instance != null)
            BattleUI.Instance.ShowNotice($"{nameText.text} 격파!", 3f);
    }

    void RefreshHud()
    {
        hpFill.fillAmount = (float)Hp / maxHp;
        //체력이 낮을수록 빨간색
        hpFill.color = Color.Lerp(new Color(0.9f, 0.2f, 0.2f), new Color(0.3f, 0.85f, 0.35f), (float)Hp / maxHp);
    }
}
