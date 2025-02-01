using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ImageDownloader : MonoBehaviour
{
    public string imageUrl = ""; // URL or Base64 data of the image
    public string fileName = "DownloadedImage.png"; // Name of the file to save
    private string downloadFolderPath = "Assets/DownloadedImages/";

    // Strengths for map generation
    public float normalMapStrength = 1.0f;
    public float heightMapStrength = 1.0f;
    public float aoBias = 0.5f;
    public int aoSampleCount = 64;

    public async void DownloadImage()
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            Debug.LogError("Image URL is empty. Please provide a valid URL or Base64 data.");
            return;
        }

        imageUrl = imageUrl.Trim();

        if (imageUrl.StartsWith("data:image/"))
        {
            // Handle Base64 image data
            Debug.Log("Detected Base64 image data. Decoding...");
            SaveBase64Image(imageUrl, fileName);
        }
        else if (Uri.IsWellFormedUriString(imageUrl, UriKind.Absolute))
        {
            // Handle regular URL
            Debug.Log($"Detected regular URL. Starting download: {imageUrl}");
            await DownloadImageFromUrl(imageUrl, fileName);
        }
        else
        {
            Debug.LogError("Invalid input: not a valid URL or Base64 image data.");
        }
    }

    private async Task DownloadImageFromUrl(string url, string fileName)
    {
        try
        {
            using (HttpClient client = new HttpClient())
            {
                byte[] imageBytes = await client.GetByteArrayAsync(url);

                if (!Directory.Exists(downloadFolderPath))
                {
                    Directory.CreateDirectory(downloadFolderPath);
                }

                string filePath = Path.Combine(downloadFolderPath, fileName);
                File.WriteAllBytes(filePath, imageBytes);
                Debug.Log($"Image downloaded to: {filePath}");

#if UNITY_EDITOR
                AssetDatabase.ImportAsset(filePath);
#endif

                Texture2D baseTexture = LoadTextureFromFile(filePath);
                if (baseTexture != null)
                {
                    GenerateNormalMap(baseTexture, filePath, normalMapStrength);
                    GenerateHeightMap(baseTexture, filePath, heightMapStrength);
                    GenerateAOMap(baseTexture, filePath, 1.0f, aoBias, aoSampleCount);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to download image from URL. Error: {e.Message}");
        }
    }

    private void SaveBase64Image(string base64Data, string fileName)
    {
        try
        {
            int commaIndex = base64Data.IndexOf(",");
            if (commaIndex >= 0)
            {
                base64Data = base64Data.Substring(commaIndex + 1);
            }

            byte[] imageBytes = Convert.FromBase64String(base64Data);

            if (!Directory.Exists(downloadFolderPath))
            {
                Directory.CreateDirectory(downloadFolderPath);
            }

            string filePath = Path.Combine(downloadFolderPath, fileName);
            File.WriteAllBytes(filePath, imageBytes);
            Debug.Log($"Base64 image saved to: {filePath}");

#if UNITY_EDITOR
            AssetDatabase.ImportAsset(filePath);
#endif

            Texture2D baseTexture = LoadTextureFromFile(filePath);
            if (baseTexture != null)
            {
                GenerateNormalMap(baseTexture, filePath, normalMapStrength);
                GenerateHeightMap(baseTexture, filePath, heightMapStrength);
                GenerateAOMap(baseTexture, filePath, 1.0f, aoBias, aoSampleCount);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save Base64 image. Error: {e.Message}");
        }
    }

    private Texture2D LoadTextureFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"File not found: {filePath}");
            return null;
        }

        byte[] fileData = File.ReadAllBytes(filePath);
        Texture2D texture = new Texture2D(2, 2);
        if (texture.LoadImage(fileData))
        {
            return texture;
        }
        else
        {
            Debug.LogError("Failed to load texture.");
            return null;
        }
    }

    public void GenerateNormalMap(Texture2D inputTexture, string filePath, float strength)
    {
        if (strength == 0) return;

        int width = inputTexture.width;
        int height = inputTexture.height;

        Texture2D normalMap = new Texture2D(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color c00 = inputTexture.GetPixel(x - 1, y - 1);
                Color c10 = inputTexture.GetPixel(x, y - 1);
                Color c20 = inputTexture.GetPixel(x + 1, y - 1);
                Color c01 = inputTexture.GetPixel(x - 1, y);
                Color c21 = inputTexture.GetPixel(x + 1, y);
                Color c02 = inputTexture.GetPixel(x - 1, y + 1);
                Color c12 = inputTexture.GetPixel(x, y + 1);
                Color c22 = inputTexture.GetPixel(x + 1, y + 1);

                float dx = (c20.r + 2 * c21.r + c22.r) - (c00.r + 2 * c01.r + c02.r);
                float dy = (c02.r + 2 * c12.r + c22.r) - (c00.r + 2 * c10.r + c20.r);

                Vector3 normal = new Vector3(dx * strength, dy * strength, 1.0f).normalized * 0.5f + new Vector3(0.5f, 0.5f, 0.5f);
                normalMap.SetPixel(x, y, new Color(normal.x, normal.y, normal.z, 1.0f));
            }
        }

        normalMap.Apply();
        SaveGeneratedMap(normalMap, filePath, "_NormalMap");
    }

    public void GenerateHeightMap(Texture2D inputTexture, string filePath, float strength)
    {
        if (strength == 0) return;

        int width = inputTexture.width;
        int height = inputTexture.height;

        Texture2D heightMap = new Texture2D(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float heightValue = inputTexture.GetPixel(x, y).grayscale * strength;
                heightMap.SetPixel(x, y, new Color(heightValue, heightValue, heightValue));
            }
        }

        heightMap.Apply();
        SaveGeneratedMap(heightMap, filePath, "_HeightMap");
    }

    public void GenerateAOMap(Texture2D inputTexture, string filePath, float sampleRadius, float bias, int sampleCount)
    {
        if (bias == 0) return;

        int width = inputTexture.width;
        int height = inputTexture.height;

        Texture2D aoMap = new Texture2D(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float occlusion = 0.0f;
                for (int i = 0; i < sampleCount; i++)
                {
                    Vector2 sample = UnityEngine.Random.insideUnitCircle * sampleRadius;
                    int sx = Mathf.Clamp(x + (int)sample.x, 0, width - 1);
                    int sy = Mathf.Clamp(y + (int)sample.y, 0, height - 1);

                    occlusion += inputTexture.GetPixel(sx, sy).grayscale;
                }

                occlusion = Mathf.Clamp01(occlusion / sampleCount + bias);
                aoMap.SetPixel(x, y, new Color(occlusion, occlusion, occlusion));
            }
        }

        aoMap.Apply();
        SaveGeneratedMap(aoMap, filePath, "_AOMap");
    }

    private void SaveGeneratedMap(Texture2D map, string filePath, string suffix)
    {
        byte[] bytes = map.EncodeToPNG();
        string mapFilePath = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + suffix + ".png");
        File.WriteAllBytes(mapFilePath, bytes);
#if UNITY_EDITOR
        AssetDatabase.ImportAsset(mapFilePath);
#endif
        Debug.Log($"Generated map saved to: {mapFilePath}");
    }
}
