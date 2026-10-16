using UnityEngine;

public class CannonCtrl : MonoBehaviour
{
    Transform tr;
    public float rotSpeed = 100.0f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        tr = GetComponent<Transform>();
    }

    // Update is called once per frame
    void Update()
    {
        float angle = -Input.GetAxis("Mouse ScrollWheel") * Time.deltaTime * rotSpeed;
        tr.Rotate(angle, 0, 0);
    }
}
