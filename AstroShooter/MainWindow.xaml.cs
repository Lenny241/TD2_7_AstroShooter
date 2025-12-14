using Microsoft.Windows.Themes;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.DirectoryServices;
using System.Runtime.CompilerServices;
using System.Security.Permissions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Converters;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;


namespace AstroShooter
{

    

    public partial class MainWindow : Window
    {
        private static readonly ushort MAP_SIZE = 26;
        private static readonly ushort TILE_SIZE = 275;
        private static readonly double MOVE_SPEED = 600;
        private static readonly double BULLET_SPEED = 400;
        public static readonly ushort INITIAL_METEOR_COUNT = 6;

        Random rnd = new Random();

        private static readonly MediaPlayer music = new MediaPlayer();
        public static void PlayMusic() => music.Play();
        public static void StopMusic() => music.Stop();
        public static void PauseMusic() => music.Pause();
        public static void SetMusicVolume(double volume) => music.Volume = volume;
        public static double GetMusicVolume() => music.Volume;

        private bool isPaused = false;
        private bool isPlaying = false;

        private List<Rectangle> bullets = new();
        private List<Vector> directions = new();

        private List<Image> meteors = new();

        private List<Rect> obstacleHitboxes = new();

        private Canvas mapCanvas = null!;
        private Rectangle player = null!;
        private double mapOffsetX = 0;
        private double mapOffsetY = 0;

        private readonly HashSet<Key> pressedKeys = [];
        private readonly Stopwatch gameTime = new();
        private TimeSpan lastFrameTime;

        private BitmapImage meteorImage = null!;
        private BitmapImage tileImage = null!;
        private BitmapImage obstacleImage = null!;
        private BitmapImage obstacleTileImage= null!;
        private BitmapImage rocketImage = null!;

        // =====================
        // GAME STATE
        // =====================

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            ShowStartScreen();
            StartMusic();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            meteorImage = new BitmapImage(new Uri("pack://application:,,,/asset/ground/meteor.png"));
            tileImage = new BitmapImage(new Uri("pack://application:,,,/asset/ground/classicGroundTile1.png"));
            obstacleImage = new BitmapImage(new Uri("pack://application:,,,/asset/ground/rock.png"));
            rocketImage = new BitmapImage(new Uri("pack://application:,,,/asset/character/vaisseau.png"));

            GameCanvas.Focus();
            GenerateMap();
            CreatePlayer();
            CenterMapOnPlayer();
            lastFrameTime = gameTime.Elapsed;
            CompositionTarget.Rendering += GameLoop;
            GameCanvas.MouseLeftButtonDown += GameCanvas_MouseLeftButtonDown;
        }

        private void StartGame(object sender, RoutedEventArgs e)
        {
            GameCanvas.KeyDown += GameCanvas_KeyDown;
            GameCanvas.Focus();

            gameTime.Start();
#if DEBUG
            Console.WriteLine("StartGame");
#endif
            ScreenContainer.Children.Clear();
            GameCanvas.Effect = null;

            isPlaying = true;
            isPaused = false;
        }

        public void ResumeGame()
        {
            if (!isPaused)
            {
                return;
            }
            ScreenContainer.Children.Clear();
            GameCanvas.Effect = null;
            gameTime.Start();
            isPaused = false;
            music.Play();
#if DEBUG
            Console.WriteLine("ResumeGame");
#endif
        }

        protected override void OnClosed(EventArgs e)
        {
            // Arrêter la boucle de jeu proprement
            CompositionTarget.Rendering -= GameLoop;
            gameTime.Stop();
            base.OnClosed(e);
        }

        // =====================
        // INPUT
        // =====================



        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            pressedKeys.Add(e.Key);
            if (isPlaying == false)
            {
                return;
            }
            if (e.Key == Key.Escape)
            {
                if (!isPaused)
                {
                    ShowPauseMenu();
                }
                else
                {
                    ResumeGame();
                }
            }
        }
        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            pressedKeys.Remove(e.Key);
        }

        private void GameCanvas_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                ShootBullet(Mouse.GetPosition(GameCanvas));
#if DEBUG
                Console.WriteLine("Bullet shot!");
#endif
            }
        }



        // =====================
        // GAME LOOP
        // =====================
        private double CalculateDeltaTime()
        {
            // Calculer le delta time pour un mouvement indépendant du framerate
            TimeSpan currentTime = gameTime.Elapsed;
            double deltaTime = (currentTime - lastFrameTime).TotalSeconds;
            lastFrameTime = currentTime;
            return deltaTime;
        }
        


        private void GameLoop(object? sender, EventArgs e)
        {
            double deltaTime = CalculateDeltaTime();
            if(isPlaying == true && isPaused == false)
            {
                MovePlayer(deltaTime);
                MoveBullets(deltaTime);
                UpdateDisplay();
            }
        }

        // =====================
        // PLAYER
        // =====================

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

        private void MovePlayer(double deltaTime)
        {
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
                double proposedMapOffsetX = mapOffsetX + (deltaX * MOVE_SPEED * deltaTime);

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
                double proposedMapOffsetY = mapOffsetY + (deltaY * MOVE_SPEED * deltaTime);

                // On recalcule avec la potentielle nouvelle position X validée juste avant
                double playerWorldX_Current = playerScreenX - mapOffsetX;
                double playerWorldY_Future = playerScreenY - proposedMapOffsetY;

                Rect playerRectY = new Rect(playerWorldX_Current, playerWorldY_Future, player.Width, player.Height);

                if (!CheckCollision(playerRectY))
                {
                    mapOffsetY = proposedMapOffsetY; // Pas de collision, on valide le mouvement Y
                }
            }
        }


        // =====================
        // BULLETS
        // =====================




        private void ShootBullet(Point Target)
        {
            double playerCenterX = Canvas.GetLeft(player) + player.Width / 2;
            double playerCenterY = Canvas.GetTop(player) + player.Height / 2;
            Point position = Mouse.GetPosition(GameCanvas);
            double pX = position.X;
            double pY = position.Y;
            Vector direction = new Vector(pX - playerCenterX, pY - playerCenterY);
            direction.Normalize();
            Rectangle bullet = new Rectangle
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

        private void MoveBullets(double deltaTime)
        {
            // Move bullets
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                Rectangle bullet = bullets[i];
                Vector direction = directions[i];

                double x = Canvas.GetLeft(bullet);
                double y = Canvas.GetTop(bullet);

                Canvas.SetLeft(bullet, x + direction.X * BULLET_SPEED * deltaTime);
                Canvas.SetTop(bullet, y + direction.Y * BULLET_SPEED * deltaTime);

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


        // =====================
        // MAP & COLLISIONS
        // =====================

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

        private void CenterMapOnPlayer()
        {
            // Calcule l'offset pour centrer la map sur le joueur (basé sur le viewport)
            double centerX = (GameCanvas.ActualWidth - player.Width) / 2;
            double centerY = (GameCanvas.ActualHeight - player.Height) / 2;

            // Position initiale du joueur au centre de la map
            mapOffsetX = centerX - (MAP_SIZE * TILE_SIZE / 2.0) + (player.Width / 2);
            mapOffsetY = centerY - (MAP_SIZE * TILE_SIZE / 2.0) + (player.Height / 2);

            Canvas.SetLeft(player, centerX);
            Canvas.SetTop(player, centerY);
            Canvas.SetLeft(mapCanvas, mapOffsetX);
            Canvas.SetTop(mapCanvas, mapOffsetY);
        }

        private void UpdateDisplay()
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
            Image[,] tileGrid = new Image[MAP_SIZE, MAP_SIZE];

            // Chargement des images (UNE SEULE FOIS pour la performance)



            // Création des tuiles de sol
            for (int row = 0; row < MAP_SIZE; row++)
            {
                for (int col = 0; col < MAP_SIZE; col++)
                {
                    Image tile = new Image
                    {
                        Source = tileImage,
                        Width = TILE_SIZE + 1,
                        Height = TILE_SIZE + 1
                    };
                    tileGrid[row, col] = tile;
                }
            }

            // Affichage du sol ET ajout des obstacles
            for (int row = 0; row < MAP_SIZE; row++)
            {
                for (int col = 0; col < MAP_SIZE; col++)
                {

                    Image tile = tileGrid[row, col];
                    Canvas.SetLeft(tile, col * TILE_SIZE);
                    Canvas.SetTop(tile, row * TILE_SIZE);
                    Panel.SetZIndex(tile, 0); // Le sol est en bas (Couche 0)
                    mapCanvas.Children.Add(tile);

                    // bordure de la carte en 3 tuiles de large
                    bool estBordure = (row == 0 || col == 0 || row == 1 || col == 1 || row == 2 || col == 2 ||
                                       row == MAP_SIZE - 1 || col == MAP_SIZE - 1 || row == MAP_SIZE - 2 || col == MAP_SIZE - 2 || row == MAP_SIZE - 3 || col == MAP_SIZE - 3);

                    // centre de la carte en dimension 3x3
                    bool estCentre = (row >= (MAP_SIZE / 2 - 1) && row <= (MAP_SIZE / 2 + 1) &&
                                      col >= (MAP_SIZE / 2 - 1) && col <= (MAP_SIZE / 2 + 1));

                    // positionnement d'une seul fusé au centre de la carte
                    if (estCentre)
                    {
                        int centerRow = MAP_SIZE / 2;
                        int centerCol = MAP_SIZE / 2;
                        Image rocket = new Image
                        {
                            Source = rocketImage,
                            Width = TILE_SIZE,
                            Height = TILE_SIZE
                        };
                        double rocketLeft = centerCol * TILE_SIZE;
                        double rocketTop = centerRow * TILE_SIZE;
                        Canvas.SetLeft(rocket, rocketLeft);
                        Canvas.SetTop(rocket, rocketTop);
                        Panel.SetZIndex(rocket, 1);
                        mapCanvas.Children.Add(rocket);

                    }


                    // ajout d'obstacles (rochers)
                    bool mettreObstacle = false;

                    if (estBordure)
                    {
                        mettreObstacle = true; // Mur obligatoire
                    }
                    if (estCentre)
                    {
                        mettreObstacle = false; // Pas d'obstacle au centre
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
                            Width = TILE_SIZE + 25,
                            Height = TILE_SIZE + 75
                        };

                        double obsLeft = col * TILE_SIZE - 15;
                        double obsTop = row * TILE_SIZE - 60;

                        Canvas.SetLeft(obstacle, obsLeft);
                        Canvas.SetTop(obstacle, obsTop);
                        Panel.SetZIndex(obstacle, 1);

                        mapCanvas.Children.Add(obstacle);

                        Rect hitBox = new Rect(
                            obsLeft + 40,   // Marge à gauche
                            obsTop + 80,    // Marge en haut (le rocher est haut visuellement)
                            TILE_SIZE - 10,  // Largeur réelle de l'obstacle
                            TILE_SIZE - 10   // Hauteur réelle de l'obstacle
                        );
                        obstacleHitboxes.Add(hitBox);
                    }


                    // ajout de metéores aléatoires
                    if (estCentre || mettreObstacle || estBordure)
                    {
                        // Ne rien faire au centre (position du joueur)
                    }
                    else
                    {

                    }
                }

                // Ajouter le Canvas à la fenêtre s'il n'y est pas déjà
                if (!GameCanvas.Children.Contains(mapCanvas))
                {
                    GameCanvas.Children.Add(mapCanvas);
                }


            }

            for (int i = 0; i < INITIAL_METEOR_COUNT; i++)
            {
                AddMeteor();
            }
        }

        private void GameCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point clickPos = e.GetPosition(GameCanvas);

            foreach (var meteor in meteors.ToList()) // copie la liste pour éviter les problèmes de modification pendant l'itération
            {
                double left = Canvas.GetLeft(meteor);
                double top = Canvas.GetTop(meteor);
                double width = meteor.Width;
                double height = meteor.Height;

                Rect meteorRect = new Rect(left + mapOffsetX, top + mapOffsetY, width, height);

                if (meteorRect.Contains(clickPos))
                {
                    mapCanvas.Children.Remove(meteor);
                    meteors.Remove(meteor);
                    obstacleHitboxes.Remove(meteorRect);
#if DEBUG
                    Console.WriteLine("Meteor clicked");
#endif
                }
            }
        }

        public void AddMeteor()
        {
            

            int maxTries = 100;
            for (int tries = 0; tries < maxTries; tries++)
            {
                int row = rnd.Next(3, MAP_SIZE - 3);
                int col = rnd.Next(3, MAP_SIZE - 3);

                double metLeft = col * TILE_SIZE + rnd.Next(-20, 20);
                double metTop = row * TILE_SIZE + rnd.Next(-20, 20);

                Rect newMeteorRect = new Rect(metLeft, metTop, TILE_SIZE, TILE_SIZE);

                bool collision = false;
                for (int i = 0; i < obstacleHitboxes.Count; i++)
                {
                    if (obstacleHitboxes[i].IntersectsWith(newMeteorRect))
                    {
                        collision = true;
                        break;
                    }
                }

                if (!collision)
                {
                    Image meteor = new Image
                    {
                        Source = meteorImage,
                        Width = TILE_SIZE,
                        Height = TILE_SIZE
                    };

                    Canvas.SetLeft(meteor, metLeft);
                    Canvas.SetTop(meteor, metTop);
                    Panel.SetZIndex(meteor, 1);
                    mapCanvas.Children.Add(meteor);
                    meteors.Add(meteor);
                    obstacleHitboxes.Add(newMeteorRect);
                    return;
                }
            }
        }

        // =====================
        // UI / MENUS
        // =====================

        public void ShowStartScreen()
        {
            ScreenContainer.Children.Clear();
            UCMenu menu = new UCMenu();
            ScreenContainer.Children.Add(menu);
            menu.ButStart.Click += StartGame;
            menu.ButRules.Click += ShowRules;
        }
        public void ShowRules(object sender, RoutedEventArgs e)
        {
            UCRules uCRules = new UCRules();
            ScreenContainer.Children.Add(uCRules);
#if DEBUG
            Console.WriteLine("show rules");
#endif
        }

        private void ShowPauseMenu()
        {
            if (isPaused)
            {
                return;
            }
            isPaused = true;
            ShowPauseScreenUI();
        }

        private void ShowPauseScreenUI()
        {
            music.Pause();
            gameTime.Stop();
            GameCanvas.Effect = new BlurEffect();
            UCPauseScreen pause = new UCPauseScreen();
            pause.ResumeRequested += (s, e) => ResumeGame();
            pause.QuitRequested += (s, e) => Quit();
            ScreenContainer.Children.Add(pause);
            pause.ButRules.Click += ShowRules;
#if DEBUG
            Console.WriteLine("Show pause screen");
#endif
        }

        private void Quit()
        {
            isPlaying = false;
            isPaused = false;
            gameTime.Stop();
            GameCanvas.Children.Clear();
            bullets.Clear();
            directions.Clear();
            obstacleHitboxes.Clear();
            GenerateMap();
            CreatePlayer();
            CenterMapOnPlayer();
            ShowStartScreen();
            music.Stop();
            music.Play();
        }



        // =====================
        // AUDIO
        // =====================

        public void StartMusic()
        {
#if DEBUG
            Console.WriteLine("Start music");
#endif
            music.Open(new Uri(AppDomain.CurrentDomain.BaseDirectory + "asset/sons/music.mp3"));
            music.Volume = 0.5;
            music.MediaEnded += RelanceMusique;
            music.Play();
        }

        private void RelanceMusique(object? sender, EventArgs e)
        {
            music.Position = TimeSpan.Zero;
            music.Play();
        }
    }
}