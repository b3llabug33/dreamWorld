using DreamWorld.Dev;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace DreamWorld.EditorTools
{
    // Records one ClipDirector scene to an MP4 (1920x1080, 30 fps, Unity Recorder) plus a small JPEG
    // sequence (optional, in framesFolder) for making GIFs. Call ClipRecorder.Record("fight", folder) while in Play mode;
    // it starts recording, plays the scene, and stops by itself when the scene ends.
    public static class ClipRecorder
    {
        static RecorderController controller;
        public static bool Busy => controller != null;
        public static string LastClip { get; private set; }

        public static void Record(string scene, string folder, string framesFolder = null)
        {
            if (!EditorApplication.isPlaying || Busy) { Debug.LogWarning("[ClipRecorder] needs Play mode and no other recording"); return; }
            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.SetRecordModeToManual();
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
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1920, OutputHeight = 1080 };
            movie.AudioInputSettings.PreserveAudio = false;
            movie.OutputFile = $"{folder}/dreamWorld_{scene}";
            settings.AddRecorderSettings(movie);

            if (framesFolder != null)
            {
                var images = ScriptableObject.CreateInstance<ImageRecorderSettings>();
                images.name = scene + " frames";
                images.Enabled = true;
                images.OutputFormat = ImageRecorderSettings.ImageRecorderOutputFormat.JPEG;
                images.JpegQuality = 85;
                images.imageInputSettings = new GameViewInputSettings { OutputWidth = 1920, OutputHeight = 1080 };   // recorders must match sizes
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
            Debug.Log("[ClipRecorder] saved " + LastClip);
        }
    }
}
