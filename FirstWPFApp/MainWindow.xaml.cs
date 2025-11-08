using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace FirstWPFApp
{
    public partial class MainWindow : Window
    {
        // Константы
        private const double PlayerSpeed = 5.0;
        private const string BaseSpritesPath = @"C:\Users\Morozov\source\repos\FirstWPFApp\FirstWPFApp\materials\idle_sprites";
        private const string RoomsSpritesPath = @"C:\Users\Morozov\source\repos\FirstWPFApp\FirstWPFApp\materials\rooms";

        // Состояния персонажа
        private enum PlayerState { Idle, Moving }
        private enum Direction { Up, Down, Left, Right }

        private PlayerState currentState = PlayerState.Idle;
        private Direction currentDirection = Direction.Down;
        private Direction lastMovingDirection = Direction.Down;

        // Анимации для каждого направления
        private Dictionary<Direction, List<BitmapImage>> directionSprites = new Dictionary<Direction, List<BitmapImage>>();
        private int currentSpriteIndex = 0;
        private DispatcherTimer animationTimer;

        // Движение
        private bool isMovingUp = false;
        private bool isMovingDown = false;
        private bool isMovingLeft = false;
        private bool isMovingRight = false;

        // Размеры окна для ограничения движения
        private double canvasWidth = 800;
        private double canvasHeight = 400;

        private int currentRoom = 1;
        private Dictionary<int, BitmapImage> roomBackgrounds = new Dictionary<int, BitmapImage>();
        private Rect doorArea;

        public MainWindow()
        {
            InitializeComponent();

            doorArea = new Rect(20, 20, 60, 80);
            // Загружаем окно сразу, а спрайты - асинхронно
            LoadRooms();
            InitializeAnimation();
            Canvas.SetLeft(PlayerSprite, 368);
            Canvas.SetTop(PlayerSprite, 268);

            // Загружаем спрайты в фоновом режиме
            LoadSpritesAsync();
            Focus();
        }

        private void LoadRooms()
        {
            try
            {
                if (Directory.Exists(RoomsSpritesPath))
                {
                    // Загружаем все jpg файлы комнат
                    string[] roomFiles = Directory.GetFiles(RoomsSpritesPath, "*.jpg")
                        .OrderBy(f => f)
                        .ToArray();

                    foreach (string roomFile in roomFiles)
                    {
                        // Извлекаем номер комнаты из имени файла
                        string fileName = Path.GetFileNameWithoutExtension(roomFile);
                        if (int.TryParse(fileName, out int roomNumber))
                        {
                            BitmapImage roomImage = new BitmapImage();
                            roomImage.BeginInit();
                            roomImage.UriSource = new Uri(roomFile);
                            roomImage.CacheOption = BitmapCacheOption.OnLoad;
                            roomImage.EndInit();
                            roomImage.Freeze();

                            roomBackgrounds[roomNumber] = roomImage;
                        }
                    }

                    // Устанавливаем фон для первой комнаты
                    if (roomBackgrounds.ContainsKey(1))
                    {
                        RoomBackground.Source = roomBackgrounds[1];
                    }
                    else
                    {
                        // Если нет фонов, используем цветной фон
                        GameCanvas.Background = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(200, 200, 255));
                    }

                    UpdateRoomText();
                }
                else
                {
                    MessageBox.Show($"Папка с комнатами не найдена: {RoomsSpritesPath}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки комнат: {ex.Message}");
            }
        }

        private void UpdateRoomText()
        {
            RoomText.Text = $"Комната: {currentRoom}";
        }

        private async void LoadSpritesAsync()
        {
            try
            {
                await Task.Run(() => LoadAllDirectionSprites());

                // После загрузки устанавливаем начальный спрайт
                Dispatcher.Invoke(() =>
                {
                    if (directionSprites.ContainsKey(Direction.Down) &&
                        directionSprites[Direction.Down].Count > 0)
                    { 
                        PlayerSprite.Source = directionSprites[Direction.Down][0];
                    }
                    DebugText.Text = "Спрайты загружены!";
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                    MessageBox.Show($"Ошибка загрузки спрайтов: {ex.Message}"));
            }
        }

        private void LoadAllDirectionSprites()
        {
            // Папки для каждого направления
            Dictionary<Direction, string> directionFolders = new Dictionary<Direction, string>
            {
                { Direction.Up, Path.Combine(BaseSpritesPath, "forward") },
                { Direction.Down, Path.Combine(BaseSpritesPath, "back") },
                { Direction.Left, Path.Combine(BaseSpritesPath, "left") },
                { Direction.Right, Path.Combine(BaseSpritesPath, "right") }
            };

            foreach (var directionFolder in directionFolders)
            {
                Direction direction = directionFolder.Key;
                string folderPath = directionFolder.Value;

                if (Directory.Exists(folderPath))
                {
                    // Быстрая загрузка с использованием CreateOptions
                    var sprites = LoadSpritesFast(folderPath);
                    directionSprites[direction] = sprites;

                    Dispatcher.Invoke(() =>
                        Console.WriteLine($"Загружено {sprites.Count} спрайтов для {direction}"));
                }
                else
                {
                    Dispatcher.Invoke(() =>
                        MessageBox.Show($"Папка не найдена: {folderPath}"));
                }
            }
        }

        private List<BitmapImage> LoadSpritesFast(string folderPath)
        {
            var sprites = new List<BitmapImage>();

            try
            {
                // Сортируем файлы для правильной последовательности анимации
                var imageFiles = Directory.GetFiles(folderPath, "*.png")
                    .OrderBy(f => f)
                    .ToArray();

                foreach (string file in imageFiles)
                {
                    var sprite = new BitmapImage();
                    sprite.BeginInit();
                    sprite.UriSource = new Uri(file);
                    sprite.CacheOption = BitmapCacheOption.OnLoad; // Кэшируем в памяти
                    sprite.CreateOptions = BitmapCreateOptions.IgnoreImageCache; // Игнорируем кэш файлов
                    sprite.DecodePixelWidth = 64; // Уменьшаем разрешение для скорости
                    sprite.DecodePixelHeight = 64;
                    sprite.EndInit();
                    sprite.Freeze(); // Замораживаем для многопоточного использования

                    sprites.Add(sprite);
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                    Console.WriteLine($"Ошибка загрузки из {folderPath}: {ex.Message}"));
            }

            return sprites;
        }

        private void InitializeAnimation()
        {
            animationTimer = new DispatcherTimer();
            animationTimer.Interval = TimeSpan.FromMilliseconds(80); // 12.5 FPS - быстрее
            animationTimer.Tick += AnimationTimer_Tick;
            animationTimer.Start();
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            UpdateAnimation();
            UpdateMovement();
            UpdateDebugInfo();
        }

        private void UpdateAnimation()
        {
            if (directionSprites.Count == 0) return;

            Direction animationDirection = currentState == PlayerState.Moving ? currentDirection : lastMovingDirection;

            if (directionSprites.ContainsKey(animationDirection) &&
                directionSprites[animationDirection].Count > 0)
            {
                if (currentState == PlayerState.Moving)
                {
                    // Анимация движения - быстрая смена кадров
                    currentSpriteIndex = (currentSpriteIndex + 1) % directionSprites[animationDirection].Count;
                }
                else
                {
                    // Анимация ожидания - медленная смена кадров
                    if (DateTime.Now.Millisecond % 400 == 0)
                    {
                        currentSpriteIndex = (currentSpriteIndex + 1) % directionSprites[animationDirection].Count;
                    }
                }

                PlayerSprite.Source = directionSprites[animationDirection][currentSpriteIndex];
            }
        }

        private void UpdateMovement()
        {
            double newX = Canvas.GetLeft(PlayerSprite);
            double newY = Canvas.GetTop(PlayerSprite);

            currentState = PlayerState.Idle;

            // Обработка движения
            if (isMovingUp)
            {
                newY -= PlayerSpeed;
                currentState = PlayerState.Moving;
                currentDirection = Direction.Up;
                lastMovingDirection = Direction.Up;
            }
            if (isMovingDown)
            {
                newY += PlayerSpeed;
                currentState = PlayerState.Moving;
                currentDirection = Direction.Down;
                lastMovingDirection = Direction.Down;
            }
            if (isMovingLeft)
            {
                newX -= PlayerSpeed;
                currentState = PlayerState.Moving;
                currentDirection = Direction.Left;
                lastMovingDirection = Direction.Left;
            }
            if (isMovingRight)
            {
                newX += PlayerSpeed;
                currentState = PlayerState.Moving;
                currentDirection = Direction.Right;
                lastMovingDirection = Direction.Right;
            }

            // Ограничение движения (используем фиксированные размеры)
            newX = Math.Max(0, Math.Min(canvasWidth - PlayerSprite.Width - 20, newX));
            newY = Math.Max(0, Math.Min(canvasHeight - PlayerSprite.Height - 60, newY));

            Canvas.SetLeft(PlayerSprite, newX);
            Canvas.SetTop(PlayerSprite, newY);
        }

        private void UpdateDebugInfo()
        {
            string stateText = currentState == PlayerState.Moving ? "Движение" : "Ожидание";
            string directionText = currentDirection.ToString();
            string positionText = $"Позиция: ({Canvas.GetLeft(PlayerSprite):F0}, {Canvas.GetTop(PlayerSprite):F0})";

            DebugText.Text = $"Состояние: {stateText} | Направление: {directionText} | {positionText}";
        }

        // Обработка нажатия клавиш
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.W:
                case Key.Up:
                    isMovingUp = true;
                    break;
                case Key.S:
                case Key.Down:
                    isMovingDown = true;
                    break;
                case Key.A:
                case Key.Left:
                    isMovingLeft = true;
                    break;
                case Key.D:
                case Key.Right:
                    isMovingRight = true;
                    break;
            }
        }

        // Обработка отпускания клавиш
        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.W:
                case Key.Up:
                    isMovingUp = false;
                    break;
                case Key.S:
                case Key.Down:
                    isMovingDown = false;
                    break;
                case Key.A:
                case Key.Left:
                    isMovingLeft = false;
                    break;
                case Key.D:
                case Key.Right:
                    isMovingRight = false;
                    break;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            animationTimer?.Stop();
            base.OnClosed(e);
        }
    }
}