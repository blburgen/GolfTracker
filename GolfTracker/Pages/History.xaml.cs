namespace GolfTracker.Pages;
using GolfTracker.Services;

public partial class History : ContentPage
{
    public History()
    {
        InitializeComponent();
        UpdateHistoryStats();
        DesktopRoundsList.ItemsSource = rounds;
        MobileRoundsList.ItemsSource = rounds;
        DesktopRoundSortPicker.SelectedIndex = 0;
        MobileRoundSortPicker.SelectedIndex = 0;
    }

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        if (Width <= 0)
            return;

        bool isMobile = Width < 800;
        MobileHistory.IsVisible = isMobile;
        DesktopHistory.IsVisible = !isMobile;
    }

    private async void OnNewRoundClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//NewGame");
    }

    private List<GolfRound> rounds = new();
    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadRounds();
    }

    private void LoadRounds()
    {
        rounds = RoundHistoryService.Instance.Rounds.ToList();

        UpdateHistoryStats();

        // Display newest rounds first
        List<GolfRound> sortedRounds = rounds
            .OrderByDescending(r => r.Date)
            .ToList();

        DesktopRoundsList.ItemsSource = sortedRounds;
        MobileRoundsList.ItemsSource = sortedRounds;

        // Select the default sorting option
        DesktopRoundSortPicker.SelectedIndex = 0;
        MobileRoundSortPicker.SelectedIndex = 0;
    }


    private void UpdateHistoryStats()
    {
        int total = rounds.Count;

        double average = rounds.Count > 0
            ? rounds.Average(r => r.TotalStrokes)
            : 0;

        int bestScore = rounds.Count > 0
            ? rounds.Min(r => r.TotalStrokes)
            : 0;

        totalRounds.Text = total.ToString();
        totalRoundsMobile.Text = total.ToString();

        averageScore.Text = average.ToString("F1");
        averageScoreMobile.Text = average.ToString("F1");

        best.Text = bestScore.ToString();
        bestMobile.Text = bestScore.ToString();
    }

    private void OnSortChanged(object sender, EventArgs e)
    {
        if (sender is not Picker picker)
            return;

        List<GolfRound> sortedRounds = picker.SelectedIndex switch
        {
            0 => rounds.OrderByDescending(r => r.Date).ToList(),
            1 => rounds.OrderBy(r => r.Date).ToList(),
            2 => rounds.OrderBy(r => r.TotalStrokes).ToList(),
            3 => rounds.OrderByDescending(r => r.TotalStrokes).ToList(),
            4 => rounds.OrderBy(r => r.CourseName).ToList(),
            5 => rounds.OrderByDescending(r => r.CourseName).ToList(),
            _ => rounds.OrderByDescending(r => r.Date).ToList()
        };

        DesktopRoundsList.ItemsSource = sortedRounds;
        MobileRoundsList.ItemsSource = sortedRounds;
    }


}