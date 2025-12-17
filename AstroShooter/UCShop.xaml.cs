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
    /// Logique d'interaction pour UCShop.xaml
    /// </summary>
    public partial class UCShop : UserControl
    {

        private BitmapImage bulletSpeedIcon = null!;
        private BitmapImage lifeIcon = null!;
        private BitmapImage speedIcon = null!;
        public UCShop()
        {
            InitializeComponent();
            bulletSpeedIcon = new BitmapImage(new Uri("pack://application:,,,/asset/shop/bulletSpeedIcon.png"));
            lifeIcon = new BitmapImage(new Uri("pack://application:,,,/asset/shop/lifeIcon.png"));
            speedIcon = new BitmapImage(new Uri("pack://application:,,,/asset/shop/speedIcon.png"));
        }


        public EventHandler CloseShopRequested;
        public EventHandler ButLifeRequested;
        public EventHandler ButSpeedRequested;
        public EventHandler ButShootCooldownRequested;

        private void ButClose_Click(object sender, RoutedEventArgs e)
        {
#if DEBUG
            Console.WriteLine("ResumeRequested");
#endif
            CloseShopRequested?.Invoke(this, EventArgs.Empty);

        }

        private void ButSpeed_Click(object sender, RoutedEventArgs e)
        {
#if DEBUG
            Console.WriteLine("ButSpeed clicked");
#endif
            ButSpeedRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ButShootCooldown_Click(object sender, RoutedEventArgs e)
        {
#if DEBUG
            Console.WriteLine("ButShootCooldown clicked");
#endif
            ButShootCooldownRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ButLife_Click(object sender, RoutedEventArgs e)
        {
#if DEBUG
            Console.WriteLine("ButLife clicked");
#endif
            ButLifeRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
