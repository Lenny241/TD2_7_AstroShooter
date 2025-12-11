using Microsoft.Windows.Themes;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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
        Random random = new Random();

        //Bullet management
        private List<Rectangle> bullets = new();
        private List<Vector> directions = new();
        private const double bulletSpeed = 400;
        public Rectangle bullet = null!;

        //Map management
        private const int MapSize = 20;
        private const int TileSize = 275;
        private const double MoveSpeed = 400;

        //Player management
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
            AfficheDemarrage();
        }
        public void AfficheDemarrage()
        {
            ScreenContainer.Children.Clear();
            UCMenu UCMenu = new UCMenu();
            ScreenContainer.Children.Add(UCMenu);
            UCMenu.ButStart.Click += StartGame;
            UCMenu.ButParameters.Click += AfficheParameters;
            UCMenu.ButRules.Click += AfficheRules;
        }

        public void AfficheRules(object sender, RoutedEventArgs e)
        {
#if DEBUG
            Console.WriteLine("AffichageRules");
#endif
            UCRules uCRules = new UCRules();
            ScreenContainer.Children.Add(uCRules);

        }

        private void AfficheParameters(object sender, RoutedEventArgs e)
        {
#if DEBUG
            Console.WriteLine("AffichageParametres");
#endif

            UCVolume uCVolume = new UCVolume();
            ScreenContainer.Children.Add(uCVolume);
        }

        private void StartGame(object sender, RoutedEventArgs e)
        {
            //bullet
            GameCanvas.KeyDown += GameCanvas_KeyDown;
            GameCanvas.Focus();

            gameTime.Start();
            CreatePlayer();
            CenterMapOnPlayer();
#if DEBUG
            Console.WriteLine("StartGame");
#endif
            ScreenContainer.Children.Clear();
            GameCanvas.Effect = null;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            GameCanvas.Focus();
            GenerateMap();
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

            // Move bullets
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                Rectangle bullet = bullets[i];
                Vector direction = directions[i];

                double x = Canvas.GetLeft(bullet);
                double y = Canvas.GetTop(bullet);

                Canvas.SetLeft(bullet, x + direction.X * bulletSpeed * deltaTime);
                Canvas.SetTop(bullet, y + direction.Y * bulletSpeed * deltaTime);

                // Remove if off the screen
                if (x < 0 || x > GameCanvas.ActualWidth || y < 0 || y > GameCanvas.ActualHeight)
                {
                    GameCanvas.Children.Remove(bullet);
                    bullets.RemoveAt(i);
                    directions.RemoveAt(i);
#if DEBUG
                    Console.WriteLine("Children removed " + i);

#endif
                }
            }

        }

        private void GenerateMap()
        {
            // Initialisation de mapCanvas si elle n'a pas encore été instanciée
            if (mapCanvas == null)
            {
                mapCanvas = new Canvas();
            }

            // Création du tableau pour stocker les tuiles
            Image[,] tileGrid = new Image[MapSize, MapSize];


            // Chargement de l'image de tuile
            BitmapImage tileImage = new BitmapImage(
                new Uri("pack://application:,,,/asset/ground/classicGroundTile1.png"));


            // Remplissage du tableau avec les tuiles
            for (int row = 0; row < MapSize; row++)
            {
                for (int col = 0; col < MapSize; col++) 
                {
                    // Création d'une tuile
                    Image tile = new Image
                    {
                        Source = tileImage,
                        Width = TileSize,
                        Height = TileSize
                    };

                    // Stocker la tuile dans le tableau
                    tileGrid[row, col] = tile;
                }
            }

            // Ajout des tuiles dans le Canvas après génération du tableau
            for (int row = 0; row < MapSize; row++)
            {
                for (int col = 0; col < MapSize; col++)
                {
                    Image tile = tileGrid[row, col];

                    // Positionner la tuile dans le Canvas
                    Canvas.SetLeft(tile, col * TileSize);
                    Canvas.SetTop(tile, row * TileSize);

                    // Ajouter la tuile au Canvas
                    mapCanvas.Children.Add(tile);
                }
            }

            // Ajouter le Canvas dans la scène de jeu
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
            double centerX = (GameCanvas.ActualWidth - player.Width) / 2;
            double centerY = (GameCanvas.ActualHeight - player.Height) / 2;

            // Position initiale du joueur au centre de la map
            mapOffsetX = centerX - (MapSize * TileSize / 2.0) + (player.Width / 2);
            mapOffsetY = centerY - (MapSize * TileSize / 2.0) + (player.Height / 2);

            UpdatePositions();
        }

        private void UpdatePositions()
        {
            // Le joueur reste toujours au centre du viewport
            double centerX = (GameCanvas.ActualWidth - player.Width) / 2;
            double centerY = (GameCanvas.ActualHeight - player.Height) / 2;

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

        private void GameCanvas_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                double playerCenterX = Canvas.GetLeft(player) + player.Width / 2;
                double playerCenterY = Canvas.GetTop(player) + player.Height / 2;
                Point position = Mouse.GetPosition(GameCanvas);
                double pX = position.X;
                double pY = position.Y;

                Vector direction = new Vector(pX - playerCenterX, pY - playerCenterY);
                direction.Normalize();
                bullet = new Rectangle
                {
                    Width = 10,
                    Height = 4,
                    Fill = Brushes.Black

                };
                Canvas.SetLeft(bullet, playerCenterX);
                Canvas.SetTop(bullet, playerCenterY);
                GameCanvas.Children.Add(bullet);

                //Bullet angle
                double angle = Math.Atan2(direction.Y, direction.X) * 180 / Math.PI;
                bullet.RenderTransform = new RotateTransform(angle);

                bullets.Add(bullet);
                directions.Add(direction);
#if DEBUG
                Console.WriteLine("Space pressed at Mouse X: " + pX + " Mouse Y: " + pY);
                Console.WriteLine("vector X: " + direction.X + " vector Y: " + direction.Y);
                Console.WriteLine("Angle: " + angle);
#endif
            }
        }


    }
}