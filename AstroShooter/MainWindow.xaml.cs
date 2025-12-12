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

        //musique
        public static MediaPlayer music;

        //Bullet management
        private List<Rectangle> bullets = new();
        private List<Vector> directions = new();
        private const double bulletSpeed = 400;
        public Rectangle bullet = null!;

        //Map management
        private const int MapSize = 26;
        private const int TileSize = 275;
        private const double MoveSpeed = 600;
        private List<Rect> obstacleHitboxes = new();

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
            DemarrageMusique();
        }
        public void AfficheDemarrage()
        {
            ScreenContainer.Children.Clear();
            UCMenu UCMenu = new UCMenu();
            ScreenContainer.Children.Add(UCMenu);
            UCMenu.ButStart.Click += StartGame;
            UCMenu.ButRules.Click += AfficheRules;
        }

        

        public void DemarrageMusique()
        {
#if DEBUG
            Console.WriteLine("Lancement musique");
#endif
            music=new MediaPlayer();
            music.Open(new Uri(AppDomain.CurrentDomain.BaseDirectory+"asset/sons/music.mp3"));
            music.Volume = 0.5; // Volume initial
            music.MediaEnded += RelanceMusique;
            music.Play();
        }

        private void RelanceMusique(object? sender, EventArgs e)
        {
            music.Position = TimeSpan.Zero;
            music.Play();
        }
        public void AfficheRules(object sender, RoutedEventArgs e)
        {
#if DEBUG
            Console.WriteLine("AffichageRules");
#endif
            UCRules uCRules = new UCRules();
            ScreenContainer.Children.Add(uCRules);

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
                // Position actuelle du joueur sur l'écran (fixe)
                double playerScreenX = Canvas.GetLeft(player);
                double playerScreenY = Canvas.GetTop(player);


                // TEST AXE X 
                double proposedMapOffsetX = mapOffsetX + (deltaX * MoveSpeed * deltaTime);

                // Calcul de la position du joueur DANS LE MONDE
                // Formule : PositionJoueurMonde = PositionJoueurEcran - PositionCarteEcran
                double playerWorldX_Future = playerScreenX - proposedMapOffsetX;
                double playerWorldY_Current = playerScreenY - mapOffsetY;

                Rect playerRectX = new Rect(playerWorldX_Future, playerWorldY_Current, player.Width, player.Height);

                if (!CheckCollision(playerRectX))
                {
                    mapOffsetX = proposedMapOffsetX; // Pas de collision, on valide le mouvement X
                }


                // TEST AXE Y
                double proposedMapOffsetY = mapOffsetY + (deltaY * MoveSpeed * deltaTime);

                // On recalcule avec la potentielle nouvelle position X validée juste avant
                double playerWorldX_Current = playerScreenX - mapOffsetX;
                double playerWorldY_Future = playerScreenY - proposedMapOffsetY;

                Rect playerRectY = new Rect(playerWorldX_Current, playerWorldY_Future, player.Width, player.Height);

                if (!CheckCollision(playerRectY))
                {
                    mapOffsetY = proposedMapOffsetY; // Pas de collision, on valide le mouvement Y
                }

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
                        Width = TileSize +1,
                        Height = TileSize +1
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

                    bool estBordure = (row == 0 || col == 0 || row == 1 || col == 1 || row == 2 || col == 2 ||
                                       row == MapSize - 1 || col == MapSize - 1 || row == MapSize - 2 || col == MapSize - 2 || row == MapSize - 3 || col == MapSize - 3);


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
                            // On garde vos dimensions visuelles
                            Width = TileSize + 25,
                            Height = TileSize + 75
                        };

                        double obsLeft = col * TileSize - 15;
                        double obsTop = row * TileSize - 60;

                        Canvas.SetLeft(obstacle, obsLeft);
                        Canvas.SetTop(obstacle, obsTop);
                        Panel.SetZIndex(obstacle, 1);

                        mapCanvas.Children.Add(obstacle);

                        Rect hitBox = new Rect(
                            obsLeft + 40,   // Marge à gauche
                            obsTop + 80,    // Marge en haut (le rocher est haut visuellement)
                            TileSize - 10,  // Largeur réelle de l'obstacle
                            TileSize - 10   // Hauteur réelle de l'obstacle
                        );
                        obstacleHitboxes.Add(hitBox);
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
        private bool CheckCollision(Rect playerRect)
        {
            foreach (Rect obstacle in obstacleHitboxes)
            {
                if (playerRect.IntersectsWith(obstacle))
                {
                    return true; // Collision détectée !
                }
            }
            return false; // Voie libre
        }

    }
}