using Microsoft.Win32;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Vlc.DotNet.Core;
using Vlc.DotNet.Core.Interops.Signatures;
using static System.Net.Mime.MediaTypeNames;

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
                pleer.SourceProvider.MediaPlayer.Paused += StateCheck;
                pleer.SourceProvider.MediaPlayer.Playing += StateCheck;
                pleer.SourceProvider.MediaPlayer.Stopped += StateCheck;
                pleer.SourceProvider.MediaPlayer.Opening += StateCheck;

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

            isTimeChanged = true;
            StateCheck();

            ActionDispatcher(() =>
            {
                if (!isSliderDragStarted)
                {
                    long time = pleer.SourceProvider.MediaPlayer.Time;
                    slider.Value = time = pleer.SourceProvider.MediaPlayer.Time;
                    long deltaTime = time - LastTime;
                    if (deltaTime < 0 || deltaTime > 20_000)
                        Properties.Settings.Default.LastTime = LastTime = time + (deltaTime < 0 ? 0 : 10_000);
                }
                isTimeChanged = false;
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

        private void StateCheck(object sender, VlcMediaPlayerPausedEventArgs e) => StateCheck();
        private void StateCheck(object sender, VlcMediaPlayerPlayingEventArgs e) => StateCheck();
        private void StateCheck(object sender, VlcMediaPlayerStoppedEventArgs e) => StateCheck();
        private void StateCheck(object sender, VlcMediaPlayerOpeningEventArgs e) => StateCheck();

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
        private bool isTimeChanged;

        private void SliderDragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
            => isSliderDragStarted = true;

        private void slider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
            => isSliderDragStarted = false;

        private void slider_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            long time = (long)slider.Value;
            ThreadPool.QueueUserWorkItem(_ => pleer.SourceProvider.MediaPlayer.Time = time);
        }

        private void slider_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            isSliderDragStarted = false;
            pleer.SourceProvider.MediaPlayer.SetPause(false);
        }

        private void slider_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
            => isSliderDragStarted = false;

        private void Window_Closed(object sender, EventArgs e)
        {
            pleer.SourceProvider.MediaPlayer.LengthChanged -= MediaPlayer_LengthChanged;
            pleer.SourceProvider.MediaPlayer.TimeChanged -= MediaPlayer_TimeChanged;
            pleer.SourceProvider.MediaPlayer.Paused -= StateCheck;
            pleer.SourceProvider.MediaPlayer.Playing -= StateCheck;
            pleer.SourceProvider.MediaPlayer.Stopped -= StateCheck;
            pleer.SourceProvider.MediaPlayer.Opening -= StateCheck;
            StateCheck();
        }

        private void slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isTimeChanged)
                return;
            isSliderDragStarted = true;
            long time = (long)slider.Value;
            long deltaTime;
            while ((deltaTime = Math.Abs(time - pleer.SourceProvider.MediaPlayer.Time)) > 100)
                Task.Factory.StartNew(() =>
                {
                    pleer.SourceProvider.MediaPlayer.Pause();
                    while (pleer.SourceProvider.MediaPlayer.State != MediaStates.Paused)
                        Thread.Sleep(100);
                    pleer.SourceProvider.MediaPlayer.Time = time;
                    return;
                }).Wait();
        }
    }
}
