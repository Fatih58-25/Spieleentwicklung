using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class TPSCamera : MonoBehaviour
{
    [Header("References")]
    public Transform target;  

    [Header("Distance & Shoulder Offset")]
    public float distance = 3.5f; 
    public float height = 1.8f;   
    public float cameraXOffset = 0.6f; 

    [Header("Rotation Settings")]
    public float mouseSensitivity = 2.0f;
    public float yMinLimit = -15f; 
    public float yMaxLimit = 65f;  

    [Header("Lock-On Settings")]
    public string enemyTag = "Enemy";     
    public float maxLockOnDistance = 15f; 
    public RectTransform lockOnUI;        
    public float lockOnHeightOffset = 1.2f; 
    public string customLockPointName = "Lock"; 

    private float currentRotationY = 0f;
    private float currentRotationX = 15f;

    private Transform lockedTarget = null;     
    private Transform actualLockPoint = null; 
    private bool isLockedOn = false;

    public bool IsLockedOn => isLockedOn;
    public Transform LockedTarget => lockedTarget;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (target)
        {
            currentRotationY = target.eulerAngles.y;
        }

        if (lockOnUI != null) lockOnUI.gameObject.SetActive(false);
    }

    void Update()
    {
        bool lockPressed = Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;

        if (lockPressed)
        {
            if (isLockedOn) UnlockTarget();
            else FindBestTarget();
        }

        if (isLockedOn)
        {
            if (lockedTarget == null || Vector3.Distance(target.position, lockedTarget.position) > maxLockOnDistance)
            {
                UnlockTarget();
            }
        }
    }

    void LateUpdate()
    {
        if (!target) return;

        if (isLockedOn && lockedTarget != null)
        {
            Vector3 aimPosition = actualLockPoint != null ? actualLockPoint.position : (lockedTarget.position + Vector3.up * lockOnHeightOffset);
            Vector3 dirToEnemy = aimPosition - target.position;
            dirToEnemy.y = 0; 

            if (dirToEnemy != Vector3.zero)
            {
                Quaternion lookRot = Quaternion.LookRotation(dirToEnemy);
                currentRotationY = Mathf.LerpAngle(currentRotationY, lookRot.eulerAngles.y, 15f * Time.deltaTime);
            }

            if (lockOnUI != null)
            {
                Vector3 screenPos = Camera.main.WorldToScreenPoint(aimPosition);
                Vector3 currentVelocity = Vector3.zero;
                lockOnUI.position = Vector3.SmoothDamp(lockOnUI.position, screenPos, ref currentVelocity, 0.02f);
            }
        }
        else
        {
            float mouseX = 0f;
            float mouseY = 0f;

            if (Mouse.current != null)
            {
                Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                mouseX = mouseDelta.x;
                mouseY = mouseDelta.y;
            }

            // Titremeyi önleyen temiz mouse rotasyonu
            currentRotationY += mouseX * mouseSensitivity;
            currentRotationX -= mouseY * mouseSensitivity;
            currentRotationX = Mathf.Clamp(currentRotationX, yMinLimit, yMaxLimit);
        }

        Quaternion rotation = Quaternion.Euler(currentRotationX, currentRotationY, 0);
        Vector3 targetCenterPos = target.position + Vector3.up * height;
        Vector3 localOffset = new Vector3(cameraXOffset, 0.0f, -distance);
        Vector3 position = targetCenterPos + (rotation * localOffset);

        transform.rotation = rotation;
        transform.position = position;
    }

    private void FindBestTarget()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        Transform bestTarget = null;
        Transform bestLockPoint = null;
        float bestScore = Mathf.Infinity; 

        foreach (GameObject enemy in enemies)
        {
            Transform childLockPoint = FindChildLockPoint(enemy.transform);
            Transform aimTransform = (childLockPoint != null) ? childLockPoint : enemy.transform;

            float physicalDist = Vector3.Distance(target.position, aimTransform.position);
            if (physicalDist > maxLockOnDistance) continue;

            Vector3 screenPos = Camera.main.WorldToViewportPoint(aimTransform.position);
            if (screenPos.z < 0) continue;

            if (screenPos.x < 0 || screenPos.x > 1 || screenPos.y < 0 || screenPos.y > 1) continue;

            float distanceScore = physicalDist / maxLockOnDistance;
            float screenScore = Vector2.Distance(new Vector2(0.5f, 0.5f), new Vector2(screenPos.x, screenPos.y)) * 2f;      
            float finalScore = (distanceScore * 0.6f) + (screenScore * 0.4f);

            if (finalScore < bestScore)
            {
                bestScore = finalScore;
                bestTarget = enemy.transform;
                bestLockPoint = childLockPoint; 
            }
        }

        if (bestTarget != null)
        {
            lockedTarget = bestTarget;
            actualLockPoint = bestLockPoint;
            isLockedOn = true;
            if (lockOnUI != null) lockOnUI.gameObject.SetActive(true);
        }
    }

    private Transform FindChildLockPoint(Transform parent)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>())
        {
            if (child != parent && child.name.ToLower().Contains(customLockPointName.ToLower()))
            {
                return child;
            }
        }
        return null;
    }

    private void UnlockTarget()
    {
        lockedTarget = null;
        actualLockPoint = null;
        isLockedOn = false;
        if (lockOnUI != null) lockOnUI.gameObject.SetActive(false);
    }
}