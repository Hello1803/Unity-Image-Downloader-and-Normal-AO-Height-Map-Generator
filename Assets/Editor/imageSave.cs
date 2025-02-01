using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ImageDownloader))]
public class ImageDownloaderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ImageDownloader imageDownloader = (ImageDownloader)target;

        if (GUILayout.Button("Download and Generate Maps"))
        {
            imageDownloader.DownloadImage();
        }
    }
}
