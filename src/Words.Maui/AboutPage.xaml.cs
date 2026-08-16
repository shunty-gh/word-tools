using Words.Core;

namespace Words.Maui;

public partial class AboutPage : ContentPage
{
    public AboutPage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    /// <summary>
    /// The bundled word lists' terms. Displaying these is an obligation of both licences,
    /// not a nicety — see docs/adr/0004.
    /// </summary>
    public IReadOnlyList<LexiconLicence> Licences => LexiconLicences.All;
}
