using System.Collections.Generic;
using UnityEngine;

//포탄 종류 (RPC로 보낼 때는 byte로 변환)
public enum ShellType : byte { Basic, Laser, AntiAir }

//포탄 종류별 설정 (BattleManager Inspector에서 수정)
[System.Serializable]
public class ShellSettings
{
    public string displayName = "기본탄";
    public int damage = 35;             //폭발 중심에서의 피해
    public float damageRadius = 10f;    //피해 반경
    public float craterRadius = 8f;     //지형이 파이는 반경 (0이면 지형 파괴 없음)
    public float craterDepth = 3f;      //지형이 파이는 깊이
    public bool useGravity = true;      //false면 직선으로 날아감 (레이저)
    public float lifeTime = 10f;        //이 시간이 지나면 공중에서 폭발
    public float effectScale = 1f;      //폭발 이펙트 크기
    public Color trailColor = Color.white;
}

//날아가는 포탄. 모든 클라이언트가 각자 같은 조건으로 만들어서 날림(RPC_Fire)
//쏜 사람의 포탄(authority)만 "어디에 맞았는지"를 다른 사람에게 알림 → 결과가 모두 같아짐
public class Shell : MonoBehaviour
{
    public static readonly List<Shell> Active = new List<Shell>();

    TankController owner;
    ShellType type;
    bool authority;
    bool exploded;
    float lifeLeft;
    Rigidbody rb;

    public void Init(TankController owner, ShellType type, ShellSettings settings, Vector3 velocity, bool authority)
    {
        this.owner = owner;
        this.type = type;
        this.authority = authority;
        lifeLeft = settings.lifeTime;

        rb = GetComponent<Rigidbody>();
        rb.useGravity = settings.useGravity;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; //빠른 포탄이 지형을 뚫지 않도록
        rb.linearVelocity = velocity;
        transform.rotation = Quaternion.LookRotation(velocity);

        TrailRenderer trail = GetComponentInChildren<TrailRenderer>();
        if (trail != null)
        {
            trail.startColor = settings.trailColor;
            trail.endColor = new Color(settings.trailColor.r, settings.trailColor.g, settings.trailColor.b, 0f);
        }

        //쏜 탱크와는 부딪히지 않게
        if (owner != null)
        {
            Collider myCol = GetComponent<Collider>();
            foreach (Collider c in owner.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(myCol, c);
        }
        Active.Add(this);
    }

    void Update()
    {
        if (exploded) return;

        //포탄 머리가 날아가는 방향을 보게 (카메라도 이 방향을 따라감)
        if (rb.linearVelocity.sqrMagnitude > 1f)
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity);

        lifeLeft -= Time.deltaTime;
        if (lifeLeft <= 0f || transform.position.y < -50f)
            Hit(transform.position);
    }

    void OnCollisionEnter(Collision collision)
    {
        Hit(collision.GetContact(0).point);
    }

    void Hit(Vector3 point)
    {
        if (exploded) return;
        exploded = true;
        rb.isKinematic = true;
        GetComponent<Collider>().enabled = false;

        //쏜 사람의 포탄만 결과를 알림. 폭발 이펙트/지형/피해는 BattleManager.RPC_Explode에서 모두 같이 처리
        if (authority && owner != null)
            owner.OnShellImpact(type, point);

        Destroy(gameObject, 1f); //Trail이 사라질 시간
    }

    //RPC_Explode를 받으면 아직 날고 있는 시각용 포탄도 정리
    public void Remove()
    {
        exploded = true;
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        Active.Remove(this);
    }
}
