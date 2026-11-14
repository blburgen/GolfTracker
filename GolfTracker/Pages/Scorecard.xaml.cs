
namespace GolfTracker.Pages;

public partial class Scorecard : ContentPage, IQueryAttributable
{
    private int numberOfHoles;
    private int currentHole = 1;
    private int?[] holeStrokes = Array.Empty<int?>();
    private int[]? holePars;

    // Keep round details for the completion overlay.
    private string roundCourseName = "";
    private string roundCourseType = "";
    private DateTime roundDate;

    public Scorecard()
    {
        InitializeComponent();
    }

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        if (Width <= 0)
            return;

        bool isMobile = Width < 800;
        MobileScorecard.IsVisible = isMobile;
        DesktopScorecard.IsVisible = !isMobile;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        roundCourseName = query["CourseName"]?.ToString() ?? "";
        roundCourseType = query["CourseType"]?.ToString() ?? "";
        roundDate = (DateTime)query["Date"];
        numberOfHoles = (int)query["NumberOfHoles"];

        currentHole = 1;
        holeStrokes = new int?[numberOfHoles];
        holePars = null;
        RoundCompleteOverlay.IsVisible = false;

        if (query.TryGetValue("HolePars", out object? parValue)
            && parValue is int[] receivedPars
            && receivedPars.Length == numberOfHoles)
        {
            holePars = receivedPars;
        }

        string details = $"{roundDate:MMMM d, yyyy} · {roundCourseType}";
        string holeCount = $"{numberOfHoles}-hole round";

        CourseName.Text = roundCourseName;
        RoundDateAndType.Text = details;
        RoundHoleCount.Text = holeCount;

        DesktopCourseName.Text = roundCourseName;
        DesktopRoundDateAndType.Text = details;
        DesktopRoundHoleCount.Text = holeCount;

        UpdateScorecard();
    }

    private void OnIncreaseStrokesClicked(object? sender, EventArgs e)
    {
        if (numberOfHoles <= 0)
            return;

        int index = currentHole - 1;
        holeStrokes[index] = (holeStrokes[index] ?? 0) + 1;
        UpdateScorecard();
    }

    private void OnDecreaseStrokesClicked(object? sender, EventArgs e)
    {
        if (numberOfHoles <= 0)
            return;

        int index = currentHole - 1;
        if (holeStrokes[index] is int score && score > 1)
            holeStrokes[index] = score - 1;

        UpdateScorecard();
    }

    private void OnPreviousHoleClicked(object? sender, EventArgs e)
    {
        if (currentHole <= 1)
            return;

        currentHole--;
        UpdateScorecard();
    }

    private void OnNextHoleClicked(object? sender, EventArgs e)
    {
        if (currentHole >= numberOfHoles)
            return;

        currentHole++;
        UpdateScorecard();
    }

    private void UpdateScorecard()
    {
        if (numberOfHoles <= 0)
            return;

        int index = currentHole - 1;
        int? strokes = holeStrokes[index];
        string holeText = $"Hole {currentHole} of {numberOfHoles}";
        string strokesText = strokes?.ToString() ?? "—";

        CurrentHoleLabel.Text = holeText;
        DesktopCurrentHoleLabel.Text = holeText;
        CurrentStrokesLabel.Text = strokesText;
        DesktopCurrentStrokesLabel.Text = strokesText;

        bool canDecrease = strokes.HasValue && strokes.Value > 1;
        DecreaseStrokesButton.IsEnabled = canDecrease;
        DesktopDecreaseStrokesButton.IsEnabled = canDecrease;

        bool canGoBack = currentHole > 1;
        PreviousHoleButton.IsEnabled = canGoBack;
        DesktopPreviousHoleButton.IsEnabled = canGoBack;

        bool isLastHole = currentHole == numberOfHoles;
        NextHoleButton.IsEnabled = !isLastHole;
        DesktopNextHoleButton.IsEnabled = !isLastHole;
        FinishRoundButton.IsVisible = isLastHole;

        bool hasPar = holePars != null;
        CurrentParBorder.IsVisible = hasPar;
        DesktopCurrentParBorder.IsVisible = hasPar;
        HoleResultLabel.IsVisible = hasPar && strokes.HasValue;
        DesktopHoleResultLabel.IsVisible = hasPar && strokes.HasValue;

        if (holePars != null)
        {
            int par = holePars[index];
            CurrentParLabel.Text = $"Par {par}";
            DesktopCurrentParLabel.Text = $"Par {par}";

            string result = strokes.HasValue
                ? GetHoleResult(strokes.Value - par)
                : "";

            HoleResultLabel.Text = result;
            DesktopHoleResultLabel.Text = result;
        }
        else
        {
            HoleResultLabel.Text = "";
            DesktopHoleResultLabel.Text = "";
        }

        UpdateRoundSummary();
        UpdateDesktopHoleTable();
    }

    private void UpdateDesktopHoleTable()
    {
        DesktopHoleRows.Children.Clear();

        Color primaryGreen =
            (Color)Application.Current!.Resources["PrimaryGreen"];

        Color textDark =
            (Color)Application.Current.Resources["TextDark"];

        Color textMuted =
            (Color)Application.Current.Resources["TextMuted"];

        Color backgroundSoft =
            (Color)Application.Current.Resources["BackgroundSoft"];

        for (int i = 0; i < numberOfHoles; i++)
        {
            int holeNumber = i + 1;
            int? strokes = holeStrokes[i];

            string parText =
                holePars != null ? holePars[i].ToString() : "—";

            string strokesText =
                strokes?.ToString() ?? "—";

            string resultText = "—";

            if (strokes.HasValue && holePars != null)
            {
                int difference = strokes.Value - holePars[i];

                resultText = difference > 0
                    ? $"+{difference}"
                    : difference.ToString();
            }

            Grid row = new Grid
            {
                ColumnSpacing = 8,
                Padding = new Thickness(8),
                BackgroundColor = holeNumber == currentHole
                    ? backgroundSoft
                    : Colors.Transparent
            };

            row.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = new GridLength(90)
                });

            row.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = GridLength.Star
                });

            row.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = GridLength.Star
                });

            row.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = GridLength.Star
                });

            row.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = GridLength.Auto
                });

            Button holeButton = new Button
            {
                Text = holeNumber.ToString(),
                BackgroundColor = holeNumber == currentHole
                    ? primaryGreen
                    : backgroundSoft,
                TextColor = holeNumber == currentHole
                    ? Colors.White
                    : primaryGreen,
                CornerRadius = 8,
                Padding = new Thickness(8),
                HeightRequest = 40
            };

            int selectedHole = holeNumber;

            holeButton.Clicked += (sender, e) =>
            {
                currentHole = selectedHole;
                UpdateScorecard();
            };

            Label parLabel = new Label
            {
                Text = parText,
                TextColor = textDark,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            };

            Label strokesLabel = new Label
            {
                Text = strokesText,
                TextColor = primaryGreen,
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            };

            Label resultLabel = new Label
            {
                Text = resultText,
                TextColor = textMuted,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            };

            row.Add(holeButton, 0, 0);
            row.Add(parLabel, 1, 0);
            row.Add(strokesLabel, 2, 0);
            row.Add(resultLabel, 3, 0);

            DesktopHoleRows.Children.Add(row);
        }
    }

    private static string GetHoleResult(int difference)
    {
        return difference switch
        {
            -3 => "Albatross",
            -2 => "Eagle",
            -1 => "Birdie",
             0 => "Par",
             1 => "Bogey",
             2 => "Double Bogey",
             3 => "Triple Bogey",
             _ => difference > 0
                ? $"+{difference} Over Par"
                : $"{difference} Under Par"
        };
    }

    private void UpdateRoundSummary()
    {
        int totalStrokes =
            holeStrokes.Sum(strokes => strokes ?? 0);

        int completedHoles =
            holeStrokes.Count(strokes => strokes.HasValue);

        string completedText =
            $"{completedHoles} / {numberOfHoles}";

        TotalStrokesLabel.Text = totalStrokes.ToString();
        HolesCompletedLabel.Text = completedText;

        DesktopTotalStrokesLabel.Text = totalStrokes.ToString();
        DesktopHolesCompletedLabel.Text = completedText;

        bool hasPar = holePars != null;

        RelativeToParSection.IsVisible = hasPar;
        DesktopRelativeToParSection.IsVisible = hasPar;

        if (holePars != null)
        {
            int completedPar = 0;

            for (int i = 0; i < numberOfHoles; i++)
            {
                if (holeStrokes[i].HasValue)
                    completedPar += holePars[i];
            }

            string relativeText = "—";

            if (completedHoles > 0)
            {
                int difference = totalStrokes - completedPar;

                relativeText = difference > 0
                    ? $"+{difference}"
                    : difference.ToString();
            }

            RelativeToParLabel.Text = relativeText;
            DesktopRelativeToParLabel.Text = relativeText;
        }

        bool isComplete =
            numberOfHoles > 0 &&
            completedHoles == numberOfHoles;

        FinishRoundButton.IsEnabled = isComplete;
        DesktopFinishRoundButton.IsEnabled = isComplete;
    }

    // --------------------------------------------------
    // ROUND COMPLETE POPUP
    // --------------------------------------------------

    private async void OnFinishRoundClicked(object? sender, EventArgs e)
    {
        if (numberOfHoles <= 0 ||
            holeStrokes.Any(strokes => !strokes.HasValue))
        {
            await DisplayAlertAsync(
                "Incomplete Round",
                "Please enter a score for every hole before finishing.",
                "OK");

            return;
        }

        int totalStrokes =
            holeStrokes.Sum(strokes => strokes ?? 0);

        FinalCourseLabel.Text = roundCourseName;

        FinalDetailsLabel.Text =
            $"{roundDate:MMMM d, yyyy} · {roundCourseType}";

        FinalStrokesLabel.Text = totalStrokes.ToString();

        FinalHoleCountLabel.Text =
            $"{numberOfHoles} holes completed";

        FinalParCard.IsVisible = holePars != null;

        if (holePars != null)
        {
            int difference = totalStrokes - holePars.Sum();

            FinalParLabel.Text = difference > 0
                ? $"+{difference}"
                : difference.ToString();
        }

        RoundCompleteOverlay.IsVisible = true;
    }

    private void OnReviewScorecardClicked(object? sender, EventArgs e)
    {
        RoundCompleteOverlay.IsVisible = false;
    }

    private async void OnStartNewRoundClicked(object? sender, EventArgs e)
    {
        RoundCompleteOverlay.IsVisible = false;

        await Shell.Current.GoToAsync("//NewGame");
    }

    private async void OnSignInToSaveClicked(object? sender, EventArgs e)
    {
        // Sign-in saving has not been connected yet.
        // Keep the user on this page so their scores aren't lost.
        await DisplayAlertAsync(
            "Saving Not Connected Yet",
            "Your round is complete, but sign-in saving has not been connected yet. " +
            "You can review your scorecard or start a new round. " +
            "The round is not saved to History.",
            "OK");
    }
}
