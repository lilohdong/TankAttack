using System.Collections;
using UnityEngine;

public class Cannon : MonoBehaviour
{
    public float speed = 6000.0f;
    public GameObject expEffect;
    private CapsuleCollider _collider;
    Rigidbody _rigidbody;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _collider = GetComponent<CapsuleCollider>();
        _rigidbody = GetComponent<Rigidbody>();
        GetComponent<Rigidbody>().AddForce(transform.forward * speed);
        StartCoroutine(this.ExplosionCannon(3.0f));
    }
    private void OnTriggerEnter(Collider other)
    {
        StartCoroutine(this.ExplosionCannon(0.0f));
    }
    IEnumerator ExplosionCannon(float tm)
    {
     
        yield return new WaitForSeconds(tm);
        _collider.enabled = false; // 충돌이 안되도록
        _rigidbody.isKinematic = true;  //물리엔진의 영향을 받지 않음
        //폭발 프리팹 생성
        GameObject obj = (GameObject)Instantiate(expEffect, transform.position, Quaternion.identity);
        Destroy(obj, 1.0f);//폭발효과 1초뒤 사라짐,
        Destroy(this.gameObject, 1.0f); //Trail Renderer가 사라질 때 까지 기다림
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
