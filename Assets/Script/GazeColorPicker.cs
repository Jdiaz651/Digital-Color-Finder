using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GazeColorPicker : MonoBehaviour
{
    public Transform crosshairOrigin;
    public float maxDistance = 10f;

    public OVRHand leftHand;

    public RawImage colorDisplay;
    public TextMeshProUGUI colorValueText;

    void Update()
    {
        if (leftHand != null && leftHand.GetFingerIsPinching(OVRHand.HandFinger.Index))
        {
            Ray ray = new Ray(crosshairOrigin.position, crosshairOrigin.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
            {
                Renderer rend = hit.collider.GetComponent<Renderer>();
                Texture2D tex = rend?.material?.mainTexture as Texture2D;

                if (tex != null)
                {
                    Vector2 pixelUV = hit.textureCoord;
                    pixelUV.x *= tex.width;
                    pixelUV.y *= tex.height;

                    Color color = tex.GetPixel((int)pixelUV.x, (int)pixelUV.y);

                    if (colorDisplay != null)
                        colorDisplay.color = color;

                    if (colorValueText != null)
                        colorValueText.text = $"R: {(int)(color.r * 255)} G: {(int)(color.g * 255)} B: {(int)(color.b * 255)}\n#{ColorUtility.ToHtmlStringRGB(color)}";
                }
            }
        }
    }
}
