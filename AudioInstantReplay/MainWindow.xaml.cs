using NAudio.Lame;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Forms;
using Path = System.IO.Path;

// TODO:
// GRAY OUT DURATION CONTROL WHEN RECORDING
// OUTPUT FILE LOCATION VALIDATION NAH JUST REQUIRE BROWSE BUTTON PRESS
// RESIZE THE BUTTON ICONS TO MAKE THEM NOT LOOK WEIRD DOWNSAMPLED

namespace AudioInstantReplay
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public MainWindow()
        {
            // Set DataContext
            DataContext = this;

            // Init local variables and settings
            if (OutputLocation == "BLANK")
            {
                OutputLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "AudioInstantReplay");
                Directory.CreateDirectory(OutputLocation);
            }

            // Init buffers
            speakerBytes = new CircularBuffer<byte>(DurationToBytes(ReplayDuration));
            micBytes = new CircularBuffer<byte>(DurationToBytes(ReplayDuration));

            // Get input devices
            IList<InputDevice> deviceList = new List<InputDevice>();
            for (int n = -1; n < WaveIn.DeviceCount; n++)
            {
                var caps = WaveIn.GetCapabilities(n);
                deviceList.Add(new InputDevice() { Name = caps.ProductName, DeviceId = n });
            }
            deviceList.Insert(0, new InputDevice() { Name = "None", DeviceId = -9999 });
            InputDevices = new CollectionView(deviceList);
            SelectedInputDevice = InputDeviceId;

            // Initialize Component
            InitializeComponent();
        }

        private int DurationToBytes(int durationSeconds)
        {
            return 384000 * durationSeconds;
        }

        private void Button_StartStop_Click(object sender, RoutedEventArgs e)
        {
            // Invert the isRecording bool
            isRecording = !isRecording;

            // Stop the recording
            if (!isRecording)
            {
                // Hide/Show buttons
                StartRecVis = true;
                StopRecVis = false;

                // Set settings colors
                SettingControlLightColor = "#4889c7";
                SettingControlDarkColor = "#4889c7";

                // Set settings tooltip
                DisabledSettingTooltip = null;

                // Stop the recording
                audioCapture.StopRecording();
                if (InputDeviceId != -9999)
                {
                    micCapture.StopRecording();
                }
            }

            // Start recording
            if (isRecording)
            {
                // Hide/Show buttons
                StartRecVis = false;
                StopRecVis = true;

                // Set settings colors
                SettingControlLightColor = "#eeeeee";
                SettingControlDarkColor = "#878787";

                // Set settings tooltip
                DisabledSettingTooltip = "You must stop recording in order to change this setting!";

                // Re-init the WasapiLoopbackCapture and WaveIn
                audioCapture = new WasapiLoopbackCapture();
                if (InputDeviceId != -9999)
                {
                    micCapture = new WaveIn() { DeviceNumber = InputDeviceId };
                    micCapture.WaveFormat = audioCapture.WaveFormat;
                }

                // Event Handles
                audioCapture.DataAvailable += (s, a) =>
                {
                    for (int i = 0; i < a.BytesRecorded; i++)
                    {
                        speakerBytes.Enqueue(a.Buffer[i]);
                    }
                };

                // Event Handles
                audioCapture.RecordingStopped += (s, a) =>
                {
                    audioCapture?.Dispose();
                    audioCapture = null;
                };

                if (InputDeviceId != -9999)
                {
                    // Event Handles
                    micCapture.DataAvailable += (s, a) =>
                    {
                        for (int i = 0; i < a.BytesRecorded; i++)
                        {
                            micBytes.Enqueue(a.Buffer[i]);
                        }
                    };

                    // Event Handles
                    micCapture.RecordingStopped += (s, a) =>
                    {
                        micCapture?.Dispose();
                        micCapture = null;
                    };
                }

                // Start the recording
                audioCapture.StartRecording();
                if (InputDeviceId != -9999)
                {
                    micCapture.StartRecording();
                }
            }
        }

        // Save the last minute of audio
        private void Button_Save_Click(object sender, RoutedEventArgs e)
        {
            // Create output filename
            string outFileNameSpeakerWav = DateTime.Now.ToLongDateString() + "_" + DateTime.Now.ToLongTimeString().Replace(":", ".") + "_Speaker.wav";
            string outFileNameMicWav = DateTime.Now.ToLongDateString() + "_" + DateTime.Now.ToLongTimeString().Replace(":", ".") + "_Mic.wav";
            string outFileNameMuxWav = DateTime.Now.ToLongDateString() + "_" + DateTime.Now.ToLongTimeString().Replace(":", ".") + "_Mux.wav";
            string outFileNameMp3 = DateTime.Now.ToLongDateString() + "_" + DateTime.Now.ToLongTimeString().Replace(":", ".") + ".mp3";

            // Write speaker audio to disk
            var wavWriter = new WaveFileWriter(Path.Combine(OutputLocation, outFileNameSpeakerWav), audioCapture.WaveFormat);
            byte[] writeBytes = speakerBytes.ToArray();
            wavWriter.Write(writeBytes, 0, writeBytes.Length);
            wavWriter.Dispose();
            wavWriter = null;

            // Write mic audio to disk
            if (InputDeviceId != -9999)
            {
                wavWriter = new WaveFileWriter(Path.Combine(OutputLocation, outFileNameMicWav), audioCapture.WaveFormat);
                writeBytes = micBytes.ToArray();
                wavWriter.Write(writeBytes, 0, writeBytes.Length);
                wavWriter.Dispose();
                wavWriter = null;
            }

            // Mux mic and speaker audio
            if (InputDeviceId != -9999)
            {
                using (var reader1 = new AudioFileReader(Path.Combine(OutputLocation, outFileNameSpeakerWav)))
                using (var reader2 = new AudioFileReader(Path.Combine(OutputLocation, outFileNameMicWav)))
                {
                    var mixer = new MixingSampleProvider(new[] { reader1, reader2 });
                    WaveFileWriter.CreateWaveFile16(Path.Combine(OutputLocation, outFileNameMuxWav), mixer);
                }
            }

            // Compress into mp3
            if (InputDeviceId != -9999)
            {
                using (var reader = new AudioFileReader(Path.Combine(OutputLocation, outFileNameMuxWav)))
                using (var writer = new LameMP3FileWriter(Path.Combine(OutputLocation, outFileNameMp3), reader.WaveFormat, 128))
                    reader.CopyTo(writer);
            }
            else
            {
                using (var reader = new AudioFileReader(Path.Combine(OutputLocation, outFileNameSpeakerWav)))
                using (var writer = new LameMP3FileWriter(Path.Combine(OutputLocation, outFileNameMp3), reader.WaveFormat, 128))
                    reader.CopyTo(writer);
            }

            // Delete original wav file
            File.Delete(Path.Combine(OutputLocation, outFileNameSpeakerWav));
            if (InputDeviceId != -9999)
            {
                File.Delete(Path.Combine(OutputLocation, outFileNameMicWav));
                File.Delete(Path.Combine(OutputLocation, outFileNameMuxWav));
            }

            // Show saved message for 2 seconds
            SavedMsgVis = true;
            Task.Run(() =>
            {
                Thread.Sleep(2000);
                SavedMsgVis = false;
            });
        }

        // Show settings grid row
        private void Button_Settings_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindowHeight == 120)
            {
                MainWindowHeight = 190;
                SettingsGridHeight = 70;
            }
            else
            {
                MainWindowHeight = 120;
                SettingsGridHeight = 0;
            }
        }

        // Set output directory
        private void Button_Browse_Click(object sender, RoutedEventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    OutputLocation = dialog.SelectedPath;
                }
            }
        }

        // Close the window
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Environment.Exit(Environment.ExitCode);
        }

        // Start button visibility
        public bool StartRecVis
        {
            get { return mStartRecVis; }
            set 
            { 
                mStartRecVis = value; 
                NotifyPropertyChanged(); 
            }
        }

        // Stop button visibility
        public bool StopRecVis
        {
            get { return mStopRecVis; }
            set
            {
                mStopRecVis = value;
                NotifyPropertyChanged();
            }
        }

        // Saved Message visibility
        public bool SavedMsgVis
        {
            get { return mSavedMsgVis; }
            set
            {
                mSavedMsgVis = value;
                NotifyPropertyChanged();
            }
        }

        // Saved Message visibility
        public int MainWindowHeight
        {
            get { return mMainWindowHeight; }
            set
            {
                mMainWindowHeight = value;
                NotifyPropertyChanged();
            }
        }

        // Settings Grid Height
        public int SettingsGridHeight
        {
            get { return mSettingsGridHeight; }
            set
            {
                mSettingsGridHeight = value;
                NotifyPropertyChanged();
            }
        }

        // Output Location
        public string OutputLocation
        {
            get { return Properties.Settings.Default.OutputLocation; }
            set
            {
                mOutputLocation = value;
                Properties.Settings.Default.OutputLocation = value;
                Properties.Settings.Default.Save();
                NotifyPropertyChanged();
            }
        }

        // Input Device Id
        public int InputDeviceId
        {
            get { return Properties.Settings.Default.InputDeviceId; }
            set
            {
                mInputDeviceId = value;
                Properties.Settings.Default.InputDeviceId = value;
                Properties.Settings.Default.Save();
                NotifyPropertyChanged();
            }
        }

        // Replay Duration
        public int ReplayDuration
        {
            get { return Properties.Settings.Default.ReplayDuration; }
            set
            {
                mReplayDuration = value;
                Properties.Settings.Default.ReplayDuration = value;
                Properties.Settings.Default.Save();

                speakerBytes = new CircularBuffer<byte>(DurationToBytes(value));
                micBytes = new CircularBuffer<byte>(DurationToBytes(value));

                NotifyPropertyChanged();
            }
        }

        // Selected Input Device
        public int SelectedInputDevice
        {
            get { return mSelectedInputDevice; }
            set
            {
                mSelectedInputDevice = value;
                InputDeviceId = value;
                NotifyPropertyChanged();
            }
        }

        // Setting Control Light Color
        public string SettingControlLightColor
        {
            get { return mSettingControlLightColor; }
            set
            {
                mSettingControlLightColor = value;
                NotifyPropertyChanged();
            }
        }

        // Setting Control Dark Color
        public string SettingControlDarkColor
        {
            get { return mSettingControlDarkColor; }
            set
            {
                mSettingControlDarkColor = value;
                NotifyPropertyChanged();
            }
        }

        // Disabled Setting Tooltip
        public string DisabledSettingTooltip
        {
            get { return mDisabledSettingTooltip; }
            set
            {
                mDisabledSettingTooltip = value;
                NotifyPropertyChanged();
            }
        }

        // Property Changed
        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        // Property - Fields
        int mMainWindowHeight = 120;
        int mSettingsGridHeight = 0;
        int mInputDeviceId = -9999;
        int mSelectedInputDevice = -9999;
        int mReplayDuration = 10;
        bool mStartRecVis = true;
        bool mStopRecVis = false;
        bool mSavedMsgVis = false;
        string mOutputLocation = "";
        string mSettingControlLightColor = "#4889c7";
        string mSettingControlDarkColor = "#4889c7";
        string mDisabledSettingTooltip = null;

        // Local - Fields
        WasapiLoopbackCapture audioCapture;
        WaveIn micCapture;
        bool isRecording = false;
        CircularBuffer<byte> speakerBytes;
        CircularBuffer<byte> micBytes;
        public CollectionView InputDevices { get; }
    }
}
