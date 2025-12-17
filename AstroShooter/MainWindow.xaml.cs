using Microsoft.Windows.Themes;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.DirectoryServices;
using System.Runtime.CompilerServices;
using System.Security.Permissions;
using System.Security.Policy;
using System.Windows;
using System.Windows.Automation;
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
        private static readonly double BULLET_SPEED = 600;
        private static readonly int CENTER_MIN = (MAP_SIZE / 2) - 1;
        private static readonly int CENTER_MAX = (MAP_SIZE / 2) + 1;
        private static readonly int MAP_LIMITE_LOW = 3;
        private static readonly int MAP_LIMITE_HIGH = MAP_SIZE - 3;
        private static readonly ushort INITIAL_METEOR_COUNT = 10;
        private static readonly ushort INITIAL_ENEMY_COUNT = 5;
        private static ushort MAX_LIVES = 5;
        private static ushort NUGGETS_FOR_EXTRA_LIFE = 3;
        private static readonly ushort NUGGETS_FOR_SPEED_UPGRADE = 2;
        private static readonly ushort NUGGETS_FOR_SHOOTCOOLDOWN_UPGRADE = 2;
        private static readonly ushort SPEED_UPGRADE_AMOUNT = 50;
        private static readonly double SHOOTCOOLDOWN_UPGRADE_AMOUNT = 0.05;
        private static readonly uint MAX_PLAYER_SPEED = 800;
        private static readonly double ENEMY_SPEED = 100; // Vitesse de déplacement des ennemis
        private static readonly double INVINCIBILITY_DURATION = 2.0; // Durée d'invincibilité en secondes
        private bool isInvincible = false;
        private double invincibilityTimer = 0;

        private UCShop currentShop;
        private double timeSinceLastShoot = 0;
        private double shootCooldown = 0.3;
        Random rnd = new Random();
        Rect RocketHitBox;

        public int nbNuggets;

        private double move_speed = 500;

        private List<Image> lives = new();
        private int currentLives = 3;

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
        private static readonly double PLAYER_WIDTH = 50;
        private static readonly double PLAYER_HEIGHT = 90;

        private List<BitmapImage> animUp = new List<BitmapImage>();
        private List<BitmapImage> animDown = new List<BitmapImage>();
        private List<BitmapImage> animLeft = new List<BitmapImage>();
        private List<BitmapImage> animRight = new List<BitmapImage>();

        private List<BitmapImage> enemyAnimImages = new List<BitmapImage>(); // Stocke les 4 sprites
        private List<Image> enemies = new List<Image>();
        private BitmapImage enemyDeadImage = null!;
        private HashSet<Image> deadEnemies = new HashSet<Image>(); // Ennemis en état "mort"
        private static readonly double ENEMY_WIDTH = 50;
        private static readonly double ENEMY_HEIGHT = 90;

        private double enemyFrameTimer = 0;   // Timer spécifique aux ennemis
        private int enemyCurrentFrame = 0;    // Compteur d'image spécifique aux ennemis

        private int currentFrame = 0;
        private double frameTimer = 0;         // Compteur de temps
        private double timePerFrame = 0.1;     // Vitesse : change d'image toutes les 0.1 secondes
        private string currentDirection = "Down"; // Pour se souvenir de la dernière direction
        private bool isMoving = false;         // Pour savoir si on doit animer ou rester figé

        private BitmapImage meteorImage = null!;
        private BitmapImage tileImage = null!;
        private BitmapImage obstacleImage = null!;
        private BitmapImage rocketImage = null!;
        private BitmapImage lifeIconImage = null!;

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
                enemyAnimImages.Add(new BitmapImage(new Uri($"pack://application:,,,/asset/enemy/enemy_{i}.png")));
            }

            // Charger le sprite de l'ennemi mort
            enemyDeadImage = new BitmapImage(new Uri("pack://application:,,,/asset/enemy/dead/enemy_dead.png"));

            meteorImage = new BitmapImage(new Uri("pack://application:,,,/asset/ground/asteroide.png"));
            tileImage = new BitmapImage(new Uri("pack://application:,,,/asset/ground/classicGroundTile1.png"));
            obstacleImage = new BitmapImage(new Uri("pack://application:,,,/asset/ground/rock.png"));
            rocketImage = new BitmapImage(new Uri("pack://application:,,,/asset/character/vaisseau.png"));
            lifeIconImage = new BitmapImage(new Uri("pack://application:,,,/asset/shop/lifeIcon.png"));

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
            VariablesInitialisation();
            ScreenContainer.Children.Clear();
            GameCanvas.Effect = null;
            isPlaying = true;
            isPaused = false;
            lifedisplay();
            nuggetsDisplay(); 
        }

        public void VariablesInitialisation()
        {
            move_speed = 500;
            shootCooldown = 0.3;
            currentLives = 3;
            nbNuggets = 0;
            isInvincible = false;
            invincibilityTimer = 0;
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

        private void GameOver()
        {
            StopGame();
            GameOverScreen();
        }

        private void StopGame()
        {
            GameCanvas.Children.Remove(player);
            isPlaying = false;
            isPaused = false;
            gameTime.Stop();
            foreach (var bullet in bullets)
            {
                GameCanvas.Children.Remove(bullet);
            }
            bullets.Clear();
            directions.Clear();
            obstacleHitboxes.Clear();
            enemies.Clear();
            deadEnemies.Clear(); // Ajoutez cette ligne
            GenerateMap();
            CreatePlayer();
            CenterMapOnPlayer();
            music.Stop();
            music.Play();
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
                MovePlayer(deltaTime);
                UpdatePlayerAnimation(deltaTime);
                UpdateEnemyAnimations(deltaTime);
                MoveEnemies(deltaTime);           // Ajout : déplacement des ennemis
                UpdateInvincibility(deltaTime);   // Ajout : gestion de l'invincibilité
                MoveBullets(deltaTime);
                UpdateDisplay();
                timeSinceLastShoot += deltaTime;
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

        private async void KillEnemy(Image enemy)
        {
            // Retirer de la liste des ennemis actifs
            enemies.Remove(enemy);

            // Ajouter aux ennemis morts
            deadEnemies.Add(enemy);

            // Afficher le sprite "dead"
            enemy.Source = enemyDeadImage;

#if DEBUG
            Console.WriteLine("Ennemi tué !");
#endif

            // Attendre 3 secondes puis supprimer
            await Task.Delay(3000);

            // Vérifier que l'ennemi existe toujours (au cas où la partie s'est arrêtée)
            if (deadEnemies.Contains(enemy))
            {
                deadEnemies.Remove(enemy);
                mapCanvas.Children.Remove(enemy);
            }

            int newEnemyCount = rnd.Next(1, 3); // 1 ou 2 nouveaux ennemis
            for (int i = 0; i < newEnemyCount; i++)
            {
                AddEnemy();
            }
        }

        private void MoveEnemies(double deltaTime)
        {
            // Position du joueur dans le monde
            double playerScreenX = Canvas.GetLeft(player);
            double playerScreenY = Canvas.GetTop(player);
            double playerWorldX = playerScreenX - mapOffsetX;
            double playerWorldY = playerScreenY - mapOffsetY;

            // Hitbox du joueur
            Rect playerRect = new Rect(playerWorldX, playerWorldY, player.Width, player.Height);

            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                Image enemy = enemies[i];

                double enemyX = Canvas.GetLeft(enemy);
                double enemyY = Canvas.GetTop(enemy);

                // Calculer la direction vers le joueur
                double dirX = playerWorldX - enemyX;
                double dirY = playerWorldY - enemyY;

                // Normaliser le vecteur direction
                double length = Math.Sqrt(dirX * dirX + dirY * dirY);
                if (length > 0)
                {
                    dirX /= length;
                    dirY /= length;
                }

                // Nouvelle position proposée
                double newX = enemyX + dirX * ENEMY_SPEED * deltaTime;
                double newY = enemyY + dirY * ENEMY_SPEED * deltaTime;

                // Vérifier collision avec les obstacles
                Rect enemyRectX = new Rect(newX, enemyY, enemy.Width, enemy.Height);
                Rect enemyRectY = new Rect(enemyX, newY, enemy.Width, enemy.Height);

                bool canMoveX = !CheckCollision(enemyRectX);
                bool canMoveY = !CheckCollision(enemyRectY);

                if (canMoveX)
                {
                    Canvas.SetLeft(enemy, newX);
                    enemyX = newX;
                }
                if (canMoveY)
                {
                    Canvas.SetTop(enemy, newY);
                    enemyY = newY;
                }

                // Vérifier collision avec le joueur
                Rect enemyRect = new Rect(enemyX, enemyY, enemy.Width, enemy.Height);
                if (enemyRect.IntersectsWith(playerRect))
                {
                    OnPlayerHit();
                }
            }
        }

        private void OnPlayerHit()
        {
            if (isInvincible) return;

            RemoveLife();
            StartInvincibility();

#if DEBUG
            Console.WriteLine("Joueur touché par un ennemi !");
#endif
        }

        private void StartInvincibility()
        {
            isInvincible = true;
            invincibilityTimer = INVINCIBILITY_DURATION;
        }

        private void UpdateInvincibility(double deltaTime)
        {
            if (!isInvincible) return;

            invincibilityTimer -= deltaTime;

            // Effet de clignotement du joueur
            if ((int)(invincibilityTimer * 10) % 2 == 0)
            {
                player.Opacity = 0.5;
            }
            else
            {
                player.Opacity = 1.0;
            }

            // Fin de l'invincibilité
            if (invincibilityTimer <= 0)
            {
                isInvincible = false;
                player.Opacity = 1.0;
            }
        }
        public void AddEnemy()
        {
            // liste pour noter les coordonnées (Col, Row) libres
            List<Point> freeSpots = new List<Point>();

            for (int row = 3; row < MAP_SIZE - 3; row++)
            {
                for (int col = 3; col < MAP_SIZE - 3; col++)
                {
                    // A. Vérifier si c'est la zone centrale (Spawn joueur)
                    bool estCentre = (row >= CENTER_MIN && row <= CENTER_MAX &&
                                      col >= CENTER_MIN && col <= CENTER_MAX);

                    if (estCentre) continue; // On passe à la case suivante

                    // B. Vérifier si cette case touche un obstacle existant
                    // On crée un rectangle théorique à cet emplacement
                    Rect potentialRect = new Rect(col * TILE_SIZE, row * TILE_SIZE, 50, 90);

                    bool collision = false;
                    foreach (Rect obstacle in obstacleHitboxes)
                    {
                        if (obstacle.IntersectsWith(potentialRect))
                        {
                            collision = true;
                            break;
                        }
                    }

                    // C. Si pas de collision, c'est une place valide ! On l'ajoute à la liste.
                    if (!collision)
                    {
                        freeSpots.Add(new Point(col, row));
                    }
                }
            }

            // 3. Vérification finale : Est-ce qu'il reste de la place ?
            if (freeSpots.Count > 0)
            {
                // On pioche une case au hasard DANS la liste des cases libres
                int index = rnd.Next(freeSpots.Count);
                Point selectedSpot = freeSpots[index];

                // On calcule la position finale en pixels
                // (J'ai gardé votre petit décalage aléatoire +0-50 pour le style)
                double enemyLeft = selectedSpot.X * TILE_SIZE + rnd.Next(0, 50);
                double enemyTop = selectedSpot.Y * TILE_SIZE + rnd.Next(0, 50);

                // --- Création visuelle de l'ennemi (votre code original) ---
                Image enemy = new Image
                {
                    Source = enemyAnimImages[0],
                    Width = 50,
                    Height = 90,
                    Stretch = Stretch.Uniform
                };

                Canvas.SetLeft(enemy, enemyLeft);
                Canvas.SetTop(enemy, enemyTop);
                Panel.SetZIndex(enemy, 2);

                mapCanvas.Children.Add(enemy);
                enemies.Add(enemy);

                // Optionnel : Ajouter la hitbox de l'ennemi aux obstacles pour éviter
                // que le prochain ennemi apparaisse EXACTEMENT sur celui-ci
                // obstacleHitboxes.Add(new Rect(enemyLeft, enemyTop, 50, 90));

#if DEBUG
                Console.WriteLine($"Ennemi ajouté en : {selectedSpot.X}, {selectedSpot.Y}");
#endif
            }
            else
            {
#if DEBUG
                Console.WriteLine("Carte pleine ! Impossible d'ajouter un ennemi.");
#endif
            }
        }

        private void UpdateEnemyAnimations(double deltaTime)
        {
            if (enemies.Count == 0) return;

            enemyFrameTimer += deltaTime;

            // Changer d'image toutes les 0.15 secondes (ajustez la vitesse ici)
            if (enemyFrameTimer >= 0.15)
            {
                enemyFrameTimer = 0;
                enemyCurrentFrame++;

                // Boucler l'index (0, 1, 2, 3, 0, 1...)
                int frameIndex = enemyCurrentFrame % enemyAnimImages.Count;
                BitmapImage currentImage = enemyAnimImages[frameIndex];

                // Mettre à jour tous les ennemis
                foreach (Image enemy in enemies)
                {
                    enemy.Source = currentImage;
                }
            }
        }

        // =====================
        // PLAYER
        // =====================

        private void SpeedUpgrade()
        {
            if((nbNuggets>=NUGGETS_FOR_SPEED_UPGRADE) && (move_speed<=MAX_PLAYER_SPEED))
            {
                move_speed += SPEED_UPGRADE_AMOUNT;
                nbNuggets -= (int)NUGGETS_FOR_SPEED_UPGRADE;
                nuggetsDisplay();
#if DEBUG
                Console.WriteLine("Speed upgrated");
#endif
            }
        }

        private void lifedisplay()
        {
            foreach (Image life in lives)
            {
                GameCanvas.Children.Remove(life);
            }
            lives.Clear();

            for (int i = 0; i < currentLives; i++)
            {
                Image lifeIcon = new Image
                {
                    Width = 40,
                    Height = 40,
                    Source = lifeIconImage,
                };
                Canvas.SetLeft(lifeIcon, i*100+10);
                Canvas.SetTop(lifeIcon, 10);
                GameCanvas.Children.Add(lifeIcon);
                lives.Add(lifeIcon);
            }
        }

        private void AddLife()
        {
            if ((currentLives < MAX_LIVES) && (nbNuggets >= NUGGETS_FOR_EXTRA_LIFE))
            {
#if DEBUG
                Console.WriteLine("Adding life");
#endif
                Image lifeIcon = new Image
                {
                    Width = 40,
                    Height = 40,
                    Source = lifeIconImage,
                };
                Canvas.SetLeft(lifeIcon, lives.Count * 100 + 10);
                Canvas.SetTop(lifeIcon, 10);

                GameCanvas.Children.Add(lifeIcon);
                lives.Add(lifeIcon);
                currentLives++;
                nbNuggets -= (int)NUGGETS_FOR_EXTRA_LIFE;
                nuggetsDisplay();
            }
        }

        private void RemoveLife()
        {
            if (currentLives > 0)
            {
#if DEBUG
                Console.WriteLine("Removing life");
#endif
                int last = lives.Count - 1;
                GameCanvas.Children.Remove(lives[last]);
                lives.RemoveAt(last);
                currentLives--;

                if (currentLives == 0)
                {
                    GameOver();
#if DEBUG
                    Console.WriteLine("GameOver");
#endif
                }
            }
        }

        private void AddNugget()
        {
            nbNuggets++;
            nuggetsDisplay();
        }
        private void CreatePlayer()
        {
            // On remplace le Rectangle par une Image
            player = new Image
            {
                Width = PLAYER_WIDTH,
                Height = PLAYER_HEIGHT,
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
                double proposedMapOffsetX = mapOffsetX + (deltaX * move_speed * deltaTime);

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
                double proposedMapOffsetY = mapOffsetY + (deltaY * move_speed * deltaTime);

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
        private void ShootcooldownUpgrade()
        {
            if ((shootCooldown > 0.05) && (nbNuggets>=NUGGETS_FOR_SHOOTCOOLDOWN_UPGRADE))
            {
                shootCooldown -= SHOOTCOOLDOWN_UPGRADE_AMOUNT;
                nbNuggets -= (int)NUGGETS_FOR_SHOOTCOOLDOWN_UPGRADE;
                nuggetsDisplay();
#if DEBUG
                Console.WriteLine("Shootcooldown improved");
#endif
            }
        }
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

                // Créer la hitbox de la balle en coordonnées monde
                Rect bulletRect = new Rect(bulletWorldX[i] - bullet.Width / 2, bulletWorldY[i] - bullet.Height / 2, bullet.Width, bullet.Height);

                // Vérifier collision avec les ennemis
                for (int j = enemies.Count - 1; j >= 0; j--)
                {
                    Image enemy = enemies[j];
                    double enemyLeft = Canvas.GetLeft(enemy);
                    double enemyTop = Canvas.GetTop(enemy);
                    Rect enemyRect = new Rect(enemyLeft, enemyTop, enemy.Width, enemy.Height);

                    if (bulletRect.IntersectsWith(enemyRect))
                    {
                        // Ennemi touché : le faire mourir
                        KillEnemy(enemy);
                        bulletRemoved = true;
                        break;
                    }
                }

                if (x < 0 || x > GameCanvas.ActualWidth || y < 0 || y > GameCanvas.ActualHeight)
                {
                    bulletRemoved = true;
                }

                if (bulletRemoved)
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
            if (mapCanvas == null)
            {
                mapCanvas = new Canvas();
            }
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

            // 6. Ennemis
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
                    AddNugget();
                    AddMeteor();
#if DEBUG
                    Console.WriteLine("Meteor clicked");
#endif
                }
            }
        }

        public void AddMeteor()
        {
            // 1. Liste des places libres
            List<Point> freeSpots = new List<Point>();

            // 2. Scan de la carte
            for (int row = 3; row < MAP_SIZE - 3; row++)
            {
                for (int col = 3; col < MAP_SIZE - 3; col++)
                {
                    // A. Vérification Centre (Spawn joueur)
                    bool estCentre = (row >= CENTER_MIN && row <= CENTER_MAX &&
                                      col >= CENTER_MIN && col <= CENTER_MAX);

                    if (estCentre) continue;

                    // B. Vérification Collision avec Obstacles existants
                    Rect potentialRect = new Rect(
                        col * TILE_SIZE, row * TILE_SIZE, TILE_SIZE, TILE_SIZE);

                    bool collision = false;
                    foreach (Rect obstacle in obstacleHitboxes)
                    {
                        if (obstacle.IntersectsWith(potentialRect))
                        {
                            collision = true;
                            break;
                        }
                    }

                    // C. Si libre, on ajoute à la liste
                    if (!collision)
                    {
                        freeSpots.Add(new Point(col, row));
                    }
                }
            }

            // 3. Choix et Placement
            if (freeSpots.Count > 0)
            {
                // Choix aléatoire parmi les places valides
                int index = rnd.Next(freeSpots.Count);
                Point selectedSpot = freeSpots[index];

                double metLeft = selectedSpot.X * TILE_SIZE + rnd.Next(-20, 20);
                double metTop = selectedSpot.Y * TILE_SIZE + rnd.Next(-20, 20);

                Rect newMeteorRect = new Rect(metLeft, metTop, TILE_SIZE, TILE_SIZE);

                // Double vérification de sécurité (optionnelle mais recommandée à cause du rnd décalage)
                // Le décalage aléatoire pourrait théoriquement pousser le météore sur un obstacle voisin
                bool collisionFinale = false;
                foreach (var obs in obstacleHitboxes)
                {
                    if (obs.IntersectsWith(newMeteorRect)) { collisionFinale = true; break; }
                }

                if (!collisionFinale)
                {
                    // --- Création Visuelle ---
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

#if DEBUG
                    Console.WriteLine($"Météore ajouté en : {selectedSpot.X}, {selectedSpot.Y}");
#endif
                }
            }
            else
            {
#if DEBUG
                Console.WriteLine("Pas de place pour un météore supplémentaire.");
#endif
            }
        }

        // =====================
        // UI / MENUS
        // =====================

        public void ShowShopScreen()
        {
            currentShop = new UCShop();
            music.Pause();
            gameTime.Stop();
            ScreenContainer.Children.Clear();
            ScreenContainer.Children.Add(currentShop);
            currentShop.CloseShopRequested += (s, e) => CloseShopScreen();
            currentShop.ButLifeRequested += (s, e) => AddLife();
            currentShop.ButShootCooldownRequested += (s, e) => ShootcooldownUpgrade();
            currentShop.ButSpeedRequested += (s, e) => SpeedUpgrade();
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
            StopGame();
            ShowStartScreen();
        }

        private void GameOverScreen()
        {
#if DEBUG
            Console.WriteLine("Show GameOver screen");
#endif
            Blur();
            UCGameOver gameOver = new UCGameOver();
            gameOver.CloseGameOverRequested += (s, e) => ShowStartScreen();
            ScreenContainer.Children.Add(gameOver);
        }

        private void nuggetsDisplay()
        {
            InterstellarNuggetsCount.Text = "Interstellar Nuggets : " + nbNuggets;
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