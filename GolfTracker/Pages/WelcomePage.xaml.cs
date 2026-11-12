namespace GolfTracker.Pages;

public partial class WelcomePage : ContentPage
{
    public WelcomePage()
    {
        InitializeComponent();
    }

    private async void OnNewGameClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//NewGame");
    }

    private async void OnSignInClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//SignIn");
    }


    private bool? _isMobileLayout;

    private void OnPageSizeChanged(object sender, EventArgs e)
    {
        if (Width <= 0)
            return;

        bool isMobile = Width < 800;

        // Don't rebuild the layout unnecessarily.
        if (_isMobileLayout == isMobile)
            return;

        _isMobileLayout = isMobile;

        if (isMobile)
        {
            // WELCOME SECTION: one column, two rows.
            WelcomeGrid.ColumnDefinitions =
                new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = GridLength.Star }
                };

            WelcomeGrid.RowDefinitions =
                new RowDefinitionCollection
                {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto }
                };

            WelcomeGrid.MinimumHeightRequest = 0;

            Grid.SetRow(WelcomeText, 0);
            Grid.SetColumn(WelcomeText, 0);

            Grid.SetRow(WelcomeImage, 1);
            Grid.SetColumn(WelcomeImage, 0);

            WelcomeImage.HeightRequest = 300;

            // FEATURES SECTION: one column, five rows.
            FeaturesGrid.ColumnDefinitions =
                new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = GridLength.Star }
                };

            FeaturesGrid.RowDefinitions =
                new RowDefinitionCollection
                {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto }
                };

            Grid.SetRow(RecordSection, 1);
            Grid.SetColumn(RecordSection, 0);

            Grid.SetRow(ImproveSection, 2);
            Grid.SetColumn(ImproveSection, 0);

            Grid.SetRow(HistorySection, 3);
            Grid.SetColumn(HistorySection, 0);

            Grid.SetRow(BottomDivider, 4);
            Grid.SetColumn(BottomDivider, 0);

            Grid.SetColumnSpan(TopDivider, 1);
            Grid.SetColumnSpan(BottomDivider, 1);
        }
        else
        {
            // WELCOME SECTION: two equal columns.
            WelcomeGrid.ColumnDefinitions =
                new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star }
                };

            WelcomeGrid.RowDefinitions =
                new RowDefinitionCollection
                {
                    new RowDefinition { Height = GridLength.Auto }
                };

            WelcomeGrid.MinimumHeightRequest = 450;

            Grid.SetRow(WelcomeText, 0);
            Grid.SetColumn(WelcomeText, 0);

            Grid.SetRow(WelcomeImage, 0);
            Grid.SetColumn(WelcomeImage, 1);

            WelcomeImage.HeightRequest = -1;

            // FEATURES SECTION: three equal columns.
            FeaturesGrid.ColumnDefinitions =
                new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star }
                };

            FeaturesGrid.RowDefinitions =
                new RowDefinitionCollection
                {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto }
                };

            Grid.SetRow(RecordSection, 1);
            Grid.SetColumn(RecordSection, 0);

            Grid.SetRow(ImproveSection, 1);
            Grid.SetColumn(ImproveSection, 1);

            Grid.SetRow(HistorySection, 1);
            Grid.SetColumn(HistorySection, 2);

            Grid.SetRow(BottomDivider, 2);
            Grid.SetColumn(BottomDivider, 0);

            Grid.SetColumnSpan(TopDivider, 3);
            Grid.SetColumnSpan(BottomDivider, 3);
        }
    }
}