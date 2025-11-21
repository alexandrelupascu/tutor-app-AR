using UnityEngine;
using UnityEngine.Networking;
using System.Threading.Tasks;
using System.Collections;
using System;

/// <summary>
/// Handles sending images to an API and receiving 2D screen position responses
/// </summary>
public class ImageAPIClient : MonoBehaviour
{
    [Header("API Configuration")]
    [SerializeField] private string apiEndpoint = "http://148.230.90.188:8000/analyze-image";
    [SerializeField] private string apiKey = ""; // Optional API key
    [SerializeField] private float requestTimeout = 100f;

    [Header("Image Settings")]
    [SerializeField] private int maxImageWidth = 1920;
    [SerializeField] private int maxImageHeight = 1080;
    [SerializeField] private bool resizeImageBeforeSending = false;
    [SerializeField] private int jpegQuality = 85;

    [Header("Response Format")]
    [SerializeField] private ResponseFormat responseFormat = ResponseFormat.JSON;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;
    [SerializeField] private bool saveRequestImages = false; // Save sent images to disk for debugging
    [SerializeField] private bool bypassSSLValidation = false; // DEVELOPMENT ONLY - Enable for testing

    [SerializeField] private PlayerController playerController;

    public enum ResponseFormat
    {
        JSON,           // {"x": 100, "y": 200}
        JSONNormalized, // {"x": 0.5, "y": 0.5} - normalized 0-1
        Custom          // Implement your own parsing
    }

    private void Awake()
    {
        // DEVELOPMENT ONLY - Bypass SSL certificate validation if enabled
        if (bypassSSLValidation)
        {
            Debug.LogWarning("⚠️ SSL Certificate validation is DISABLED - only use for development/testing!");
            System.Net.ServicePointManager.ServerCertificateValidationCallback =
                (sender, certificate, chain, sslPolicyErrors) => true;
        }
    }

    /// <summary>
    /// Send image to API and get back a 2D screen position
    /// </summary>
    public async Task<Vector2> SendImageForAnalysis(Texture2D image)
    {
        if (image == null)
        {
            Debug.LogError("Cannot send null image to API");
            return Vector2.zero;
        }

        if (string.IsNullOrEmpty(apiEndpoint))
        {
            Debug.LogError("API endpoint not configured!");
            return Vector2.zero;
        }

        if (debugMode)
        {
            Debug.Log($"=== Starting API Request ===");
            Debug.Log($"Image: {image.width}x{image.height}");
            Debug.Log($"Endpoint: {apiEndpoint}");
        }

        try
        {
            // Optional: Resize image to reduce upload size
            Texture2D imageToSend = image;
            if (resizeImageBeforeSending)
            {
                imageToSend = ResizeImage(image, maxImageWidth, maxImageHeight);
            }

            // Optional: Save image for debugging
            if (saveRequestImages)
            {
                SaveImageToDisk(imageToSend, $"api_request_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
            }

            // Convert to JPEG bytes
            byte[] imageBytes = imageToSend.EncodeToJPG(jpegQuality);

            if (debugMode)
                Debug.Log($"Image encoded: {imageBytes.Length} bytes ({imageBytes.Length / 1024}KB)");

            // Send request
            if (debugMode)
                Debug.Log("Calling SendPostRequest...");

            string responseText = await SendPostRequest(imageBytes);

            if (debugMode)
                Debug.Log($"=== API Response Received ===\n{responseText}");

            // Parse response (always use JSON format for this API)
            APIResponse apiResponse = JsonUtility.FromJson<APIResponse>(responseText);

            if (apiResponse == null)
            {
                Debug.LogError("Failed to parse API response as JSON!");
                return Vector2.zero;
            }

            if (debugMode)
            {
                Debug.Log($"Parsed response:");
                Debug.Log($"  Equation: {apiResponse.equation_str}");
                Debug.Log($"  Is Correct: {apiResponse.is_correct}");
                Debug.Log($"  Error Type: {apiResponse.error_type}");
            }

            string hint = "";
            if (apiResponse.annotations != null && apiResponse.annotations.Length > 0)
            {
                hint = apiResponse.annotations[0];

                if (debugMode)
                    Debug.Log($"Hint extracted: {hint}");


            }

            // After getting the hint
            if (!string.IsNullOrEmpty(hint))
            {
                // Call your text-to-speech or audio generation API
                string audioUrl = await GetAudioURLFromHint(hint);

                if (!string.IsNullOrEmpty(audioUrl))
                {
                    AudioManager.Instance.PlayAudioFromURL(audioUrl, () =>
                    {
                        Debug.Log("Hint audio finished playing");
                    });
                }
            }

            // Check if there's an error
            if (apiResponse.is_correct)
            {
                if (debugMode)

                    playerController.PlayDance();

                Debug.Log("✓ Equation is correct - no error to locate");
                return Vector2.zero;
            }

            if (debugMode)
                Debug.Log($"✗ Error detected: {apiResponse.error_type}");

            // Get error location
            Vector2 position = GetErrorLocation(apiResponse);

            if (debugMode)
                Debug.Log($"=== Error Position: {position} ===");

            // Cleanup resized image if we created one
            if (resizeImageBeforeSending && imageToSend != image)
            {
                Destroy(imageToSend);
            }

            return position;
        }
        catch (Exception e)
        {
            Debug.LogError($"=== API ERROR ===");
            Debug.LogError($"Exception Type: {e.GetType().Name}");
            Debug.LogError($"Message: {e.Message}");
            Debug.LogError($"Stack Trace: {e.StackTrace}");
            return Vector2.zero;
        }
    }

    /// <summary>
    /// Call API to convert hint text to audio URL
    /// </summary>
    private async Task<string> GetAudioURLFromHint(string hintText)
    {
        string audioApiEndpoint = "http://148.230.90.188:8000/audioforhint"; // Your audio API endpoint

        // Create request body (adjust based on your API's format)
        string jsonBody = $"{{\"error_message\": \"{hintText}\", \"language\": \"english\"}}";
        byte[] bodyData = System.Text.Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(audioApiEndpoint, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyData);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            await request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseJson = request.downloadHandler.text;

                // Parse response to get audio URL
                // Adjust parsing based on your API's response format
                AudioURLResponse audioResponse = JsonUtility.FromJson<AudioURLResponse>(responseJson);

                if (debugMode)
                    Debug.Log($"Got audio URL: {audioResponse.response}");

                return audioResponse.response;
            }
            else
            {
                Debug.LogError($"Failed to get audio URL: {request.error}");
                return null;
            }
        }
    }

    [Serializable]
    public class AudioURLResponse
    {
        public string response;
    }

    /// <summary>
    /// Send POST request with image data
    /// </summary>
    private async Task<string> SendPostRequest(byte[] imageData)
    {
        if (debugMode)
            Debug.Log($"Sending POST request to: {apiEndpoint}");

        // Create form with image data
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", imageData, "image.jpg", "image/jpeg");

        using (UnityWebRequest request = UnityWebRequest.Post(apiEndpoint, form))
        {
            // Add API key header if configured
            if (!string.IsNullOrEmpty(apiKey))
            {
                request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            }

            // Set timeout
            request.timeout = (int)requestTimeout;

            // Important: For HTTP (not HTTPS), we need to handle certificate handler differently
            request.certificateHandler = new BypassCertificateHandler();

            if (debugMode)
                Debug.Log("Sending request...");

            // Send request
            var operation = request.SendWebRequest();

            // Wait for completion
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (debugMode)
                Debug.Log($"Request complete. Result: {request.result}");

            // Check for errors
            if (request.result != UnityWebRequest.Result.Success)
            {
                string errorMsg = $"API request failed: {request.error}";
                if (debugMode)
                {
                    Debug.LogError($"Full error details:");
                    Debug.LogError($"  URL: {apiEndpoint}");
                    Debug.LogError($"  Error: {request.error}");
                    Debug.LogError($"  Response Code: {request.responseCode}");
                    Debug.LogError($"  Response: {request.downloadHandler?.text}");
                }
                throw new Exception(errorMsg);
            }

            if (debugMode)
                Debug.Log($"Response received: {request.downloadHandler.text.Substring(0, Mathf.Min(200, request.downloadHandler.text.Length))}...");

            return request.downloadHandler.text;
        }
    }

    /// <summary>
    /// Parse API response to get 2D screen position
    /// </summary>
    private Vector2 ParseResponse(string responseText, int imageWidth, int imageHeight)
    {
        try
        {
            switch (responseFormat)
            {
                case ResponseFormat.JSON:
                    return ParseJSONResponse(responseText);

                case ResponseFormat.JSONNormalized:
                    Vector2 normalized = ParseJSONResponse(responseText);
                    // Convert from normalized (0-1) to pixel coordinates
                    return new Vector2(
                        normalized.x * imageWidth,
                        normalized.y * imageHeight
                    );

                case ResponseFormat.Custom:
                    return ParseCustomResponse(responseText, imageWidth, imageHeight);

                default:
                    throw new Exception("Unknown response format");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error parsing API response: {e.Message}");
            return Vector2.zero;
        }
    }

    /// <summary>
    /// Parse JSON response like {"x": 100, "y": 200}
    /// </summary>
    private Vector2 ParseJSONResponse(string json)
    {
        // Parse the full API response
        APIResponse response = JsonUtility.FromJson<APIResponse>(json);

        // Get the error location (middle point between first bbox after "=" and last bbox)
        return GetErrorLocation(response);
    }

    /// <summary>
    /// Get the middle point between the first bbox after "=" and the last bbox
    /// Returns the center point where the error is located
    /// </summary>
    private Vector2 GetErrorLocation(APIResponse response)
    {
        if (response == null || response.datapoints == null || response.datapoints.Length == 0)
        {
            Debug.LogError("Invalid API response - no datapoints");
            return Vector2.zero;
        }

        // Find index of "=" token
        int equalsIndex = -1;
        for (int i = 0; i < response.datapoints.Length; i++)
        {
            if (response.datapoints[i].text == "=")
            {
                equalsIndex = i;
                break;
            }
        }

        if (equalsIndex == -1)
        {
            Debug.LogError("No '=' found in equation");
            return Vector2.zero;
        }

        // Get first bbox after "="
        int firstAfterEquals = equalsIndex + 1;
        if (firstAfterEquals >= response.datapoints.Length)
        {
            Debug.LogError("No tokens after '='");
            return Vector2.zero;
        }

        // Get last bbox
        int lastIndex = response.datapoints.Length - 1;

        Token firstToken = response.datapoints[firstAfterEquals];
        Token lastToken = response.datapoints[lastIndex];

        if (firstToken.bbox == null || firstToken.bbox.Length < 4 ||
            lastToken.bbox == null || lastToken.bbox.Length < 4)
        {
            Debug.LogError("Invalid bbox data");
            return Vector2.zero;
        }

        // First bbox after "=": [x1, y1, x2, y2]
        float x1_first = firstToken.bbox[0];
        float y1_first = firstToken.bbox[1];

        // Last bbox: [x1, y1, x2, y2]
        float x2_last = lastToken.bbox[2];
        float y2_last = lastToken.bbox[3];

        // Calculate middle point
        float middleX = (x1_first + x2_last) / 2f;
        float middleY = (y1_first + y2_last) / 2f;

        if (debugMode)
        {
            Debug.Log($"Error location calculation:");
            Debug.Log($"  First token after '=': '{firstToken.text}' at [{x1_first}, {y1_first}]");
            Debug.Log($"  Last token: '{lastToken.text}' at [{x2_last}, {y2_last}]");
            Debug.Log($"  Middle point: ({middleX}, {middleY})");
        }

        return new Vector2(middleX, middleY);
    }

    /// <summary>
    /// Override this method for custom response parsing
    /// </summary>
    protected virtual Vector2 ParseCustomResponse(string responseText, int imageWidth, int imageHeight)
    {
        Debug.LogWarning("Custom response parsing not implemented! Override ParseCustomResponse()");
        return Vector2.zero;
    }

    /// <summary>
    /// Resize image to fit within max dimensions while maintaining aspect ratio
    /// </summary>
    private Texture2D ResizeImage(Texture2D source, int maxWidth, int maxHeight)
    {
        int newWidth = source.width;
        int newHeight = source.height;

        // Calculate new dimensions maintaining aspect ratio
        if (source.width > maxWidth || source.height > maxHeight)
        {
            float widthRatio = (float)maxWidth / source.width;
            float heightRatio = (float)maxHeight / source.height;
            float ratio = Mathf.Min(widthRatio, heightRatio);

            newWidth = Mathf.RoundToInt(source.width * ratio);
            newHeight = Mathf.RoundToInt(source.height * ratio);
        }

        if (newWidth == source.width && newHeight == source.height)
            return source;

        // Create resized texture
        RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
        rt.filterMode = FilterMode.Bilinear;

        RenderTexture.active = rt;
        Graphics.Blit(source, rt);

        Texture2D result = new Texture2D(newWidth, newHeight, TextureFormat.RGB24, false);
        result.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
        result.Apply();

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        if (debugMode)
            Debug.Log($"Image resized from {source.width}x{source.height} to {newWidth}x{newHeight}");

        return result;
    }

    /// <summary>
    /// Save image to disk for debugging
    /// </summary>
    private void SaveImageToDisk(Texture2D image, string filename)
    {
        byte[] bytes = image.EncodeToJPG(jpegQuality);
        string path = System.IO.Path.Combine(Application.persistentDataPath, filename);
        System.IO.File.WriteAllBytes(path, bytes);

        if (debugMode)
            Debug.Log($"Image saved to: {path}");
    }

    /// <summary>
    /// Test API connection
    /// </summary>
    public async Task<bool> TestConnection()
    {
        if (string.IsNullOrEmpty(apiEndpoint))
        {
            Debug.LogError("API endpoint not configured!");
            return false;
        }

        try
        {
            using (UnityWebRequest request = UnityWebRequest.Get(apiEndpoint))
            {
                request.timeout = 5;
                await request.SendWebRequest();

                bool success = request.result == UnityWebRequest.Result.Success;

                if (debugMode)
                    Debug.Log($"API connection test: {(success ? "SUCCESS" : "FAILED")}");

                return success;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"API connection test failed: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Set API endpoint at runtime
    /// </summary>
    public void SetAPIEndpoint(string endpoint)
    {
        apiEndpoint = endpoint;
    }

    /// <summary>
    /// Set API key at runtime
    /// </summary>
    public void SetAPIKey(string key)
    {
        apiKey = key;
    }
}

/// <summary>
/// API Response structure for equation analysis
/// </summary>
[Serializable]
public class APIResponse
{
    public string equation_str;
    public string error_type;
    public bool is_correct;
    public AnalyzeEquationOBJ analyzeEquationOBJ;
    public int correct_value;
    public Token[] datapoints;
    public string[] annotations; // Changed: now array of strings
}

[Serializable]
public class AnalyzeEquationOBJ
{
    public string equation_str;
    public Token[] tokens;
    public int image_width;
    public int image_height;
}

[Serializable]
public class Token
{
    public string text;
    public float[] bbox; // Changed: now float array [x1, y1, x2, y2]
}

/// <summary>
/// Certificate handler to bypass SSL validation for HTTP requests
/// </summary>
public class BypassCertificateHandler : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData)
    {
        // Accept all certificates (for development/testing only)
        return true;
    }
}