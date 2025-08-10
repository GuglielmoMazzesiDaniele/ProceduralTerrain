using UnityEngine;
using System.Collections;
using System.IO;

/// <summary>
/// Simple MonoBehaviour for capturing screenshots and recording video (frame sequence) during play mode.
/// Press `screenshotKey` to take a single screenshot.
/// Press `videoKey` to start/stop recording frames to a folder; assemble them into a video with external tools (e.g., FFmpeg).
/// </summary>
public class ScreenCaptureUtility : MonoBehaviour
{
    [Header("Key Bindings")]
    [Tooltip("Key to capture a single screenshot")] public KeyCode screenshotKey = KeyCode.K;
    [Tooltip("Key to start/stop video recording")] public KeyCode videoKey = KeyCode.R;

    [Header("Recording Settings")]
    [Tooltip("Frame rate for recording sequence")] public int frameRate = 30;

    private bool isRecording = false;
    private string folderPath;
    private int frameCount = 0;
    private Coroutine recordCoroutine;

    void Update()
    {
        // Screenshot capture
        if (Input.GetKeyDown(screenshotKey))
        {
            string fileName = $"Screenshot_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
            ScreenCapture.CaptureScreenshot(fileName);
            Debug.Log($"Screenshot saved: {fileName}");
        }

        // Toggle recording
        if (Input.GetKeyDown(videoKey))
        {
            if (!isRecording) StartRecording();
            else StopRecording();
        }
    }

    private void StartRecording()
    {
        isRecording = true;
        Time.captureFramerate = frameRate;
        folderPath = Path.Combine(Application.dataPath, "Recordings", $"Video_{System.DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(folderPath);
        frameCount = 0;
        recordCoroutine = StartCoroutine(RecordFrames());
        Debug.Log($"Started recording to: {folderPath}");
    }

    private void StopRecording()
    {
        isRecording = false;
        Time.captureFramerate = 0;
        if (recordCoroutine != null) StopCoroutine(recordCoroutine);
        Debug.Log($"Stopped recording. Total frames: {frameCount}");
    }

    private IEnumerator RecordFrames()
    {
        while (isRecording)
        {
            yield return new WaitForEndOfFrame();

            // Read screen contents
            Texture2D frame = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            frame.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            frame.Apply();

            // Encode to PNG
            byte[] bytes = frame.EncodeToPNG();
            Destroy(frame);

            // Save to disk
            string filePath = Path.Combine(folderPath, $"frame_{frameCount:D04}.png");
            File.WriteAllBytes(filePath, bytes);
            frameCount++;
        }
    }
}