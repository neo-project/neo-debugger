using Microsoft.VisualStudio.PlatformUI;

namespace NeoDebug.VS
{
    public partial class LaunchConfigSelectionDialog : DialogWindow
    {
        public LaunchConfigSelectionDialog()
        {
            InitializeComponent();
        }

        private void okButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void cancelButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
