using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Newtonsoft.Json;

namespace FloatingNoteApp
{
    public partial class MainWindow : Window
    {
        private bool _isPanelOpen = false;
        private bool _isDragging = false;
        private Point _lastPosition;
        private string _notesFilePath = "notes.json";
        private DispatcherTimer _edgeSnapTimer;
        private double _originalLeft;
        private double _originalTop;

        public MainWindow()
        {
            InitializeComponent();
            InitializeWindow();
            LoadNotes();
            SetupEdgeSnapping();
        }

        private void InitializeWindow()
        {
            // Pencereyi ekranın sağ kenarına yerleştir
            var workingArea = SystemParameters.WorkArea;
            this.Left = workingArea.Right - this.Width - 10;
            this.Top = workingArea.Height / 2 - this.Height / 2;
        }

        private void SetupEdgeSnapping()
        {
            _edgeSnapTimer = new DispatcherTimer();
            _edgeSnapTimer.Interval = TimeSpan.FromMilliseconds(100);
            _edgeSnapTimer.Tick += EdgeSnapTimer_Tick;
        }

        #region Floating Button Events

        private void FloatingButton_MouseEnter(object sender, MouseEventArgs e)
        {
            // Hover efekti
            var scaleTransform = new ScaleTransform(1.1, 1.1);
            FloatingButton.RenderTransform = scaleTransform;
            FloatingButton.RenderTransformOrigin = new Point(0.5, 0.5);

            var animation = new DoubleAnimation(1.0, 1.1, TimeSpan.FromMilliseconds(200));
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }

        private void FloatingButton_MouseLeave(object sender, MouseEventArgs e)
        {
            // Normal boyuta dön
            var scaleTransform = FloatingButton.RenderTransform as ScaleTransform ?? new ScaleTransform();
            var animation = new DoubleAnimation(scaleTransform.ScaleX, 1.0, TimeSpan.FromMilliseconds(200));
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }

        private void FloatingButton_Click(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging && !_isPanelOpen)
            {
                ToggleNotePanel();
            }
        }

        #endregion

        #region Window Dragging

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                _isDragging = true;
                _lastPosition = e.GetPosition(this);
                this.CaptureMouse();
                this.MouseMove += Window_MouseMove;
                this.MouseUp += Window_MouseUp;
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPosition = e.GetPosition(this);
                var offset = currentPosition - _lastPosition;

                this.Left += offset.X;
                this.Top += offset.Y;
            }
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                this.ReleaseMouseCapture();
                this.MouseMove -= Window_MouseMove;
                this.MouseUp -= Window_MouseUp;

                // Kenar yapışma başlat
                _edgeSnapTimer.Start();
            }
        }

        #endregion

        #region Edge Snapping

        private void EdgeSnapTimer_Tick(object sender, EventArgs e)
        {
            _edgeSnapTimer.Stop();
            SnapToEdge();
        }

        private void SnapToEdge()
        {
            var workingArea = SystemParameters.WorkArea;
            var snapDistance = 50; // Kenardan ne kadar uzakta olursa yapışsın

            double targetLeft = this.Left;
            double targetTop = this.Top;

            // Sol kenar kontrolü
            if (this.Left < snapDistance)
            {
                targetLeft = 10;
            }
            // Sağ kenar kontrolü
            else if (this.Left + this.Width > workingArea.Width - snapDistance)
            {
                targetLeft = workingArea.Width - this.Width - 10;
            }

            // Üst kenar kontrolü
            if (this.Top < snapDistance)
            {
                targetTop = 10;
            }
            // Alt kenar kontrolü
            else if (this.Top + this.Height > workingArea.Height - snapDistance)
            {
                targetTop = workingArea.Height - this.Height - 10;
            }

            // Animasyonlu hareket
            var leftAnimation = new DoubleAnimation(this.Left, targetLeft, TimeSpan.FromMilliseconds(300));
            var topAnimation = new DoubleAnimation(this.Top, targetTop, TimeSpan.FromMilliseconds(300));

            leftAnimation.EasingFunction = new QuadraticEase();
            topAnimation.EasingFunction = new QuadraticEase();

            this.BeginAnimation(Window.LeftProperty, leftAnimation);
            this.BeginAnimation(Window.TopProperty, topAnimation);
        }

        #endregion

        #region Note Panel

        private void ToggleNotePanel()
        {
            if (_isPanelOpen)
            {
                CloseNotePanel();
            }
            else
            {
                OpenNotePanel();
            }
        }

        private void OpenNotePanel()
        {
            _isPanelOpen = true;

            // Mevcut konumu kaydet
            _originalLeft = this.Left;
            _originalTop = this.Top;

            NotePanel.Visibility = Visibility.Visible;

            // Pencere boyutları
            var currentWidth = this.Width;
            var currentHeight = this.Height;
            var targetWidth = 380.0;
            var targetHeight = 400.0;

            // Yeni konumu hesapla - butonun merkezi sabit kalacak şekilde
            var newLeft = _originalLeft - (targetWidth - currentWidth) / 2;
            var newTop = _originalTop - (targetHeight - currentHeight) / 2;

            // Ekran sınırlarını kontrol et
            var workingArea = SystemParameters.WorkArea;
            if (newLeft < 0) newLeft = 10;
            if (newTop < 0) newTop = 10;
            if (newLeft + targetWidth > workingArea.Width) newLeft = workingArea.Width - targetWidth - 10;
            if (newTop + targetHeight > workingArea.Height) newTop = workingArea.Height - targetHeight - 10;

            // Panel başlangıçta buton boyutunda, ortadan başlayacak
            var scaleTransform = new ScaleTransform(0.2, 0.2);
            NotePanel.RenderTransform = scaleTransform;
            NotePanel.RenderTransformOrigin = new Point(0.5, 0.5); // Ortadan büyüsün

            // Buton yavaşça kaybolsun
            var buttonOpacityAnimation = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));

            // Pencere boyut ve konum animasyonları
            var widthAnimation = new DoubleAnimation(currentWidth, targetWidth, TimeSpan.FromMilliseconds(400));
            var heightAnimation = new DoubleAnimation(currentHeight, targetHeight, TimeSpan.FromMilliseconds(400));
            var leftAnimation = new DoubleAnimation(this.Left, newLeft, TimeSpan.FromMilliseconds(400));
            var topAnimation = new DoubleAnimation(this.Top, newTop, TimeSpan.FromMilliseconds(400));

            widthAnimation.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            heightAnimation.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            leftAnimation.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            topAnimation.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };

            // Panel büyüme animasyonu
            var scaleXAnimation = new DoubleAnimation(0.2, 1, TimeSpan.FromMilliseconds(500));
            var scaleYAnimation = new DoubleAnimation(0.2, 1, TimeSpan.FromMilliseconds(500));

            scaleXAnimation.EasingFunction = new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut };
            scaleYAnimation.EasingFunction = new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut };

            // Animasyonları başlat
            FloatingButton.BeginAnimation(UIElement.OpacityProperty, buttonOpacityAnimation);

            this.BeginAnimation(Window.WidthProperty, widthAnimation);
            this.BeginAnimation(Window.HeightProperty, heightAnimation);
            this.BeginAnimation(Window.LeftProperty, leftAnimation);
            this.BeginAnimation(Window.TopProperty, topAnimation);

            // Panel animasyonunu biraz gecikmeli başlat
            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(100);
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnimation);
                scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnimation);
            };
            timer.Start();

            // Focus textbox'a
            var focusTimer = new DispatcherTimer();
            focusTimer.Interval = TimeSpan.FromMilliseconds(600);
            focusTimer.Tick += (s, e) =>
            {
                focusTimer.Stop();
                NoteTextBox.Focus();
                if (NoteTextBox.Text == "Notunuzu buraya yazın...")
                {
                    NoteTextBox.SelectAll();
                }
            };
            focusTimer.Start();
        }

        private void CloseNotePanel()
        {
            _isPanelOpen = false;

            // Panel küçülme animasyonu
            var scaleTransform = NotePanel.RenderTransform as ScaleTransform;
            if (scaleTransform == null)
            {
                scaleTransform = new ScaleTransform(1, 1);
                NotePanel.RenderTransform = scaleTransform;
            }

            var scaleXAnimation = new DoubleAnimation(scaleTransform.ScaleX, 0.2, TimeSpan.FromMilliseconds(300));
            var scaleYAnimation = new DoubleAnimation(scaleTransform.ScaleY, 0.2, TimeSpan.FromMilliseconds(300));

            scaleXAnimation.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn };
            scaleYAnimation.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn };

            // Pencere eski boyut ve konumuna dönme animasyonları
            var widthAnimation = new DoubleAnimation(this.Width, 80, TimeSpan.FromMilliseconds(400));
            var heightAnimation = new DoubleAnimation(this.Height, 80, TimeSpan.FromMilliseconds(400));
            var leftAnimation = new DoubleAnimation(this.Left, _originalLeft, TimeSpan.FromMilliseconds(400));
            var topAnimation = new DoubleAnimation(this.Top, _originalTop, TimeSpan.FromMilliseconds(400));

            widthAnimation.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn };
            heightAnimation.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn };
            leftAnimation.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn };
            topAnimation.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn };

            // Buton görünür yapma animasyonu
            var buttonOpacityAnimation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            buttonOpacityAnimation.BeginTime = TimeSpan.FromMilliseconds(200);

            // Panel gizleme
            scaleXAnimation.Completed += (s, e) =>
            {
                NotePanel.Visibility = Visibility.Collapsed;
                // Transform'u sıfırla
                NotePanel.RenderTransform = new ScaleTransform(1, 1);
            };

            // Animasyonları başlat
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnimation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnimation);

            // Pencere animasyonlarını biraz gecikmeli başlat
            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(100);
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                this.BeginAnimation(Window.WidthProperty, widthAnimation);
                this.BeginAnimation(Window.HeightProperty, heightAnimation);
                this.BeginAnimation(Window.LeftProperty, leftAnimation);
                this.BeginAnimation(Window.TopProperty, topAnimation);
                FloatingButton.BeginAnimation(UIElement.OpacityProperty, buttonOpacityAnimation);
            };
            timer.Start();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseNotePanel();
        }

        #endregion

        #region Note Management

        public class NoteData
        {
            public string Content { get; set; } = "";
            public DateTime LastModified { get; set; } = DateTime.Now;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveNotes();
        }

        private void SaveNotes()
        {
            try
            {
                var noteData = new NoteData
                {
                    Content = NoteTextBox.Text,
                    LastModified = DateTime.Now
                };

                var json = JsonConvert.SerializeObject(noteData, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(_notesFilePath, json);

                MessageBox.Show("Not kaydedildi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Not kaydedilemedi: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadNotes()
        {
            try
            {
                if (File.Exists(_notesFilePath))
                {
                    var json = File.ReadAllText(_notesFilePath);
                    var noteData = JsonConvert.DeserializeObject<NoteData>(json);

                    if (noteData != null && !string.IsNullOrEmpty(noteData.Content))
                    {
                        NoteTextBox.Text = noteData.Content;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Notlar yüklenemedi: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            SaveNotes(); // Uygulama kapanırken otomatik kaydet
            base.OnClosed(e);
        }
    }
}