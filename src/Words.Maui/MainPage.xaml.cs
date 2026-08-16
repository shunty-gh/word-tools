using Words.Maui.ViewModels;

namespace Words.Maui;

public partial class MainPage : ContentPage
{
    private readonly SearchViewModel _viewModel;

    public MainPage(SearchViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    private async void OnAboutClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(new AboutPage());

    /// <summary>
    /// Prompting is a view concern, so the page asks and the view model does the work.
    /// </summary>
    private async void OnAddWordClicked(object? sender, EventArgs e)
    {
        var word = await DisplayPromptAsync(
            "Add a word",
            "Words you add are searched alongside the built-in lists.",
            accept: "Add",
            placeholder: "A word or phrase");

        await _viewModel.AddWordAsync(word);
    }
}
