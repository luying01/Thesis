using UnityEngine;

public class ScreenshotCamera : MonoBehaviour
{
    public Camera captureCamera;
    public int width = 1920;
    public int height = 1080;
    public KeyCode key = KeyCode.F9;

    void Update()
    {
        if (Input.GetKeyDown(key)) Capture();
    }

    [ContextMenu("Capture Now")]
    void Capture()
    {
        RenderTexture rt = new RenderTexture(width, height, 24);
        captureCamera.targetTexture = rt;
        Texture2D img = new Texture2D(width, height, TextureFormat.RGB24, false);
        captureCamera.Render();
        RenderTexture.active = rt;
        img.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        captureCamera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);

        string folder = System.IO.Path.Combine(
    System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop),
    "PulleyShots");
        System.IO.Directory.CreateDirectory(folder);
        string path = System.IO.Path.Combine(folder,
            "shot_" + System.DateTime.Now.ToString("HHmmss") + ".png");
        System.IO.File.WriteAllBytes(path, img.EncodeToPNG());
        Debug.Log("Saved: " + path);
    }
}