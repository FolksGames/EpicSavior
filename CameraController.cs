using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Forward/Backward Settings")]
    [SerializeField] private float forwardSpeed = 15f;
    [SerializeField] private float backwardSpeed = 15f;

    [Header("Movement Settings")]
    [SerializeField] private float swipeSpeedHorizontal = 1f;
    [SerializeField] private float swipeSpeedVertical = 1f;
    [SerializeField] private float rotationSpeedHorizontal = 1f;
    [SerializeField] private float rotationSpeedVertical = 1f;
    [SerializeField] private float continuousRotationSpeedHorizontal = 20f;
    [SerializeField] private float continuousRotationSpeedVertical = 20f;
    [SerializeField] private float acceleration = 1f;
    [SerializeField] private float deceleration = 1f;

    [Header("Edge Thresholds")]
    [SerializeField] private float horizontalEdgeThreshold = 30f;
    [SerializeField] private float verticalEdgeThreshold = 45f;

    [Header("Stabilization Settings")]
    [SerializeField] private float stabilizationSpeed = 1f;

    [Header("Hover Settings")]
    [SerializeField] private float hoverSmoothness = 1f;
    [SerializeField] private float hoverAmplitude = 1f;
    [SerializeField] private float hoverFrequency = 1f;

    [Header("Tilt Settings")]
    [SerializeField] private float tiltSpeed = 1f;

    [Header("Direction Change Settings")]
    [SerializeField] private float directionChangeFactor = 0.1f;
    [SerializeField] private float directionChangeSpeed = 5f;

    private Vector2 touchStart;
    private Vector3 touchDelta;
    private float screenWidth;
    private float screenHeight;
    private bool hasGameJustStarted = true;
    private Quaternion targetRotation;

    private float stabilizationStartTime;
    private bool shouldStabilize = false;
    private float stabilizationDelay = 0.5f;

    private Vector3 currentVelocity = Vector3.zero;
    private Vector3 targetVelocity = Vector3.zero;

    [Header("Invert Settings")]
    [SerializeField] private InvertSettings invertSettings;

    private Vector3 storedPosition;
    private bool isUserInteracting = false;
    private float startYOffset;
    private bool continuousDirectionChange = false;

    private CameraGesture previousGesture = CameraGesture.None;
    private float savedRotationY = 0;

    [SerializeField] private float continuousDirectionChangeAngle = 5f;

    [Header("Touch Sensitivity")]
    [SerializeField] private float touchSensitivity = 10f;

    [Header("Zoom Settings")]
    [SerializeField] private float zoomSpeed = 1f;

    [Header("Camera Movement Limits")]
    [SerializeField] private float minY = 10f;
    [SerializeField] private float maxY = 80f;
    [SerializeField] private float minXAngle = 10f;
    [SerializeField] private float maxXAngle = 80f;
    [SerializeField] private float cameraSpeed = 10f;

    [Header("Screen Edge Settings")]
    [SerializeField] private float screenEdgeBorder = 5f;

    [Flags]
    public enum InvertSettings
    {
        None = 0,
        SwipeHorizontal = 1 << 0,
        SwipeVertical = 1 << 1,
        ContinuousRotationHorizontal = 1 << 2,
        ContinuousRotationVertical = 1 << 3,
        Tilt = 1 << 4,
        Move = 1 << 5,
        DirectionChange = 1 << 6
    }

    private enum CameraGesture
    {
        None,
        SingleTouch,
        MultiTouch,
    }

    private void Awake()
    {
        screenWidth = Screen.width;
        screenHeight = Screen.height;
        startYOffset = transform.position.y;
        StartCoroutine(RandomHover());
    }

    private void LateUpdate()
    {
        CameraGesture currentGesture = CameraGesture.None;

        if (Input.touchCount == 0 && !Input.GetMouseButton(0))
        {
            continuousDirectionChange = false;
            return;
        }

        isUserInteracting = Input.touchCount > 0 || Input.GetMouseButton(0);

        if (Input.touchCount == 1)
        {
            currentGesture = CameraGesture.SingleTouch;
            if (previousGesture == currentGesture)
            {
                HandleSingleTouch();
            }
            else
            {
                Touch touch = Input.GetTouch(0);
                touchStart = touch.position;
            }
        }
        else if (Input.touchCount == 2)
        {
            currentGesture = CameraGesture.MultiTouch;
            if (previousGesture == currentGesture)
            {
                HandleMultiTouch();
            }
        }
        else if (hasGameJustStarted)
        {
            hasGameJustStarted = false;
        }
        else if (!shouldStabilize)
        {
            stabilizationStartTime = Time.time;
            targetRotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
            shouldStabilize = true;
        }
        else if (Time.time - stabilizationStartTime > stabilizationDelay)
        {
            storedPosition = transform.position;
            StabilizeCamera();
            transform.position = storedPosition;
        }

        ApplyContinuousDirectionChange();

        previousGesture = currentGesture;
    }

    private void ApplyContinuousDirectionChange()
    {
        if (!continuousDirectionChange) return;

        // Save the current rotation
        Quaternion savedRotation = transform.rotation;

        // Rotate the camera by the desired angle in the same direction as the previous direction change
        transform.Rotate(Vector3.up, savedRotationY > 0 ? continuousDirectionChangeAngle : -continuousDirectionChangeAngle, Space.World);

        // Save the new rotation
        Quaternion newRotation = transform.rotation;

        // Restore the original rotation and apply the new rotation
        transform.rotation = savedRotation;
        transform.rotation = Quaternion.Lerp(transform.rotation, newRotation, Time.deltaTime * directionChangeSpeed);
    }

    private void MoveCamera(Vector3 touchDeltaPosition)
    {
        Vector3 moveDirection = new Vector3(-touchDeltaPosition.x, 0, -touchDeltaPosition.y) * cameraSpeed * Time.deltaTime;
        transform.Translate(moveDirection, Space.World);
        LimitCameraPosition();
    }

    private void MoveCameraUpDown(float deltaY)
    {
        float newYPosition = Mathf.Clamp(transform.position.y + deltaY, minY, maxY);
        transform.position = new Vector3(transform.position.x, newYPosition, transform.position.z);
    }

    private void RotateCameraHorizontal(float deltaX)
    {
        float rotationY = deltaX * rotationSpeedHorizontal * Time.deltaTime;
        transform.Rotate(Vector3.up, rotationY, Space.World);
        continuousDirectionChange = true;
        savedRotationY = rotationY;
    }


    private void RotateCameraVertical(float angleDelta)
    {
        float rotationAmount = angleDelta * rotationSpeedVertical * Time.deltaTime;
        Vector3 currentRotation = transform.rotation.eulerAngles;
        float newYRotation = (currentRotation.y + rotationAmount) % 360;
        transform.rotation = Quaternion.Euler(currentRotation.x, newYRotation, currentRotation.z);
    }

    private void HandleSingleTouch()
    {
        if (Input.GetMouseButton(0))
        {
            Vector3 touchDeltaPosition = Input.mousePosition - (Vector3)touchStart;
            touchStart = Input.mousePosition;

            if (Mathf.Abs(touchDeltaPosition.x) > touchSensitivity)
            {
                RotateCameraHorizontal(touchDeltaPosition.x);
            }

            else if (Mathf.Abs(touchDeltaPosition.y) > touchSensitivity)
            {
                MoveCameraUpDown(touchDeltaPosition.y * cameraSpeed * Time.deltaTime);
            }
            else
            {
                MoveCamera(touchDeltaPosition);
            }
        }
        else
        {
            Touch touch = Input.GetTouch(0);
            Vector3 touchDeltaPosition = touch.position - touchStart;
            touchStart = touch.position;

            if (touch.phase == TouchPhase.Moved)
            {
                if (Mathf.Abs(touchDeltaPosition.x) > touchSensitivity)
                {
                    RotateCamera(touchDeltaPosition.x);
                }
                else if (Mathf.Abs(touchDeltaPosition.y) > touchSensitivity)
                {
                    MoveCameraUpDown(touchDeltaPosition.y * cameraSpeed * Time.deltaTime);
                }
                else
                {
                    MoveCamera(touchDeltaPosition);
                }
            }
        }
    }

    private void HandleMultiTouch()
    {
        Touch touch1 = Input.GetTouch(0);
        Touch touch2 = Input.GetTouch(1);

        if (touch1.phase == TouchPhase.Moved || touch2.phase == TouchPhase.Moved)
        {
            Vector2 touch1PrevPos = touch1.position - touch1.deltaPosition;
            Vector2 touch2PrevPos = touch2.position - touch2.deltaPosition;

            float prevTouchDeltaMagnitude = (touch1PrevPos - touch2PrevPos).magnitude;
            float touchDeltaMagnitude = (touch1.position - touch2.position).magnitude;

            float deltaMagnitudeDiff = prevTouchDeltaMagnitude - touchDeltaMagnitude;

            ZoomCamera(deltaMagnitudeDiff);
        }
    }

    private void ZoomCamera(float deltaMagnitudeDiff)
    {
        float zoomAmount = deltaMagnitudeDiff * zoomSpeed * Time.deltaTime;
        float newYPosition = Mathf.Clamp(transform.position.y + zoomAmount, minY, maxY);
        transform.position = new Vector3(transform.position.x, newYPosition, transform.position.z);
    }

    private bool IsNearHorizontalScreenEdge(Vector3 touchPosition)
    {
        return touchPosition.x <= screenEdgeBorder || touchPosition.x >= Screen.width - screenEdgeBorder;
    }

    private bool IsNearVerticalScreenEdge(Vector3 touchPosition)
    {
        return touchPosition.y <= screenEdgeBorder || touchPosition.y >= Screen.height - screenEdgeBorder;
    }

    private void RotateCamera(float angleDelta)
    {
        float rotationAmount = angleDelta * rotationSpeedHorizontal * Time.deltaTime;
        Vector3 currentRotation = transform.rotation.eulerAngles;
        float newYRotation = (currentRotation.y + rotationAmount) % 360;
        transform.rotation = Quaternion.Euler(currentRotation.x, newYRotation, currentRotation.z);
    }

    private void TiltCamera(float angleDelta)
    {
        float tiltAmount = angleDelta * tiltSpeed * Time.deltaTime;
        Vector3 currentRotation = transform.rotation.eulerAngles;
        float newXTilt = Mathf.Clamp(currentRotation.x - tiltAmount, minXAngle, maxXAngle);
        transform.rotation = Quaternion.Euler(newXTilt, currentRotation.y, currentRotation.z);
    }

    private void StabilizeCamera()
    {
        Quaternion target = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * stabilizationSpeed);
    }

    private void LimitCameraPosition()
    {
        // Füge hier die gewünschten Einschränkungen für die Kameraposition hinzu, z. B.:
        float clampedX = Mathf.Clamp(transform.position.x, -100, 100);
        float clampedZ = Mathf.Clamp(transform.position.z, -100, 100);
        transform.position = new Vector3(clampedX, transform.position.y, clampedZ);
    }

    private IEnumerator RandomHover()
    {
        while (true)
        {
            float hoverChange = hoverAmplitude * Mathf.Sin(Time.time * hoverFrequency);
            Vector3 targetPosition = new Vector3(transform.position.x, startYOffset + hoverChange, transform.position.z);
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * hoverSmoothness);
            yield return null;
        }
    }
}