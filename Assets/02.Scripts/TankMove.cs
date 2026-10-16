using UnityEngine;

public class TankMove : MonoBehaviour
{
    public float moveSpeed = 20.0f;
    public float rotSpeed = 50.0f;
    Rigidbody rbody;
    Transform tr;
    float h, v;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rbody = GetComponent<Rigidbody>();
        tr = GetComponent<Transform>();
        //Rigidbody의 무게중심을 낮게 설정
        rbody.centerOfMass = new Vector3(0.0f, -0.5f, 0.0f);
    }

    // Update is called once per frame
    void Update()
    {
        h = Input.GetAxis("Horizontal"); //방향키 좌우(-1 - +1 값 리턴)
        v = Input.GetAxis("Vertical");
        tr.Rotate(Vector3.up * rotSpeed * h * Time.deltaTime);
        tr.Translate(Vector3.forward * v * moveSpeed * Time.deltaTime);
    }
}
