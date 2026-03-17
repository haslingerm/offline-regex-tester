using Avalonia.Controls;
using RegexTester.ViewModels;

namespace RegexTester.Views
{
    public partial class RegexToolbox : UserControl
    {
        public RegexToolboxViewModel ViewModel { get; }

        public RegexToolbox()
        {
            ViewModel = new RegexToolboxViewModel();
            DataContext = ViewModel;
            InitializeComponent();
        }
    }
}
