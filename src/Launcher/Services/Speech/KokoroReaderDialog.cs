
using Android.App;
using Android.Widget;

namespace LauncherApp.Services.Speech;

public static class KokoroReaderDialog
{
    private static readonly (int Id, string Name)[] Voices =
    [
        (3, "Heart - American female"),
        (2, "Bella - American female"),
        (6, "Nicole - American female"),
        (10, "Skye - American female"),
        (11, "Adam - American male"),
        (14, "Fenrir - American male"),
        (16, "Michael - American male"),
        (21, "Emma - British female"),
        (24, "Daniel - British male"),
        (26, "George - British male"),
        (27, "Lewis - British male")
    ];

    public static void Show(
        Activity activity,
        string? initialText = null)
    {
        // If required model files are missing, offer installation.
        if (!KokoroModelInstaller.IsInstalled(activity))
        {
            if (activity is MainActivity launcher)
                launcher.OpenSpeechSettings(() => Show(activity, initialText));

            return;
        }

        var speech = LauncherSpeech.Instance;
        var saved = SpeechPreferences.Load(activity);

        var layout = new LinearLayout(activity)
        {
            Orientation = Orientation.Vertical
        };

        layout.SetPadding(32, 12, 32, 12);

        // -----------------------------------------
        // TEXT INPUT
        // -----------------------------------------

        var textInput = new EditText(activity)
        {
            Hint = "Enter text to speak...",
            Text = initialText ?? ""
        };

        textInput.SetSingleLine(false);
        textInput.SetMinLines(3);
        textInput.SetMaxLines(6);

        layout.AddView(textInput);

        // -----------------------------------------
        // VOICE SELECTION
        // -----------------------------------------

        layout.AddView(new TextView(activity)
        {
            Text = "Voice"
        });

        var voiceSpinner = new Spinner(activity);

        var adapter = new ArrayAdapter<string>(
            activity,
            Android.Resource.Layout.SimpleSpinnerItem,
            Voices.Select(v => v.Name).ToArray());

        adapter.SetDropDownViewResource(
            Android.Resource.Layout.SimpleSpinnerDropDownItem);

        voiceSpinner.Adapter = adapter;

        // Restore previously selected voice.
        int voiceIndex = Array.FindIndex(
            Voices,
            voice => voice.Id == saved.VoiceId);

        voiceSpinner.SetSelection(
            voiceIndex >= 0 ? voiceIndex : 0);

        layout.AddView(voiceSpinner);

        // -----------------------------------------
        // SPEAKING SPEED
        // -----------------------------------------

        layout.AddView(new TextView(activity)
        {
            Text = "Speaking speed: 0.5x to 2.0x"
        });

        var speedBar = new SeekBar(activity)
        {
            Max = 150
        };

        // Restore previously selected speed.
        speedBar.Progress = Math.Clamp(
            (int)Math.Round(
                (saved.Speed - 0.5f) * 100),
            0,
            150);

        layout.AddView(speedBar);

        // -----------------------------------------
        // VOLUME
        // -----------------------------------------

        layout.AddView(new TextView(activity)
        {
            Text = "Volume"
        });

        var volumeBar = new SeekBar(activity)
        {
            Max = 100
        };

        // Restore previously selected volume.
        volumeBar.Progress = Math.Clamp(
            (int)Math.Round(saved.Volume * 100),
            0,
            100);

        layout.AddView(volumeBar);

        // -----------------------------------------
        // QUEUE SPEECH
        // -----------------------------------------

        var queueButton = new Button(activity)
        {
            Text = "Queue Speech"
        };

        queueButton.Click += (_, _) =>
        {
            string text = textInput.Text ?? "";

            if (string.IsNullOrWhiteSpace(text))
            {
                Toast.MakeText(
                    activity,
                    "Enter some text to speak.",
                    ToastLength.Short)?.Show();

                return;
            }

            int index = voiceSpinner.SelectedItemPosition;

            if (index < 0 || index >= Voices.Length)
                index = 0;

            var options = new SpeechOptions
            {
                VoiceId = Voices[index].Id,
                Speed = 0.5f + speedBar.Progress / 100f,
                Volume = volumeBar.Progress / 100f,
                SilenceScale = 0.2f
            };

            // IMPORTANT:
            // Save the selected voice, speed and volume
            // so they persist after Launcher restarts.
            SpeechPreferences.Save(activity, options);

            // Add speech to the existing background queue.
            speech.Enqueue(
                text,
                options,
                "Launcher reader");

            Toast.MakeText(
                activity,
                "Added to speech queue",
                ToastLength.Short)?.Show();
        };

        layout.AddView(queueButton);

        // -----------------------------------------
        // PLAYBACK CONTROLS
        // -----------------------------------------

        var pauseButton = new Button(activity)
        {
            Text = "Pause"
        };

        pauseButton.Click += (_, _) =>
            speech.Pause();

        var resumeButton = new Button(activity)
        {
            Text = "Resume"
        };

        resumeButton.Click += (_, _) =>
            speech.Resume();

        var skipButton = new Button(activity)
        {
            Text = "Skip Chunk"
        };

        skipButton.Click += (_, _) =>
            speech.Skip();

        var stopButton = new Button(activity)
        {
            Text = "Stop and Clear Queue"
        };

        stopButton.Click += (_, _) =>
            speech.Stop();

        layout.AddView(pauseButton);
        layout.AddView(resumeButton);
        layout.AddView(skipButton);
        layout.AddView(stopButton);

        // -----------------------------------------
        // DISPLAY DIALOG
        // -----------------------------------------

        var scroll = new ScrollView(activity);

        scroll.AddView(layout);

        new AlertDialog.Builder(activity)
            .SetTitle("Kokoro Speech")
            .SetView(scroll)
            .SetNegativeButton(
                "Close",
                (_, _) => { })
            .Show();
    }
}
