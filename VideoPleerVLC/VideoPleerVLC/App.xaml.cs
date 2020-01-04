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
    /// Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            Exit += (s, e) => VideoPleerVLC.Properties.Settings.Default.Save();
            ShutdownMode = ShutdownMode.OnMainWindowClose;
        }
    }
}
