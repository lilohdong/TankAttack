using UnityEngine;

public class TankAnim : MonoBehaviour
{
    float scollSpeed = 2.0f;
    Renderer _renderer;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _renderer = GetComponent<Renderer>();
    }

    // Update is called once per frame
    void Update()
    {
        var offset = Time.time * scollSpeed * Input.GetAxisRaw("Vertical");
        _renderer.material.SetTextureOffset("_MainTex", new Vector2(0, offset)); 
        _renderer.material.SetTextureOffset("_BumpMap", new Vector2(0, offset));
        //_MainTex : Diffuse (물체의 깊이감, 입체감)
        //_BumpMap : Normal Map(평면상 높이값이 있는 것처럼 빛 반사를 바꾸어주는 역할)
    }
}
