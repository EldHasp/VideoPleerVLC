using Microsoft.Win32;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Vlc.DotNet.Core.Interops.Signatures;

namespace VideoPleerVLC
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public readonly Dispatcher UIdispatcher;
        private void ActionDispatcher(Action action)
            => UIdispatcher.BeginInvoke(action, null);
        private string LastFilePlay;
        private long LastTime;

        public MainWindow()
        {
            InitializeComponent();

            UIdispatcher = this.Dispatcher;

            var currentAssembly = Assembly.GetEntryAssembly();
            var currentDirectory = new FileInfo(currentAssembly.Location).DirectoryName;
            // Default installation path of VideoLAN.LibVLC.Windows
            var libDirectory = new DirectoryInfo(Path.Combine(currentDirectory, "libvlc", IntPtr.Size == 4 ? "win-x86" : "win-x64"));

            LastFilePlay = Properties.Settings.Default.LastFilePlay;
            LastTime = Properties.Settings.Default.LastTime;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                pleer.SourceProvider.CreatePlayer(libDirectory/* pass your player parameters here */);
                pleer.SourceProvider.MediaPlayer.LengthChanged += MediaPlayer_LengthChanged;
                pleer.SourceProvider.MediaPlayer.TimeChanged += MediaPlayer_TimeChanged;
                pleer.SourceProvider.MediaPlayer.Paused += (s, e) => StateCheck();
                pleer.SourceProvider.MediaPlayer.Playing += (s, e) => StateCheck();
                pleer.SourceProvider.MediaPlayer.Stopped += (s, e) => StateCheck();
                pleer.SourceProvider.MediaPlayer.Opening += (s, e) => StateCheck();

                if (!string.IsNullOrWhiteSpace(LastFilePlay) && File.Exists(LastFilePlay))
                {
                    pleer.SourceProvider.MediaPlayer.Play(new Uri(LastFilePlay));
                    pleer.SourceProvider.MediaPlayer.Time = LastTime;
                }
                StateCheck();
            });

            //pleer.SourceProvider.CreatePlayer(libDirectory/* pass your player parameters here */);

            //pleer.SourceProvider.MediaPlayer.LengthChanged += MediaPlayer_LengthChanged;
            //pleer.SourceProvider.MediaPlayer.TimeChanged += MediaPlayer_TimeChanged;

        }

        private void MediaPlayer_TimeChanged(object sender, Vlc.DotNet.Core.VlcMediaPlayerTimeChangedEventArgs e)
        {
            if (isSliderDragStarted)
                return;
            StateCheck();

            ActionDispatcher(() =>
            {
                if (isSliderDragStarted)
                    return;
                slider.Value = pleer.SourceProvider.MediaPlayer.Time;
                if (LastTime + 20_000 < pleer.SourceProvider.MediaPlayer.Time)
                    Properties.Settings.Default.LastTime = LastTime = pleer.SourceProvider.MediaPlayer.Time + 10_000;
            });

        }

        private void MediaPlayer_LengthChanged(object sender, Vlc.DotNet.Core.VlcMediaPlayerLengthChangedEventArgs e)
        {
            ActionDispatcher(() => slider.Maximum = pleer.SourceProvider.MediaPlayer.Length);
            StateCheck();
        }

        protected bool isValueChanged = false;

        private void Load_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    pleer.SourceProvider.MediaPlayer.Play(new Uri(openFileDialog.FileName));
                    Properties.Settings.Default.LastFilePlay = LastFilePlay = openFileDialog.FileName;
                });
        }

        private void StateCheck()
        {
            string content = "Старт";
            switch (pleer.SourceProvider.MediaPlayer.State)
            {
                case MediaStates.Playing: content = "Пауза"; break;
                case MediaStates.Paused: content = "Продолжить"; break;
            }
            ActionDispatcher(() => StartPausePlayButton.Content = content);

        }

        private void StartPausePlay_Click(object sender, RoutedEventArgs e)
        {
            if (pleer.SourceProvider.MediaPlayer.State == MediaStates.Playing)
                ThreadPool.QueueUserWorkItem(_ => pleer.SourceProvider.MediaPlayer.Pause());
            else if (pleer.SourceProvider.MediaPlayer.State == MediaStates.Paused)
                ThreadPool.QueueUserWorkItem(_ => pleer.SourceProvider.MediaPlayer.Play());
            else if (!string.IsNullOrWhiteSpace(LastFilePlay) && File.Exists(LastFilePlay))
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    pleer.SourceProvider.MediaPlayer.Play(new Uri(LastFilePlay));
                    pleer.SourceProvider.MediaPlayer.Time = 0;
                });
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                ActionDispatcher(() => slider.Value = 0);
                pleer.SourceProvider.MediaPlayer.Stop();
                pleer.SourceProvider.MediaPlayer.Time = 0;
            });
        }


        private bool isSliderDragStarted = false;

        private void SliderDragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
            => isSliderDragStarted = true;

        private void slider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
            => isSliderDragStarted = false;

        private void slider_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            long time = (long)slider.Value;
            ThreadPool.QueueUserWorkItem(_ => pleer.SourceProvider.MediaPlayer.Time = time);
        }
    }
}
