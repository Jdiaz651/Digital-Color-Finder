using Meta.XR;
using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.UI;
using PassthroughCameraSamples;

public enum SamplingMode
{
    Environment,
    Manual
}

public enum HarmonyType
{
    SplitComplementary,
    Analogous,
    Triadic
}

public class ColorPicker : MonoBehaviour
{
    [SerializeField] private SamplingMode samplingMode = SamplingMode.Environment;

    [Header("Sampling")]
    [SerializeField] private Transform raySampleOrigin;
    [SerializeField] private Transform manualSamplingOrigin;
    [SerializeField] private LineRenderer lineRenderer;

    [Header("Brightness Correction")]
    [SerializeField, Range(0f, 1f)] private float targetBrightness = 0.8f;
    [SerializeField, Range(0f, 1f)] private float correctionSmoothing = 0.5f;
    [SerializeField] private int roiSize = 3;
    [SerializeField] private float minCorrection = 0.8f;
    [SerializeField] private float maxCorrection = 1.5f;

    [Header("UI")]
    [SerializeField] private RawImage mainColorImage;
    [SerializeField] private RawImage harmonyColor1Image;
    [SerializeField] private RawImage harmonyColor2Image;
    [SerializeField] private TextMeshProUGUI mainColorText;
    [SerializeField] private TextMeshProUGUI harmonyColor1Text;
    [SerializeField] private TextMeshProUGUI harmonyColor2Text;
    [SerializeField] private TextMeshProUGUI harmonyTypeLabel;

    private float _prevCorrectionFactor = 1f;
    private Vector3? _lastHitPoint;
    private Camera _mainCamera;
    private WebCamTexture _webcamTexture;
    private WebCamTextureManager _cameraManager;
    private EnvironmentRaycastManager _raycastManager;

    private Color _mainColor;
    private HarmonyType _currentHarmony = HarmonyType.SplitComplementary;

    private void Start()
    {
        _mainCamera = Camera.main;
        _cameraManager = FindAnyObjectByType<WebCamTextureManager>();
        _raycastManager = GetComponent<EnvironmentRaycastManager>();

        if (!_mainCamera || !_cameraManager || !_raycastManager)
        {
            Debug.LogError("ColorPicker: Missing references.");
            return;
        }

        SetupLineRenderer();
        StartCoroutine(WaitForWebCam());
    }

    private IEnumerator WaitForWebCam()
    {
        while (_cameraManager.WebCamTexture == null || !_cameraManager.WebCamTexture.isPlaying)
            yield return null;

        _webcamTexture = _cameraManager.WebCamTexture;
    }

    private void Update()
    {
        UpdateSamplingPoint();

        if (OVRInput.GetDown(OVRInput.Button.One)) // A button
        {
            PickMainColor();
        }

        if (OVRInput.GetDown(OVRInput.Button.Two)) // B button
        {
            CycleHarmonyType();
            UpdateHarmonyColors();
        }
    }

    private void UpdateSamplingPoint()
    {
        if (samplingMode == SamplingMode.Environment && raySampleOrigin)
        {
            Ray ray = new Ray(raySampleOrigin.position, raySampleOrigin.forward);
            bool hit = _raycastManager.Raycast(ray, out var hitInfo);
            _lastHitPoint = hit ? hitInfo.point : (Vector3?)null;

            lineRenderer.enabled = true;
            lineRenderer.SetPosition(0, ray.origin);
            lineRenderer.SetPosition(1, hit ? hitInfo.point : ray.origin + ray.direction * 5f);
        }
        else if (manualSamplingOrigin)
        {
            _lastHitPoint = manualSamplingOrigin.position;
            lineRenderer.enabled = false;
        }
    }

    private void PickMainColor()
    {
        if (_webcamTexture == null || !_webcamTexture.isPlaying || _lastHitPoint == null)
        {
            Debug.LogWarning("Webcam not ready or invalid sample point.");
            return;
        }

        Vector2 uv = WorldToTextureUV(_lastHitPoint.Value);
        _mainColor = SampleAndCorrectColor(uv);
        UpdateMainColorUI();
        UpdateHarmonyColors();
    }

    private void CycleHarmonyType()
    {
        _currentHarmony = (HarmonyType)(((int)_currentHarmony + 1) % System.Enum.GetValues(typeof(HarmonyType)).Length);
        harmonyTypeLabel.text = _currentHarmony.ToString();
    }

    private void UpdateMainColorUI()
    {
        if (mainColorImage)
            mainColorImage.color = _mainColor;

        if (mainColorText)
            mainColorText.text = $"{ColorToHex(_mainColor)}\nRGB: {ColorToRGB(_mainColor)}";
    }

    private void UpdateHarmonyColors()
    {
        Color[] harmonies = GetHarmonyColors(_mainColor, _currentHarmony);

        if (harmonyColor1Image) harmonyColor1Image.color = harmonies[0];
        if (harmonyColor2Image) harmonyColor2Image.color = harmonies[1];

        if (harmonyColor1Text) harmonyColor1Text.text = $"{ColorToHex(harmonies[0])}\nRGB: {ColorToRGB(harmonies[0])}";
        if (harmonyColor2Text) harmonyColor2Text.text = $"{ColorToHex(harmonies[1])}\nRGB: {ColorToRGB(harmonies[1])}";

        if (harmonyTypeLabel) harmonyTypeLabel.text = _currentHarmony.ToString();
    }

    private Color[] GetHarmonyColors(Color baseColor, HarmonyType type)
    {
        Color.RGBToHSV(baseColor, out float h, out float s, out float v);

        float h1 = 0f, h2 = 0f;

        switch (type)
        {
            case HarmonyType.SplitComplementary:
                h1 = (h + 150f / 360f) % 1f;
                h2 = (h + 210f / 360f) % 1f;
                break;
            case HarmonyType.Analogous:
                h1 = (h + 30f / 360f) % 1f;
                h2 = (h - 30f / 360f + 1f) % 1f;
                break;
            case HarmonyType.Triadic:
                h1 = (h + 120f / 360f) % 1f;
                h2 = (h + 240f / 360f) % 1f;
                break;
        }

        Color color1 = Color.HSVToRGB(h1, s, v);
        Color color2 = Color.HSVToRGB(h2, s, v);

        return new[] { color1, color2 };
    }

    private Vector2 WorldToTextureUV(Vector3 worldPoint)
    {
        var pose = PassthroughCameraUtils.GetCameraPoseInWorld(_cameraManager.Eye);
        var local = Quaternion.Inverse(pose.rotation) * (worldPoint - pose.position);
        var intrinsics = PassthroughCameraUtils.GetCameraIntrinsics(_cameraManager.Eye);

        if (local.z <= 0.0001f)
            return Vector2.zero;

        float u = intrinsics.FocalLength.x * (local.x / local.z) + intrinsics.PrincipalPoint.x;
        float v = intrinsics.FocalLength.y * (local.y / local.z) + intrinsics.PrincipalPoint.y;

        u *= _webcamTexture.width / (float)intrinsics.Resolution.x;
        v *= _webcamTexture.height / (float)intrinsics.Resolution.y;

        return new Vector2(u / _webcamTexture.width, v / _webcamTexture.height);
    }

    private Color SampleAndCorrectColor(Vector2 uv)
    {
        int x = Mathf.Clamp(Mathf.RoundToInt(uv.x * _webcamTexture.width), 0, _webcamTexture.width - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(uv.y * _webcamTexture.height), 0, _webcamTexture.height - 1);

        Color sampled = _webcamTexture.GetPixel(x, y);
        float brightness = CalculateRoiBrightness(x, y);

        float factor = Mathf.Clamp(targetBrightness / Mathf.Max(brightness, 0.001f), minCorrection, maxCorrection);
        _prevCorrectionFactor = Mathf.Lerp(_prevCorrectionFactor, factor, correctionSmoothing);

        Color corrected = (sampled.linear * _prevCorrectionFactor).gamma;
        return new Color(Mathf.Clamp01(corrected.r), Mathf.Clamp01(corrected.g), Mathf.Clamp01(corrected.b), corrected.a);
    }

    private float CalculateRoiBrightness(int x, int y)
    {
        float sum = 0f;
        int count = 0;
        int half = roiSize / 2;

        for (int i = -half; i <= half; i++)
        {
            for (int j = -half; j <= half; j++)
            {
                int xi = x + i, yj = y + j;
                if (xi < 0 || xi >= _webcamTexture.width || yj < 0 || yj >= _webcamTexture.height) continue;

                Color pixel = _webcamTexture.GetPixel(xi, yj).linear;
                sum += 0.2126f * pixel.r + 0.7152f * pixel.g + 0.0722f * pixel.b;
                count++;
            }
        }

        return count > 0 ? sum / count : 0f;
    }

    private void SetupLineRenderer()
    {
        if (!lineRenderer) return;

        lineRenderer.enabled = true;
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = 0.005f;
        lineRenderer.endWidth = 0.005f;
    }

    private string ColorToHex(Color c)
    {
        return $"#{ColorUtility.ToHtmlStringRGB(c)}";
    }

    private string ColorToRGB(Color c)
    {
        return $"({Mathf.RoundToInt(c.r * 255)}, {Mathf.RoundToInt(c.g * 255)}, {Mathf.RoundToInt(c.b * 255)})";
    }
}
