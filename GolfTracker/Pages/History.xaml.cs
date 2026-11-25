
using GolfTracker.Data;
using GolfTracker.Models;
using LiveChartsCore;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace GolfTracker.Pages;

public partial class History : ContentPage
{
    private readonly GolfDatabase _database;

    private List<Round> rounds = new();
    private List<Round> chartRounds = new();

    private bool initializingProgressFilters = false;
    private bool? progressFiltersStacked;
    private int chartUpdateVersion = 0;

    public History(GolfDatabase database)
    {
        InitializeComponent();
        _database = database;
    }

    // --------------------------------------------------
    // RESPONSIVE LAYOUT
    // --------------------------------------------------

    private void OnPageSizeChanged(
        object? sender,
        EventArgs e)
    {
        if (Width <= 0)
            return;

        bool isMobile = Width < 800;

        MobileHistory.IsVisible = isMobile;
        DesktopHistory.IsVisible = !isMobile;

        UpdateProgressFilterLayout();
    }

    // --------------------------------------------------
    // RESPONSIVE PROGRESS FILTERS
    // --------------------------------------------------

    private void UpdateProgressFilterLayout()
    {
        if (Width <= 0)
            return;

        bool shouldStack = Width < 900;

        if (progressFiltersStacked == shouldStack)
            return;

        progressFiltersStacked = shouldStack;

        if (shouldStack)
        {
            // One column and three rows.
            ProgressFiltersGrid.ColumnDefinitions =
                new ColumnDefinitionCollection
                {
                    new ColumnDefinition
                    {
                        Width = GridLength.Star
                    }
                };

            ProgressFiltersGrid.RowDefinitions =
                new RowDefinitionCollection
                {
                    new RowDefinition
                    {
                        Height = GridLength.Auto
                    },
                    new RowDefinition
                    {
                        Height = GridLength.Auto
                    },
                    new RowDefinition
                    {
                        Height = GridLength.Auto
                    }
                };

            Grid.SetRow(ProgressMetricFilter, 0);
            Grid.SetColumn(ProgressMetricFilter, 0);

            Grid.SetRow(ProgressHolesFilter, 1);
            Grid.SetColumn(ProgressHolesFilter, 0);

            Grid.SetRow(ProgressTimeFilter, 2);
            Grid.SetColumn(ProgressTimeFilter, 0);
        }
        else
        {
            // Three columns and one row.
            ProgressFiltersGrid.ColumnDefinitions =
                new ColumnDefinitionCollection
                {
                    new ColumnDefinition
                    {
                        Width = GridLength.Star
                    },
                    new ColumnDefinition
                    {
                        Width = GridLength.Star
                    },
                    new ColumnDefinition
                    {
                        Width = GridLength.Star
                    }
                };

            ProgressFiltersGrid.RowDefinitions =
                new RowDefinitionCollection
                {
                    new RowDefinition
                    {
                        Height = GridLength.Auto
                    }
                };

            Grid.SetRow(ProgressMetricFilter, 0);
            Grid.SetColumn(ProgressMetricFilter, 0);

            Grid.SetRow(ProgressHolesFilter, 0);
            Grid.SetColumn(ProgressHolesFilter, 1);

            Grid.SetRow(ProgressTimeFilter, 0);
            Grid.SetColumn(ProgressTimeFilter, 2);
        }
    }

    // --------------------------------------------------
    // NEW ROUND NAVIGATION
    // --------------------------------------------------

    private async void OnNewRoundClicked(
        object? sender,
        EventArgs e)
    {
        await Shell.Current.GoToAsync("//NewGame");
    }

    // --------------------------------------------------
    // PAGE LOADING
    // --------------------------------------------------

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await LoadRoundsAsync();

        // Preserve existing sign-in behavior.
        if (Shell.Current is AppShell appShell)
        {
            await appShell.WaitForSessionRestoreAsync();
        }

        bool isGuest =
            Preferences.Default.Get("GuestMode", true);

        if (isGuest)
        {
            await DisplayAlertAsync(
                "Sign In Required",
                "Please sign in with Microsoft to view your game history.",
                "OK");

            await Shell.Current.GoToAsync("//SignIn");
        }
    }

    // --------------------------------------------------
    // LOAD SAVED ROUNDS
    // --------------------------------------------------

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

    // --------------------------------------------------
    // OPEN PROGRESS POPUP
    // --------------------------------------------------

    private async void OnViewProgressClicked(
        object? sender,
        EventArgs e)
    {
        UpdateProgressFilterLayout();

        // Prevent multiple chart refreshes while
        // setting the initial filters.
        initializingProgressFilters = true;

        ProgressMetricPicker.SelectedIndex = 2;
        ProgressHolesPicker.SelectedIndex = 0;
        ProgressTimePicker.SelectedIndex = 0;

        initializingProgressFilters = false;

        // Display the graph popup.
        ViewProgressOverlay.IsVisible = true;

        await UpdateProgressChartAsync();
    }

    // --------------------------------------------------
    // PROGRESS FILTER CHANGES
    // --------------------------------------------------

    private async void OnProgressFiltersChanged(
        object? sender,
        EventArgs e)
    {
        if (initializingProgressFilters)
            return;

        await UpdateProgressChartAsync();
    }

    // --------------------------------------------------
    // UPDATE PROGRESS CHART
    // --------------------------------------------------

    private async Task UpdateProgressChartAsync()
    {
        int metric = ProgressMetricPicker.SelectedIndex;
        int length = ProgressHolesPicker.SelectedIndex;
        int time = ProgressTimePicker.SelectedIndex;

        if (metric < 0 || length < 0 || time < 0)
            return;

        // Prevent older filter requests from replacing
        // the results of a newer request.
        int currentVersion = ++chartUpdateVersion;

        var filteredRounds = rounds.AsEnumerable();

        // Filter by number of holes.
        if (length == 1)
        {
            filteredRounds = filteredRounds
                .Where(r => r.NumberOfHoles == 9);
        }
        else if (length == 2)
        {
            filteredRounds = filteredRounds
                .Where(r => r.NumberOfHoles == 18);
        }

        // Sort newest first before applying time limit.
        filteredRounds = filteredRounds
            .OrderByDescending(r => r.DatePlayed)
            .ThenByDescending(r => r.ID);

        // Last 10 rounds.
        if (time == 1)
        {
            filteredRounds = filteredRounds.Take(10);
        }

        // Graph should display from oldest to newest.
        var selectedRounds = filteredRounds
            .OrderBy(r => r.DatePlayed)
            .ThenBy(r => r.ID)
            .ToList();

        List<double> values = new();
        List<string> labels = new();

        // Each plotted value has a matching Round.
        List<Round> matchingRounds = new();

        foreach (var round in selectedRounds)
        {
            var scores = await _database
                .GetHoleScoresForRoundAsync(round.ID);

            var completed = scores
                .Where(s => s.Strokes.HasValue)
                .ToList();

            // Ignore rounds with no entered strokes.
            if (completed.Count == 0)
                continue;

            // Total Strokes compares completed rounds only.
            if (metric == 0 &&
                completed.Count != round.NumberOfHoles)
            {
                continue;
            }

            double value;

            switch (metric)
            {
                case 1:
                    // Relative to Par.
                    // Par is required for each scored hole.
                    if (completed.Any(s => !s.Par.HasValue))
                        continue;

                    int totalStrokes = completed
                        .Sum(s => s.Strokes ?? 0);

                    int totalPar = completed
                        .Sum(s => s.Par ?? 0);

                    value = totalStrokes - totalPar;
                    break;

                case 2:
                    // Strokes per Hole.
                    // Allows partially completed rounds.
                    value = completed
                        .Average(s => s.Strokes!.Value);
                    break;

                default:
                    // Total Strokes.
                    value = completed
                        .Sum(s => s.Strokes ?? 0);
                    break;
            }

            values.Add(value);

            labels.Add(
                round.DatePlayed.ToString("MMM d"));

            // Keep round order identical to point order.
            matchingRounds.Add(round);
        }

        // Discard an outdated chart update.
        if (currentVersion != chartUpdateVersion)
            return;

        // Store the mapping used by dot clicks.
        chartRounds = matchingRounds;

        // Empty chart handling.
        ProgressEmptyLabel.IsVisible = values.Count == 0;
        ProgressChart.IsVisible = values.Count > 0;

        ProgressDescriptionLabel.Text = metric switch
        {
            0 => "Lower total strokes generally means a better score on comparable rounds. Only complete rounds are included.",

            1 => "Relative to par compares recorded strokes with the par of completed holes.",

            _ => "Strokes per hole helps compare rounds of different lengths, including rounds ended early."
        };

        // X axis.
        ProgressChart.XAxes = new Axis[]
        {
            new Axis
            {
                Labels = labels.ToArray(),
                LabelsRotation = 20,
                MinStep = 1,

                LabelsPaint = new SolidColorPaint(
                    SKColor.Parse("#26332C")),

                SeparatorsPaint = new SolidColorPaint(
                    SKColor.Parse("#DFE8DE"))
            }
        };

        // Y axis.
        ProgressChart.YAxes = new Axis[]
        {
            new Axis
            {
                LabelsPaint = new SolidColorPaint(
                    SKColor.Parse("#26332C")),

                SeparatorsPaint = new SolidColorPaint(
                    SKColor.Parse("#DFE8DE"))
            }
        };

        // Chart series.
        ProgressChart.Series = new ISeries[]
        {
            new LineSeries<double>
            {
                Values = values.ToArray(),

                Fill = null,

                Stroke = new SolidColorPaint(
                    SKColor.Parse("#174D38"), 3),

                GeometryFill = new SolidColorPaint(
                    SKColor.Parse("#C5A45D")),

                GeometryStroke = new SolidColorPaint(
                    SKColor.Parse("#174D38"), 2),

                GeometrySize = 16,

                LineSmoothness = 0.3
            }
        };
    }

    // --------------------------------------------------
    // CLICK A GRAPH DOT
    // --------------------------------------------------

    private async void OnProgressPointClicked(
        IChartView chart,
        IEnumerable<ChartPoint> points)
    {
        var point = points.FirstOrDefault();

        if (point == null)
            return;

        // LiveCharts2 exposes the point index directly.
        int index = point.Index;

        if (index < 0 || index >= chartRounds.Count)
            return;

        Round selectedRound = chartRounds[index];

        // IMPORTANT:
        // Do NOT hide ViewProgressOverlay here.
        //
        // The graph stays open underneath the
        // Round Summary popup.
        await ShowRoundSummaryAsync(selectedRound);
    }

    // --------------------------------------------------
    // CLICK A ROUND IN HISTORY
    // --------------------------------------------------

    private async void OnRoundSelected(
        object? sender,
        SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault()
            is not Round selectedRound)
        {
            return;
        }

        // Allow reopening the same round.
        if (sender is CollectionView collection)
        {
            collection.SelectedItem = null;
        }

        await ShowRoundSummaryAsync(selectedRound);
    }

    // --------------------------------------------------
    // SHARED ROUND SUMMARY
    // --------------------------------------------------

    private async Task ShowRoundSummaryAsync(
        Round selectedRound)
    {
        try
        {
            var holeScores = await _database
                .GetHoleScoresForRoundAsync(selectedRound.ID);

            // Number of scored holes.
            int completedHoles = holeScores
                .Count(h => h.Strokes.HasValue);

            // Total strokes.
            int totalStrokes = holeScores
                .Sum(h => h.Strokes ?? 0);

            // Par for scored holes only.
            int completedPar = holeScores
                .Where(h => h.Strokes.HasValue)
                .Sum(h => h.Par ?? 0);

            // Difference from par.
            int relativeToPar =
                totalStrokes - completedPar;

            // Verify that scored holes have par data.
            bool hasCompletePar =
                completedHoles > 0 &&
                holeScores
                    .Where(h => h.Strokes.HasValue)
                    .All(h => h.Par.HasValue);

            // Populate summary labels.
            HistoryPopupCourse.Text =
                selectedRound.CourseName;

            HistoryPopupDetails.Text =
                $"{selectedRound.DatePlayed:MMMM d, yyyy} · {selectedRound.CourseType}";

            HistoryPopupStrokes.Text =
                totalStrokes.ToString();

            if (hasCompletePar)
            {
                HistoryPopupPar.Text =
                    relativeToPar > 0
                        ? $"+{relativeToPar}"
                        : relativeToPar.ToString();
            }
            else
            {
                HistoryPopupPar.Text = "—";
            }

            HistoryPopupHoles.Text =
                $"{completedHoles} of {selectedRound.NumberOfHoles} holes completed";

            // Display the Round Summary above the graph.
            // The graph remains visible underneath.
            HistoryRoundOverlay.IsVisible = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);

            await DisplayAlertAsync(
                "Round Loading Error",
                "This round's details could not be loaded.",
                "OK");
        }
    }

    // --------------------------------------------------
    // CLOSE ROUND SUMMARY
    // --------------------------------------------------

    private void OnCloseHistoryPopupClicked(
        object? sender,
        EventArgs e)
    {
        // Only hide the Round Summary.
        //
        // If the graph was open, it will still be
        // underneath, with the same filters.
        //
        // If the summary was opened from the History
        // list, the History page will be visible.
        HistoryRoundOverlay.IsVisible = false;
    }

    // --------------------------------------------------
    // CLOSE PROGRESS GRAPH
    // --------------------------------------------------

    private void OnCloseProgressPopupClicked(
        object? sender,
        EventArgs e)
    {
        // Prevent a chart refresh still in progress
        // from updating the chart after it closes.
        chartUpdateVersion++;

        ViewProgressOverlay.IsVisible = false;
    }

    // --------------------------------------------------
    // UPDATE HISTORY STATISTICS
    // --------------------------------------------------

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

        // Desktop statistics.
        totalRounds.Text = total.ToString();
        averageScore.Text = average.ToString("F1");
        best.Text = bestScore.ToString();

        // Mobile statistics.
        totalRoundsMobile.Text = total.ToString();
        averageScoreMobile.Text = average.ToString("F1");
        bestMobile.Text = bestScore.ToString();
    }

    // --------------------------------------------------
    // SORT PREVIOUS ROUNDS
    // --------------------------------------------------

    private void OnSortChanged(
        object? sender,
        EventArgs e)
    {
        if (sender is not Picker picker)
            return;

        List<Round> sortedRounds =
            picker.SelectedIndex switch
            {
                0 => rounds
                    .OrderByDescending(r => r.DatePlayed)
                    .ToList(),

                1 => rounds
                    .OrderBy(r => r.DatePlayed)
                    .ToList(),

                2 => rounds
                    .OrderBy(r => r.TotalStrokes)
                    .ToList(),

                3 => rounds
                    .OrderByDescending(r => r.TotalStrokes)
                    .ToList(),

                4 => rounds
                    .OrderBy(r => r.CourseName)
                    .ToList(),

                5 => rounds
                    .OrderByDescending(r => r.CourseName)
                    .ToList(),

                _ => rounds
                    .OrderByDescending(r => r.DatePlayed)
                    .ToList()
            };

        // Keep the desktop and mobile Picker values
        // synchronized.
        if (DesktopRoundSortPicker.SelectedIndex
            != picker.SelectedIndex)
        {
            DesktopRoundSortPicker.SelectedIndex =
                picker.SelectedIndex;
        }

        if (MobileRoundSortPicker.SelectedIndex
            != picker.SelectedIndex)
        {
            MobileRoundSortPicker.SelectedIndex =
                picker.SelectedIndex;
        }

        DesktopRoundsList.ItemsSource = sortedRounds;
        MobileRoundsList.ItemsSource = sortedRounds;
    }
}
