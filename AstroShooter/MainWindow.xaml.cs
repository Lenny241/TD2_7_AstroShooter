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
        private static readonly double MOVE_SPEED = 900;
        private static readonly double BULLET_SPEED = 600;

        private static readonly int CENTER_MIN = (MAP_SIZE / 2) - 1;
        private static readonly int CENTER_MAX = (MAP_SIZE / 2) + 1;
        private static readonly int MAP_LIMITE_LOW = 3;
        private static readonly int MAP_LIMITE_HIGH = MAP_SIZE - 3;

        public static readonly ushort INITIAL_METEOR_COUNT = 6;
        public static readonly ushort INITIAL_ENEMY_COUNT = 5;

        private double timeSinceLastShoot = 0;
        double shootCooldown = 0.3;
        Random rnd = new Random();
        Rect RocketHitBox;

        private static readonly MediaPlayer music = new MediaPlayer();
        public static void PlayMusic() => music.Play();
        public static void StopMusic() => music.Stop();
        public static void PauseMusic() => music.Pause();
        public static void SetMusicVolume(double volume) => music.Volume = volume;
        public static double GetMusicVolume() => music.Volume;

        private bool isPaused = false;
        private bool isPlaying = false;

        private List<Rectangle> bullets = new();
        private List<double> bulletWorldX = new();
        private List<double> bulletWorldY = new();
        private List<Vector> directions = new();

        private List<Image> meteors = new();

        private List<Rect> obstacleHitboxes = new();

        private Canvas mapCanvas = null!;
        private double mapOffsetX = 0;
        private double mapOffsetY = 0;

        private readonly HashSet<Key> pressedKeys = [];
        private readonly Stopwatch gameTime = new();
        private TimeSpan lastFrameTime;

        private Image player = null!;
        private List<BitmapImage> animUp = new List<BitmapImage>();
        private List<BitmapImage> animDown = new List<BitmapImage>();
        private List<BitmapImage> animLeft = new List<BitmapImage>();
        private List<BitmapImage> animRight = new List<BitmapImage>();

        private BitmapImage enemy = null!;
        private List<BitmapImage> animEnemy = new List<BitmapImage>();

        private int currentFrame = 0;
        private double frameTimer = 0;         // Compteur de temps
        private double timePerFrame = 0.1;     // Vitesse : change d'image toutes les 0.1 secondes
        private string currentDirection = "Down"; // Pour se souvenir de la dernière direction
        private bool isMoving = false;         // Pour savoir si on doit animer ou rester figé

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

            for (int i = 1; i < 5; i++)
            {
                animDown.Add(new BitmapImage(new Uri($"pack://application:,,,/asset/character/down/characterDown_{i}.png")));
                animUp.Add(new BitmapImage(new Uri($"pack://application:,,,/asset/character/up/characterUp_{i}.png")));
                animLeft.Add(new BitmapImage(new Uri($"pack://application:,,,/asset/character/left/characterLeft_{i}.png")));
                animRight.Add(new BitmapImage(new Uri($"pack://application:,,,/asset/character/right/characterRight_{i}.png")));
                animEnemy.Add(new BitmapImage(new Uri($"pack://application:,,,/asset/enemy/enemy_{i}.png")));
            }


            meteorImage = new BitmapImage(new Uri("pack://application:,,,/asset/ground/asteroide.png"));
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
            if (isPlaying && !isPaused)
            {
                MovePlayer(deltaTime);       // Calcule la position et l'état (isMoving, Direction)
                UpdatePlayerAnimation(deltaTime); // Met à jour l'image en fonction de l'état
                MoveBullets(deltaTime);
                UpdateDisplay();
                timeSinceLastShoot+= deltaTime;
                if (pressedKeys.Contains(Key.Space))
                {
                    if (timeSinceLastShoot >= shootCooldown)
                    {
                        ShootBullet(Mouse.GetPosition(GameCanvas));
                        timeSinceLastShoot = 0;
                    }
                }
            }
        }

        // =====================
        // ENEMIES
        // =====================

        private void AddEnemy()
        {
            int maxTries = 100; // Sécurité pour éviter une boucle infinie si la map est pleine

            for (int tries = 0; tries < maxTries; tries++)
            {
                // 1. Choisir une case au hasard (en respectant les limites de la map)
                int row = rnd.Next(3, MAP_SIZE - 3);
                int col = rnd.Next(3, MAP_SIZE - 3);

                // 2. Vérifier qu'on n'est pas dans la zone de départ (centre)
                bool isCenter = (row >= CENTER_MIN && row <= CENTER_MAX && col >= CENTER_MIN && col <= CENTER_MAX);
                if (isCenter) continue; // On recommence la boucle si on est au centre

                // 3. Calculer la position pour centrer l'ennemi dans la case
                double enemyWidth = 50;
                double enemyHeight = 50;

                // Formule pour centrer l'objet dans la tuile (TILE_SIZE)
                double posX = (col * TILE_SIZE) + (TILE_SIZE - enemyWidth) / 2;
                double posY = (row * TILE_SIZE) + (TILE_SIZE - enemyHeight) / 2;

                // Créer une Hitbox temporaire pour tester la collision
                Rect newEnemyRect = new Rect(
                    posX, 
                    posY, 
                    enemyWidth, 
                    enemyHeight
                );

                // 4. Vérifier la collision avec les obstacles existants (Rochers, Fusée, Météores)
                bool collision = false;
                foreach (Rect obstacle in obstacleHitboxes)
                {
                    if (obstacle.IntersectsWith(newEnemyRect))
                    {
                        collision = true;
                        break;
                    }
                }

                // Si la place est libre
                if (!collision)
                {
                    Image newEnemy = new Image
                    {
                        Width = enemyWidth,
                        Height = enemyHeight,
                        Source = animEnemy[0],
                        Stretch = Stretch.Uniform
                    };

                    Canvas.SetLeft(newEnemy, posX);
                    Canvas.SetTop(newEnemy, posY);
                    Panel.SetZIndex(newEnemy, 2); // Pour que l'ennemi soit au-dessus du sol

                    mapCanvas.Children.Add(newEnemy);

                    // animeEnemy.Add(newEnemy); 

                    return;
                }
            }
        }

        // =====================
        // PLAYER
        // =====================

        private void CreatePlayer()
        {
            // On remplace le Rectangle par une Image
            player = new Image
            {
                Width = 50,
                Height = 90,
                Source = animDown[0],
                Stretch = Stretch.Uniform // Pour garder les proportions
            };
            // Position fixe au centre de l'écran
            GameCanvas.Children.Add(player);
        }

        private void MovePlayer(double deltaTime)
        {
            // Calculer le mouvement basé sur les touches pressées
            double deltaX = 0;
            double deltaY = 0;

            // Réinitialiser isMoving à chaque frame
            isMoving = false;

            // --- Gestion de l'axe Y (Haut / Bas) ---
            if (pressedKeys.Contains(Key.Z) || pressedKeys.Contains(Key.Up))
            {
                deltaY += 1;
                currentDirection = "Up";
                isMoving = true;
            }
            if (pressedKeys.Contains(Key.S) || pressedKeys.Contains(Key.Down))
            {
                deltaY -= 1;
                currentDirection = "Down";
                isMoving = true;
            }

            if (pressedKeys.Contains(Key.Q) || pressedKeys.Contains(Key.Left))
            {
                deltaX += 1;
                currentDirection = "Left";
                isMoving = true;
            }
            if (pressedKeys.Contains(Key.D) || pressedKeys.Contains(Key.Right))
            {
                deltaX -= 1;
                currentDirection = "Right";
                isMoving = true;
            }


            // Appliquer le mouvement avec delta time
            if (deltaX != 0 || deltaY != 0)
            {
                double length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

                // pour éviter un déplacement plus rapide en diagonale
                deltaX /= length;
                deltaY /= length;

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
        // PLAYER ANIMATION
        // =====================

        private void UpdatePlayerAnimation(double deltaTime)
        {
            if (isMoving)
            {
                frameTimer += deltaTime;

                // Est-ce qu'il est temps de changer d'image ?
                if (frameTimer >= timePerFrame)
                {
                    frameTimer = 0;
                    currentFrame++; // Image suivante
                }
            }
            else
            {
                // Si on ne bouge pas, on reste sur la première image (position statique)
                currentFrame = 0;
                frameTimer = 0;
            }

            // --- APPLICATION DE L'IMAGE ---

            // On sélectionne la bonne liste selon la direction
            List<BitmapImage> currentAnimList = animDown; // Par défaut

            switch (currentDirection)
            {
                case "Up": currentAnimList = animUp; break;
                case "Down": currentAnimList = animDown; break;
                case "Left": currentAnimList = animLeft; break;
                case "Right": currentAnimList = animRight; break;
            }

            // Sécurité : on s'assure que currentFrame ne dépasse pas la taille de la liste (Modulo)
            if (currentAnimList.Count > 0)
            {
                int indexReel = currentFrame % currentAnimList.Count;
                player.Source = currentAnimList[indexReel];
            }
        }


        // =====================
        // BULLETS
        // =====================

        private void ShootBullet(Point Target)
        {
            double playerCenterX = Canvas.GetLeft(player) - mapOffsetX + player.Width / 2;
            double playerCenterY = Canvas.GetTop(player) - mapOffsetY + player.Height / 2;
            double pX = Target.X - mapOffsetX;
            double pY = Target.Y - mapOffsetY;

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
            double angle = Math.Atan2(direction.Y, direction.X) * 180 / Math.PI;
            bullet.RenderTransform = new RotateTransform(angle);

            bullets.Add(bullet);
            directions.Add(direction);
            bulletWorldX.Add(playerCenterX);
            bulletWorldY.Add(playerCenterY);
#if DEBUG
            Console.WriteLine("Space pressed at Mouse X: " + pX + " Mouse Y: " + pY);
            Console.WriteLine("vector X: " + direction.X + " vector Y: " + direction.Y);
            Console.WriteLine("Angle: " + angle);
#endif
        }

        private void MoveBullets(double deltaTime)
        {
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                bool bulletRemoved = false;

                Rectangle bullet = bullets[i];
                Vector direction = directions[i];

                // Avancer la balle dans le monde
                bulletWorldX[i] += direction.X * BULLET_SPEED * deltaTime;
                bulletWorldY[i] += direction.Y * BULLET_SPEED * deltaTime;

                // Convertir position monde -> position écran
                double x = bulletWorldX[i] + mapOffsetX - bullet.Width / 2;
                double y = bulletWorldY[i] + mapOffsetY - bullet.Height / 2;

                Canvas.SetLeft(bullet, x);
                Canvas.SetTop(bullet, y);

                //Rect bulletRect = new Rect(Canvas.GetLeft(bullet) - mapOffsetX,Canvas.GetTop(bullet) - mapOffsetY,bullet.Width,bullet.Height);

                if (x < 0 || x > GameCanvas.ActualWidth || y < 0 || y > GameCanvas.ActualHeight)
                {
                   bulletRemoved = true;
                }

                if(bulletRemoved == true)
                {
                    GameCanvas.Children.Remove(bullet);
                    bullets.RemoveAt(i);
                    directions.RemoveAt(i);
                    bulletWorldX.RemoveAt(i);
                    bulletWorldY.RemoveAt(i);
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
            // 1. Initialisation
            if (mapCanvas == null) mapCanvas = new Canvas();
            mapCanvas.Children.Clear();
            obstacleHitboxes.Clear();

            // 2. Boucle unique pour le sol et les obstacles
            for (int row = 0; row < MAP_SIZE; row++)
            {
                for (int col = 0; col < MAP_SIZE; col++)
                {
                    double posX = col * TILE_SIZE;
                    double posY = row * TILE_SIZE;

                    // --- A. Création du Sol ---
                    Image tile = new Image
                    {
                        Source = tileImage,
                        Width = TILE_SIZE,
                        Height = TILE_SIZE
                    };

                    Canvas.SetLeft(tile, posX);
                    Canvas.SetTop(tile, posY);
                    mapCanvas.Children.Add(tile);

                    // --- B. Logique des Obstacles ---

                    // Optimisation de la détection de bordure (plus rapide que votre booléen géant)
                    bool estBordure = (row < MAP_LIMITE_LOW || row >= MAP_LIMITE_HIGH || col < MAP_LIMITE_LOW || col >= MAP_LIMITE_HIGH);

                    // Optimisation de la détection du centre
                    bool estCentre = (row >= CENTER_MIN && row <= CENTER_MAX && col >= CENTER_MIN && col <= CENTER_MAX);

                    bool mettreObstacle = false;

                    if (estBordure)
                    {
                        mettreObstacle = true;
                    }
                    else if (!estCentre)
                    {
                        // 10% de chance pour chaque tile non bordure et non centre
                        if (rnd.NextDouble() < 0.1)
                        {
                            mettreObstacle = true;
                        }
                    }

                    if (mettreObstacle)
                    {
                        Image obstacle = new Image
                        {
                            Source = obstacleImage,
                            Width = TILE_SIZE + 25,
                            Height = TILE_SIZE + 75
                        };

                        double obsLeft = posX - 15;
                        double obsTop = posY - 60;

                        Canvas.SetLeft(obstacle, obsLeft);
                        Canvas.SetTop(obstacle, obsTop);
                        Panel.SetZIndex(obstacle, 1); // Au-dessus du sol

                        mapCanvas.Children.Add(obstacle);

                        Rect hitBox = new Rect(
                            obsLeft + 40,
                            obsTop + 80,
                            TILE_SIZE - 10,
                            TILE_SIZE - 10
                        );
                        obstacleHitboxes.Add(hitBox);
                    }
                }
            }

            // 3. Placement de la Fusée 
            int rocketRow = MAP_SIZE / 2;
            int rocketCol = MAP_SIZE / 2 + 1;

            double rocketLeft = rocketCol * TILE_SIZE;
            double rocketTop = rocketRow * TILE_SIZE;

            Image rocket = new Image
            {
                Source = rocketImage,
                Width = TILE_SIZE,
                Height = TILE_SIZE
            };



            Canvas.SetLeft(rocket, rocketLeft);
            Canvas.SetTop(rocket, rocketTop);
            Panel.SetZIndex(rocket, 2); // Au-dessus des obstacles si besoin
            mapCanvas.Children.Add(rocket);

            RocketHitBox = new Rect(rocketLeft, rocketTop, TILE_SIZE, TILE_SIZE);

            obstacleHitboxes.Add(RocketHitBox);

            // 4. Ajout au Canvas principal
            if (!GameCanvas.Children.Contains(mapCanvas))
            {
                GameCanvas.Children.Add(mapCanvas);
            }

            // 5. Météores
            for (int i = 0; i < INITIAL_METEOR_COUNT; i++)
            {
                AddMeteor();
            }

            for (int i = 0; i < INITIAL_ENEMY_COUNT; i++)
            {
                AddEnemy();
            }
        }

        private void GameCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point clickPos = e.GetPosition(GameCanvas);
            // Hitbox en coordonnées monde (comme stockée dans obstacleHitboxes)


            // Convertir le clic écran en coordonnées monde
            Point clickWorld = new Point(clickPos.X - mapOffsetX, clickPos.Y - mapOffsetY);

            if (RocketHitBox.Contains(clickWorld))
            {
                ShowShopScreen();
#if DEBUG
                Console.WriteLine("Ship clicked");
#endif
            }

            foreach (var meteor in meteors.ToList())
            {
                double left = Canvas.GetLeft(meteor);
                double top = Canvas.GetTop(meteor);
                double width = meteor.Width;
                double height = meteor.Height;

                Rect meteorHitbox = new Rect(left, top, width, height);


                if (meteorHitbox.Contains(clickWorld))
                {
                    mapCanvas.Children.Remove(meteor);
                    meteors.Remove(meteor);
                    obstacleHitboxes.Remove(meteorHitbox);
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

                bool escentre = (row >= CENTER_MIN && row <= CENTER_MAX && col >= CENTER_MIN && col <= CENTER_MAX);

                Rect newMeteorRect = new Rect(metLeft, metTop, TILE_SIZE, TILE_SIZE);

                if (!escentre) { 
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
        }

        // =====================
        // UI / MENUS
        // =====================

        public void ShowShopScreen()
        {
            music.Pause();
            gameTime.Stop();
            ScreenContainer.Children.Clear();
            UCShop shop = new UCShop();
            shop.CloseShopRequested += (s, e) => CloseShopScreen();
            ScreenContainer.Children.Add(shop);
            Blur();
            isPaused = true;
        }

        public void CloseShopScreen()
        {
            ScreenContainer.Children.Clear();
            GameCanvas.Effect = null;
            gameTime.Start();
            isPaused = false;
            music.Play();
        }

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

        private void Blur()
        {
            GameCanvas.Effect = new BlurEffect();
        }
        private void ShowPauseScreenUI()
        {
            music.Pause();
            gameTime.Stop();
            Blur();
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