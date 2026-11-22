
namespace GolfTracker.Pages;

using GolfTracker.Services;
using GolfTracker.Data;
using GolfTracker.Models;

public partial class History : ContentPage
{
    private readonly GolfDatabase _database;
    private List<Round> rounds = new();

    public History(GolfDatabase database)
    {
        InitializeComponent();
        _database = database;
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

    protected async override void OnAppearing()
    {
        base.OnAppearing();
        await LoadRoundsAsync();
        // Wait for Microsoft session restoration to finish.
        if (Shell.Current is AppShell appShell)
        {
            await appShell.WaitForSessionRestoreAsync();
        }

        // Check if the user is signed in.
        bool isGuest = Preferences.Default.Get("GuestMode", true);

        if (isGuest)
        {
            await DisplayAlertAsync(
                "Sign In Required",
                "Please sign in with Microsoft to view your game history.",
                "OK");

            await Shell.Current.GoToAsync("//SignIn");
        }
    }

    private async Task LoadRoundsAsync()
{
    rounds = await _database.GetRoundsAsync();
    UpdateHistoryStats();

    var sortedRounds = rounds
        .OrderByDescending(r => r.DatePlayed)
        .ToList();

    DesktopRoundsList.ItemsSource = sortedRounds;
    MobileRoundsList.ItemsSource = sortedRounds;

    DesktopRoundSortPicker.SelectedIndex = 0;
    MobileRoundSortPicker.SelectedIndex = 0;
}



    private void UpdateHistoryStats()
    {
        int total = rounds.Count;

        List<Round> scoredRounds = rounds
            .Where(r => r.NumberOfHoles > 0)
            .ToList();

        double average = scoredRounds.Count > 0
            ? scoredRounds.Average(r => r.TotalStrokes)
            : 0;

        int bestScore = scoredRounds.Count > 0
            ? scoredRounds.Min(r => r.TotalStrokes)
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

        List<Round> sortedRounds = picker.SelectedIndex switch
        {
            0 => rounds.OrderByDescending(r => r.DatePlayed).ToList(),
1 => rounds.OrderBy(r => r.DatePlayed).ToList(),
            2 => rounds.OrderBy(r => r.TotalStrokes).ToList(),
            3 => rounds.OrderByDescending(r => r.TotalStrokes).ToList(),
            4 => rounds.OrderBy(r => r.CourseName).ToList(),
            5 => rounds.OrderByDescending(r => r.CourseName).ToList(),
            _ => rounds.OrderByDescending(r => r.DatePlayed).ToList()
        };

        if (DesktopRoundSortPicker.SelectedIndex != picker.SelectedIndex)
            DesktopRoundSortPicker.SelectedIndex = picker.SelectedIndex;
        if (MobileRoundSortPicker.SelectedIndex != picker.SelectedIndex)
            MobileRoundSortPicker.SelectedIndex = picker.SelectedIndex;

        DesktopRoundsList.ItemsSource = sortedRounds;
        MobileRoundsList.ItemsSource = sortedRounds;
    }
}
