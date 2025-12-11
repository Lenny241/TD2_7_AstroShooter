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
        Random random = new Random();

        private const int MapSize = 20;
        private const int TileSize = 275;
        private const double MoveSpeed = 400; // Pixels par seconde

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
        }

        private void GenerateMap()
        {
            // Initialisation du Canvas
            if (mapCanvas == null)
            {
                mapCanvas = new Canvas();
            }

            // Nettoyer le canvas si on régénère la carte (évite de superposer des cartes)
            mapCanvas.Children.Clear();

            // Initialisation des outils
            Image[,] tileGrid = new Image[MapSize, MapSize];
            Random rnd = new Random();

            // Chargement des images (UNE SEULE FOIS pour la performance)
            BitmapImage tileImage = new BitmapImage(
                new Uri("pack://application:,,,/asset/ground/classicGroundTile1.png"));

            BitmapImage obstacleImage = new BitmapImage(
                new Uri("pack://application:,,,/asset/ground/rock.png"));


            // Création des tuiles de sol
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
                    tileGrid[row, col] = tile;
                }
            }

            // Affichage du sol ET ajout des obstacles
            for (int row = 0; row < MapSize; row++)
            {
                for (int col = 0; col < MapSize; col++)
                {

                    Image tile = tileGrid[row, col];
                    Canvas.SetLeft(tile, col * TileSize);
                    Canvas.SetTop(tile, row * TileSize);
                    Panel.SetZIndex(tile, 0); // Le sol est en bas (Couche 0)
                    mapCanvas.Children.Add(tile);

                    bool estBordure = (row == 1 || col == 1 || row == MapSize - 2 || col == MapSize - 2);


                    bool mettreObstacle = false;

                    if (estBordure)
                    {
                        mettreObstacle = true; // Mur obligatoire
                    }
                    else
                    {
                        // 10% de chance d'avoir un rocher
                        if (rnd.Next(0, 100) < 10)
                        {
                            mettreObstacle = true;
                        }
                    }

                    if (mettreObstacle)
                    {
                        Image obstacle = new Image
                        {
                            Source = obstacleImage,
                            Width = TileSize,
                            Height = TileSize
                        };

                        Canvas.SetLeft(obstacle, col * TileSize);
                        Canvas.SetTop(obstacle, row * TileSize);
                        Panel.SetZIndex(obstacle, 1);

                        mapCanvas.Children.Add(obstacle);
                    }
                }
            }

            // Ajouter le Canvas à la fenêtre s'il n'y est pas déjà
            if (!GameCanvas.Children.Contains(mapCanvas))
            {
                GameCanvas.Children.Add(mapCanvas);
            }
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
    }
}