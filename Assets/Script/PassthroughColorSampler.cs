using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PassthroughColorSampler : MonoBehaviour
{
    [Header("Assigned by Inspector")]
    public Transform reticle; // Assign your hand reticle or pointer
    public RawImage wristImage;
    public TextMeshProUGUI wristText;

    private Camera passthroughCam;
    private RenderTexture passthroughRT;
    private Texture2D readableTexture;

    void Start()
    {
        SetupPassthroughCamera();
    }

    void Update()
    {
        SampleColorFromReticle();
    }

    void SetupPassthroughCamera()
    {
        GameObject camObj = new GameObject("PassthroughSamplerCam");
        passthroughCam = camObj.AddComponent<Camera>();

        passthroughCam.clearFlags = CameraClearFlags.SolidColor;
        passthroughCam.backgroundColor = Color.black;
        passthroughCam.cullingMask = LayerMask.GetMask("Passthrough");

        passthroughCam.fieldOfView = 90f; // Match main camera as close as possible
        passthroughCam.depth = -1; // Make sure it doesn't interfere

        // Match position and rotation of the XR rig's center camera
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            camObj.transform.SetPositionAndRotation(mainCam.transform.position, mainCam.transform.rotation);
        }

        // Set up RenderTexture and readable Texture2D
        passthroughRT = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32);
        passthroughRT.Create();

        readableTexture = new Texture2D(passthroughRT.width, passthroughRT.height, TextureFormat.RGBA32, false);
        passthroughCam.targetTexture = passthroughRT;

        // Optional: show the passthrough RT on your wrist UI
        if (wristImage != null)
            wristImage.texture = passthroughRT;
    }

    void SampleColorFromReticle()
    {
        if (passthroughCam == null || reticle == null) return;

        // Project reticle to screen space of passthroughCam
        Vector3 screenPoint = passthroughCam.WorldToScreenPoint(reticle.position);
        screenPoint.y = passthroughRT.height - screenPoint.y;

        // Copy pixels from RenderTexture to readableTexture
        RenderTexture.active = passthroughRT;
        readableTexture.ReadPixels(new Rect(0, 0, passthroughRT.width, passthroughRT.height), 0, 0);
        readableTexture.Apply();
        RenderTexture.active = null;

        Vector2Int pixel = new Vector2Int(Mathf.RoundToInt(screenPoint.x), Mathf.RoundToInt(screenPoint.y));

        if (pixel.x < 0 || pixel.y < 0 || pixel.x >= readableTexture.width || pixel.y >= readableTexture.height)
            return;

        Color color = readableTexture.GetPixel(pixel.x, pixel.y);
        if (wristImage != null) wristImage.color = color;
        if (wristText != null) wristText.text = ColorUtility.ToHtmlStringRGB(color);
    }
}
