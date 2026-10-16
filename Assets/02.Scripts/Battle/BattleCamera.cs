using UnityEngine;

//배틀 카메라 (Main Camera에 붙임)
//탱크 따라가기 → 발사하면 포탄 따라가기(3인칭) → 맞은 곳에서 잠시 멈춤 → 다음 턴 탱크로
public class BattleCamera : MonoBehaviour
{
    enum Mode { Tank, Shell, Hold }

    [Header("탱크 따라가기")]
    public float tankDistance = 26f;
    public float tankHeight = 13f;
    public float rotationDamping = 3f;
    public float moveDamping = 5f;

    [Header("포탄 따라가기")]
    public float shellDistance = 14f;
    public float shellHeight = 5f;

    [Header("맞은 곳 보기")]
    public float holdDistance = 30f;
    public float holdHeight = 20f;

    Mode mode = Mode.Tank;
    Transform target;
    Vector3 holdPoint;
    float yaw;

    public void FollowTank(Transform tank)
    {
        mode = Mode.Tank;
        target = tank;
    }

    public void FollowShell(Transform shell)
    {
        mode = Mode.Shell;
        target = shell;
    }

    public void HoldAt(Vector3 point)
    {
        mode = Mode.Hold;
        holdPoint = point;
        yaw = transform.eulerAngles.y; //지금 보던 방향 그대로 멈춤
    }

    void LateUpdate()
    {
        switch (mode)
        {
            case Mode.Tank:
                if (target == null) return;
                //탱크 몸체 방향을 따라 천천히 회전 (SmoothFollow와 같은 방식)
                yaw = Mathf.LerpAngle(yaw, target.eulerAngles.y, rotationDamping * Time.deltaTime);
                Vector3 tankPos = target.position - Quaternion.Euler(0, yaw, 0) * Vector3.forward * tankDistance + Vector3.up * tankHeight;
                transform.position = Vector3.Lerp(transform.position, tankPos, moveDamping * Time.deltaTime);
                transform.LookAt(target.position + Vector3.up * 4f);
                break;

            case Mode.Shell:
                if (target == null) return; //포탄이 사라지면 HoldAt이 올 때까지 그 자리
                //포탄이 날아가는 방향의 뒤쪽 위에서 바라봄
                Vector3 flat = target.forward;
                flat.y = 0f;
                if (flat.sqrMagnitude < 0.01f) //수직으로 떨어지는 대공탄 : 카메라가 보던 방향 사용
                {
                    flat = transform.forward;
                    flat.y = 0f;
                }
                flat.Normalize();
                Vector3 shellPos = target.position - flat * shellDistance + Vector3.up * shellHeight;
                transform.position = Vector3.Lerp(transform.position, shellPos, 10f * Time.deltaTime);
                transform.LookAt(target.position);
                yaw = transform.eulerAngles.y;
                break;

            case Mode.Hold:
                Vector3 holdPos = holdPoint - Quaternion.Euler(0, yaw, 0) * Vector3.forward * holdDistance + Vector3.up * holdHeight;
                transform.position = Vector3.Lerp(transform.position, holdPos, 4f * Time.deltaTime);
                transform.LookAt(holdPoint);
                break;
        }
    }
}
