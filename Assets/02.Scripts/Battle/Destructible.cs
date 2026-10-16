using System.Collections.Generic;
using UnityEngine;

//부서지는 엄폐물 (건물 블록, 바위)
//폭발 처리(BattleManager.RPC_Explode)는 모든 클라이언트가 같은 위치로 실행하므로,
//체력 계산도 각자 해도 모두 같은 결과가 나옴 → 따로 동기화할 필요 없음
public class Destructible : MonoBehaviour
{
    public static readonly List<Destructible> All = new List<Destructible>();

    public int hp = 60;
    public float effectScale = 2f;

    Collider col;

    void Awake()
    {
        col = GetComponentInChildren<Collider>(); //바위 모델은 콜라이더가 자식에 있음
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    //폭발 지점까지의 거리 (콜라이더 표면 기준)
    public float DistanceTo(Vector3 point)
    {
        if (col == null) return Vector3.Distance(transform.position, point);
        return Vector3.Distance(col.ClosestPoint(point), point);
    }

    public void TakeDamage(int damage)
    {
        hp -= damage;
        if (hp <= 0) Break();
    }

    void Break()
    {
        BattleManager bm = BattleManager.Instance;
        if (bm != null && bm.explosionPrefab != null)
        {
            GameObject fx = Instantiate(bm.explosionPrefab, col != null ? col.bounds.center : transform.position, Quaternion.identity);
            fx.transform.localScale *= effectScale;
            Destroy(fx, 2f);
        }
        Destroy(gameObject);
    }
}
