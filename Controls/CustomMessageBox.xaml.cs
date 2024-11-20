using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace MultiToolLoader.Controls
{
    public partial class CustomMessageBox : Window, INotifyPropertyChanged
    {
        private string _title = string.Empty;
        private string _message = string.Empty;
        private bool _showCancel;

        public string Title
        {
            get => _title;
            set
            {
                _title = value;
                OnPropertyChanged();
            }
        }

        public string Message
        {
            get => _message;
            set
            {
                _message = value;
                OnPropertyChanged();
            }
        }

        public bool ShowCancel
        {
            get => _showCancel;
            set
            {
                _showCancel = value;
                OnPropertyChanged();
            }
        }

        public CustomMessageBox(string title, string message, bool showCancel = false)
        {
            InitializeComponent();
            DataContext = this;

            Title = title;
            Message = message;
            ShowCancel = showCancel;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            DragMove();
        }

        public static bool Show(string title, string message, bool isQuestion = false)
        {
            var msgBox = new CustomMessageBox(title, message, isQuestion);
            return msgBox.ShowDialog() ?? false;
        }

        public static void ShowInfo(string title, string message)
        {
            Show(title, message, false);
        }

        public static bool ShowQuestion(string title, string message)
        {
            return Show(title, message, true);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}