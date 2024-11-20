using System.Windows;
using System.Windows.Input;

namespace MultiToolLoader.Dialogs
{
    public partial class CustomPromptDialog : Window
    {
        public string PromptName => PromptNameTextBox.Text;
        public string PromptText => PromptTextBox.Text;

        public CustomPromptDialog()
        {
            InitializeComponent();
            PromptNameTextBox.Focus();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PromptName))
            {
                MessageBox.Show("Bitte geben Sie einen Namen für den Prompt ein.",
                                "Fehlender Name",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(PromptText))
            {
                MessageBox.Show("Bitte geben Sie einen Text für den Prompt ein.",
                                "Fehlender Text",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return;
            }

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
    }
}