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
using System.Windows.Shapes;

namespace AstroShooter
{
    /// <summary>
    /// Logique d'interaction pour Volume.xaml
    /// </summary>
    public partial class Volume : Window
    {
        public Volume()
        {
            InitializeComponent();
        }

        private void ButOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void ButCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
