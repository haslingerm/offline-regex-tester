using Avalonia.Controls;
using RegexTester.ViewModels;

namespace RegexTester.Views;

public partial class RegexToolbox : UserControl
{
    public RegexToolbox()
    {
        ViewModel = new RegexToolboxViewModel();
        DataContext = ViewModel;
        InitializeComponent();
    }

    public RegexToolboxViewModel ViewModel { get; }
}
