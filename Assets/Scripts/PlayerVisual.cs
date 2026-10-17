using UnityEngine;

public class PlayerVisual : MonoBehaviour
{
    public PlayerMovement movement;
    public SpriteRenderer body;

    [Header("Sprite'y kierunków")]
    public Sprite front;
    public Sprite back;
    public Sprite left;
    public Sprite right;

    [Header("Chodzenie (animacja z kodu)")]
    public float bobHeight = 0.05f;
    public float bobSpeed = 16f;
    public float squash = 0.05f;
    public float tiltDegrees = 3f;
    public float settleSpeed = 12f;

    Transform bodyTransform;
    Vector3 basePosition;
    float walkTime;

    void Awake()
    {
        if (movement == null)
        {
            movement = GetComponentInParent<PlayerMovement>();
        }

        if (body == null)
        {
            body = GetComponent<SpriteRenderer>();
        }

        if (body == null)
        {
            body = GetComponentInChildren<SpriteRenderer>();
        }

        bodyTransform = body.transform;
        basePosition = bodyTransform.localPosition;
    }

    void Update()
    {
        body.sprite = SpriteFor(movement.Facing);

        Vector3 targetPosition = basePosition;
        Vector3 targetScale = Vector3.one;
        float targetTilt = 0f;

        if (movement.IsMoving)
        {
            walkTime += Time.deltaTime * bobSpeed;

            float wave = Mathf.Sin(walkTime);
            targetPosition = basePosition + new Vector3(0f, Mathf.Abs(wave) * bobHeight, 0f);
            targetScale = new Vector3(1f + wave * wave * squash, 1f - wave * wave * squash, 1f);

            bool sideways = movement.Facing == Facing.Left || movement.Facing == Facing.Right;
            targetTilt = sideways ? wave * tiltDegrees : 0f;
        }
        else
        {
            walkTime = 0f;
        }

        float blend = 1f - Mathf.Exp(-settleSpeed * Time.deltaTime);

        bodyTransform.localPosition = Vector3.Lerp(bodyTransform.localPosition, targetPosition, blend);
        bodyTransform.localScale = Vector3.Lerp(bodyTransform.localScale, targetScale, blend);

        float currentTilt = Mathf.LerpAngle(bodyTransform.localEulerAngles.z, targetTilt, blend);
        bodyTransform.localRotation = Quaternion.Euler(0f, 0f, currentTilt);
    }

    Sprite SpriteFor(Facing facing)
    {
        switch (facing)
        {
            case Facing.Up: return back;
            case Facing.Left: return left;
            case Facing.Right: return right;
        }

        return front;
    }
}
