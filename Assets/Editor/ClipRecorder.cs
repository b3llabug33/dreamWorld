using DreamWorld.Rendering;
using DreamWorld.Dev;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace DreamWorld.EditorTools
{
    // Records one ClipDirector scene to an MP4 (1920x1080, or 1080x1920 with vertical: true for TikTok/Reels,
    // 30 fps, Unity Recorder) plus a lossless PNG sequence (optional, in framesFolder) for the ad/GIF tools.
    // While recording, the PSX image is rendered sharper with lighter dither so it survives video compression.
    // Call ClipRecorder.Record("fight", folder) while in Play mode;
    // it starts recording, plays the scene, and stops by itself when the scene ends.
    public static class ClipRecorder
    {
        static RecorderController controller;
        public static bool Busy => controller != null;
        public static string LastClip { get; private set; }

        public static void Record(string scene, string folder, string framesFolder = null, bool vertical = false)
        {
            if (!EditorApplication.isPlaying || Busy) { Debug.LogWarning("[ClipRecorder] needs Play mode and no other recording"); return; }
            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.SetRecordModeToManual();
            int w = vertical ? 1080 : 1920, h = vertical ? 1920 : 1080;
            PSXCamera.RecordingHeight = vertical ? 640 : 540;
            PSXCamera.RecordingDither = 0.45f;
            settings.FrameRate = 30f;
            settings.FrameRatePlayback = FrameRatePlayback.Constant;
            settings.CapFrameRate = true;

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = scene + " movie";
            movie.Enabled = true;
            movie.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
            };
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = w, OutputHeight = h };
            movie.AudioInputSettings.PreserveAudio = false;
            movie.OutputFile = $"{folder}/dreamWorld_{scene}";
            settings.AddRecorderSettings(movie);

            if (framesFolder != null)
            {
                var images = ScriptableObject.CreateInstance<ImageRecorderSettings>();
                images.name = scene + " frames";
                images.Enabled = true;
                images.OutputFormat = ImageRecorderSettings.ImageRecorderOutputFormat.PNG;   // lossless: JPEG smears the dither
                images.imageInputSettings = new GameViewInputSettings { OutputWidth = w, OutputHeight = h };   // recorders must match sizes
                images.OutputFile = $"{framesFolder}/{scene}/f_" + DefaultWildcard.Frame;
                settings.AddRecorderSettings(images);
            }

            ClipDirector.Play(scene);   // sets the scene up (teleport, camera) before the first recorded frame
            controller = new RecorderController(settings);
            controller.PrepareRecording();
            controller.StartRecording();
            LastClip = movie.OutputFile + ".mp4";
            EditorApplication.update += WaitForEnd;
        }

        static float doneAt = -1f;

        static void WaitForEnd()
        {
            if (controller == null) { EditorApplication.update -= WaitForEnd; return; }
            if (!EditorApplication.isPlaying) { Stop(); return; }
            if (ClipDirector.Running) { doneAt = -1f; return; }
            if (doneAt < 0) doneAt = Time.realtimeSinceStartup;
            if (Time.realtimeSinceStartup - doneAt > 0.5f) Stop();
        }

        public static void Stop()
        {
            EditorApplication.update -= WaitForEnd;
            if (controller != null && controller.IsRecording()) controller.StopRecording();
            controller = null;
            doneAt = -1f;
            PSXCamera.RecordingHeight = 0;
            PSXCamera.RecordingDither = -1f;
            Debug.Log("[ClipRecorder] saved " + LastClip);
        }
    }
}
