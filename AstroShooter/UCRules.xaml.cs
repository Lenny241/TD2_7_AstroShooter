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
    /// Logique d'interaction pour UCRules.xaml
    /// </summary>
    public partial class UCRules : UserControl
    {
        public UCRules()
        {
            InitializeComponent();
        }

        private void ButOk_Click(object sender, RoutedEventArgs e)
        {
            if (this.Parent is Panel panel)
                panel.Children.Remove(this);

            // Récupérer la fenêtre parente
            if (Window.GetWindow(this) is MainWindow main)
                main.ShowStartScreen();
        }
    }
}
