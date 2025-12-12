using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

namespace AstroShooter
{
    /// <summary>
    /// Logique d'interaction pour UCPauseScreen.xaml
    /// </summary>
    public partial class UCPauseScreen : UserControl
    {
        public UCPauseScreen()
        {
            InitializeComponent();
        }

        private void ButParameters_Click(object sender, RoutedEventArgs e)
        {
            Volume volume = new Volume();
            volume.Owner = Window.GetWindow(this);
            volume.ShowDialog();
            bool? rep = volume.DialogResult;
            if (rep == true)
            {
                double musicVolume = volume.slidVolume.Value;
#if DEBUG
              Console.WriteLine("Volume" + musicVolume);
#endif
                MainWindow.music.Volume = musicVolume / 10;
            }
#if DEBUG
            Console.WriteLine("AffichageVolu");
#endif
        }

        public event EventHandler ResumeRequested;

        private void ButResume_Click(object sender, RoutedEventArgs e)
        {
#if DEBUG
            Console.WriteLine("ResumeClicked");
#endif
            ResumeRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ButRules_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
