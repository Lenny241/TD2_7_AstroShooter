using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace AstroShooter
{
    public partial class MainWindow : Window
    {
        private const int MapSize = 20;
        private const int TileSize = 275;
        private const double MoveSpeed = 400; // Pixels par seconde

        // Taille de la zone visible en tuiles
        private const int ViewportTilesX = 5;
        private const int ViewportTilesY = 3;

        // Dimensions de la fenêtre de jeu en pixels
        private readonly double ViewportWidth = ViewportTilesX * TileSize;
        private readonly double ViewportHeight = ViewportTilesY * TileSize;

        private Canvas mapCanvas = null!;
        private Rectangle player = null!;
        private double mapOffsetX = 0;
        private double mapOffsetY = 0;

        // Système de mouvement fluide basé sur le temps
        private readonly HashSet<Key> pressedKeys = [];
        private readonly Stopwatch gameTime = new();
        private TimeSpan lastFrameTime;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Définir la taille du canvas à 5x3 tuiles
            GameCanvas.Width = ViewportWidth;
            GameCanvas.Height = ViewportHeight;

            GenerateMap();
            CreatePlayer();
            CenterMapOnPlayer();


            // Démarrer la boucle de jeu synchronisée avec le rendu
            gameTime.Start();
            lastFrameTime = gameTime.Elapsed;
            CompositionTarget.Rendering += GameLoop;
        }

        private void GameLoop(object? sender, EventArgs e)
        {
            // Calculer le delta time pour un mouvement indépendant du framerate
            TimeSpan currentTime = gameTime.Elapsed;
            double deltaTime = (currentTime - lastFrameTime).TotalSeconds;
            lastFrameTime = currentTime;

            // Calculer le mouvement basé sur les touches pressées
            double deltaX = 0;
            double deltaY = 0;

            if (pressedKeys.Contains(Key.Z) || pressedKeys.Contains(Key.Up))
                deltaY += 1;
            if (pressedKeys.Contains(Key.S) || pressedKeys.Contains(Key.Down))
                deltaY -= 1;
            if (pressedKeys.Contains(Key.Q) || pressedKeys.Contains(Key.Left))
                deltaX += 1;
            if (pressedKeys.Contains(Key.D) || pressedKeys.Contains(Key.Right))
                deltaX -= 1;

            // Appliquer le mouvement avec delta time
            if (deltaX != 0 || deltaY != 0)
            {
                mapOffsetX += deltaX * MoveSpeed * deltaTime;
                mapOffsetY += deltaY * MoveSpeed * deltaTime;

                UpdatePositions();
            }
        }

        private void GenerateMap()
        {
            // Création du canvas qui contient la map
            mapCanvas = new Canvas
            {
                Width = MapSize * TileSize,
                Height = MapSize * TileSize
            };

            // Chargement de l'image de tuile
            BitmapImage tileImage = new BitmapImage(
                new Uri("pack://application:,,,/asset/classicTile.png"));

            // Génération séquentielle de la grille 20x20
            for (int row = 0; row < MapSize; row++)
            {
                for (int col = 0; col < MapSize; col++)
                {
                    Image tile = new Image
                    {
                        Source = tileImage,
                        Width = TileSize,
                        Height = TileSize
                    };

                    Canvas.SetLeft(tile, col * TileSize);
                    Canvas.SetTop(tile, row * TileSize);
                    mapCanvas.Children.Add(tile);
                }
            }

            GameCanvas.Children.Add(mapCanvas);
        }

        private void CreatePlayer()
        {
            // Création du personnage temporaire (cube rouge)
            player = new Rectangle
            {
                Width = 40,
                Height = 40,
                Fill = Brushes.Red
            };

            // Position fixe au centre de l'écran
            GameCanvas.Children.Add(player);
        }

        private void CenterMapOnPlayer()
        {
            // Calcule l'offset pour centrer la map sur le joueur (basé sur le viewport)
            double centerX = (ViewportWidth - player.Width) / 2;
            double centerY = (ViewportHeight - player.Height) / 2;

            // Position initiale du joueur au centre de la map
            mapOffsetX = centerX - (MapSize * TileSize / 2.0) + (player.Width / 2);
            mapOffsetY = centerY - (MapSize * TileSize / 2.0) + (player.Height / 2);

            UpdatePositions();
        }

        private void UpdatePositions()
        {
            // Le joueur reste toujours au centre du viewport
            double centerX = (ViewportWidth - player.Width) / 2;
            double centerY = (ViewportHeight - player.Height) / 2;

            Canvas.SetLeft(player, centerX);
            Canvas.SetTop(player, centerY);

            // La map se déplace pour créer l'illusion de mouvement
            Canvas.SetLeft(mapCanvas, mapOffsetX);
            Canvas.SetTop(mapCanvas, mapOffsetY);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            pressedKeys.Add(e.Key);
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            pressedKeys.Remove(e.Key);
        }

        protected override void OnClosed(EventArgs e)
        {
            // Arrêter la boucle de jeu proprement
            CompositionTarget.Rendering -= GameLoop;
            gameTime.Stop();
            base.OnClosed(e);
        }
    }
}