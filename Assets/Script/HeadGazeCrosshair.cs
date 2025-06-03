using UnityEngine;

public class HeadGazeCrosshair : MonoBehaviour
{
    public Transform crosshairTransform; // Assign the crosshair GameObject here
    public float maxDistance = 10f;

    void Update()
    {
        Camera cam = Camera.main;
        Vector3 origin = cam.transform.position;
        Vector3 direction = cam.transform.forward;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDistance))
        {
            crosshairTransform.position = hit.point;
            crosshairTransform.rotation = Quaternion.LookRotation(hit.normal);
        }
        else
        {
            crosshairTransform.position = origin + direction * maxDistance;
            crosshairTransform.rotation = Quaternion.LookRotation(-cam.transform.forward);
        }
    }
}
