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
    /// The app's own terms. Apache 2.0 requires a copy of the licence to reach whoever the
    /// app is distributed to, and a store listing is not that — this screen is.
    /// </summary>
    public Licence AppLicence => Licences.Program;

    /// <summary>
    /// The bundled word lists' terms. Displaying these is an obligation of both licences,
    /// not a nicety — see docs/adr/0004.
    /// </summary>
    public IReadOnlyList<Licence> WordListLicences => Licences.WordLists;
}
