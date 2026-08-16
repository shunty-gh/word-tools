using Words.Maui.ViewModels;

namespace Words.Maui;

public partial class MainPage : ContentPage
{
    public MainPage(SearchViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private async void OnAboutClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(new AboutPage());
}
