using NAudio.Lame;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace AudioInstantReplay
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public MainWindow()
        {
            // Set DataContext and initialize
            DataContext = this;
            InitializeComponent();

            // Init local variables
            outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "AudioInstantReplay");
            Directory.CreateDirectory(outputFolder);
            audioBytes = new CircularBuffer<byte>(30000000);
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

                // Stop the recording
                capture.StopRecording();
            }

            // Start recording
            if (isRecording)
            {
                // Hide/Show buttons
                StartRecVis = false;
                StopRecVis = true;

                // Re-init the WasapiLoopbackCapture
                capture = new WasapiLoopbackCapture();

                // Event Handles
                capture.DataAvailable += (s, a) =>
                {
                    for (int i = 0; i < a.BytesRecorded; i++)
                    {
                        audioBytes.Enqueue(a.Buffer[i]);
                    }
                };

                // Event Handles
                capture.RecordingStopped += (s, a) =>
                {
                    capture.Dispose();
                };

                // Stop the recording
                capture.StartRecording();
            }
        }

        // Save the last minute of audio
        private void Button_Save_Click(object sender, RoutedEventArgs e)
        {
            // Create output filename
            string outFileNameWav = DateTime.Now.ToLongDateString() + "_" + DateTime.Now.ToLongTimeString().Replace(":", ".") + ".wav";
            string outFileNameMp3 = DateTime.Now.ToLongDateString() + "_" + DateTime.Now.ToLongTimeString().Replace(":", ".") + ".mp3";

            // Write to disk
            var wavWriter = new WaveFileWriter(Path.Combine(outputFolder, outFileNameWav), capture.WaveFormat);
            byte[] writeBytes = audioBytes.ToArray();
            wavWriter.Write(writeBytes, 0, writeBytes.Length);
            wavWriter.Dispose();
            wavWriter = null;

            // Compress into mp3
            using (var reader = new AudioFileReader(Path.Combine(outputFolder, outFileNameWav)))
            using (var writer = new LameMP3FileWriter(Path.Combine(outputFolder, outFileNameMp3), reader.WaveFormat, 128))
                reader.CopyTo(writer);

            // Delete original wav file
            File.Delete(Path.Combine(outputFolder, outFileNameWav));

            // Show saved message for 2 seconds
            SavedMsgVis = true;
            Task.Run(() =>
            {
                Thread.Sleep(2000);
                SavedMsgVis = false;
            });
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

        // Property Changed
        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        // Property - Fields
        bool mStartRecVis = true;
        bool mStopRecVis = false;
        bool mSavedMsgVis = false;

        // Local - Fields
        string outputFolder = "";
        WasapiLoopbackCapture capture;
        bool isRecording = false;
        CircularBuffer<byte> audioBytes;
    }
}
