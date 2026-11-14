
namespace GolfTracker.Pages;

public partial class NewGame : ContentPage
{
    // Responsive layout tracking
    private bool? _isMobileLayout;

    // Number of holes selected on the form
    private int selectedHoles = 18;

    // Information to send to Scorecard after par setup
    private ShellNavigationQueryParameters? pendingRoundParameters;

    // One par value per hole
    // Zero means no par has been entered
    private int[] holePars = Array.Empty<int>();

    // Current array position (0 = Hole 1)
    private int currentParHole = 0;

    // Prevents TextChanged from firing logic
    // while restoring previously entered values
    private bool updatingParDisplay = false;

    public NewGame()
    {
        InitializeComponent();
    }

    // --------------------------------------------------
    // RESPONSIVE LAYOUT
    // --------------------------------------------------

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        if (Width <= 0)
            return;

        bool isMobile = Width < 800;

        if (_isMobileLayout == isMobile)
            return;

        _isMobileLayout = isMobile;

        if (isMobile)
        {
            // One column, two rows
            NewGameGrid.ColumnDefinitions =
                new ColumnDefinitionCollection
                {
                    new ColumnDefinition
                    {
                        Width = GridLength.Star
                    }
                };

            NewGameGrid.RowDefinitions =
                new RowDefinitionCollection
                {
                    new RowDefinition
                    {
                        Height = GridLength.Auto
                    },
                    new RowDefinition
                    {
                        Height = GridLength.Auto
                    }
                };

            NewGameGrid.MinimumHeightRequest = 0;

            // Form on top
            Grid.SetRow(NewGameForm, 0);
            Grid.SetColumn(NewGameForm, 0);

            // Image underneath
            Grid.SetRow(NewGameImage, 1);
            Grid.SetColumn(NewGameImage, 0);

            NewGameImage.HeightRequest = 300;
        }
        else
        {
            // Two equal columns
            NewGameGrid.ColumnDefinitions =
                new ColumnDefinitionCollection
                {
                    new ColumnDefinition
                    {
                        Width = GridLength.Star
                    },
                    new ColumnDefinition
                    {
                        Width = GridLength.Star
                    }
                };

            NewGameGrid.RowDefinitions =
                new RowDefinitionCollection
                {
                    new RowDefinition
                    {
                        Height = GridLength.Auto
                    }
                };

            NewGameGrid.MinimumHeightRequest = 450;

            // Form on the left
            Grid.SetRow(NewGameForm, 0);
            Grid.SetColumn(NewGameForm, 0);

            // Image on the right
            Grid.SetRow(NewGameImage, 0);
            Grid.SetColumn(NewGameImage, 1);

            NewGameImage.HeightRequest = -1;
        }
    }

    // --------------------------------------------------
    // NUMBER OF HOLES SELECTION
    // --------------------------------------------------

    private void OnNineHolesClicked(object? sender, EventArgs e)
    {
        SelectHoles(9);
    }

    private void OnEighteenHolesClicked(object? sender, EventArgs e)
    {
        SelectHoles(18);
    }

    private void OnCustomClicked(object? sender, EventArgs e)
    {
        SelectHoles(0);
    }

    private void SelectHoles(int holes)
    {
        selectedHoles = holes;

        // Show custom entry only when Custom is selected
        CustomHolesBorder.IsVisible = holes == 0;

        // Reset button styles
        NineHolesButton.BackgroundColor = Colors.Transparent;
        EighteenHolesButton.BackgroundColor = Colors.Transparent;
        CustomButton.BackgroundColor = Colors.Transparent;

        NineHolesButton.TextColor = Colors.Black;
        EighteenHolesButton.TextColor = Colors.Black;
        CustomButton.TextColor = Colors.Black;

        // Determine selected button
        Button selectedButton = holes switch
        {
            9 => NineHolesButton,
            18 => EighteenHolesButton,
            _ => CustomButton
        };

        // Apply selected styling
        selectedButton.BackgroundColor =
            (Color)Application.Current!.Resources["PrimaryGreen"];

        selectedButton.TextColor = Colors.White;
    }

    // --------------------------------------------------
    // COURSE NAME VALIDATION
    // --------------------------------------------------

    private void OnCourseNameChanged(
        object sender,
        TextChangedEventArgs e)
    {
        StartRoundButton.IsEnabled =
            !string.IsNullOrWhiteSpace(e.NewTextValue);
    }

    // --------------------------------------------------
    // START ROUND
    // --------------------------------------------------

    private async void StartRoundClicked(object? sender, EventArgs e)
    {
        // Get the number of holes
        int numberOfHoles = selectedHoles;

        // Handle custom number of holes
        if (selectedHoles == 0)
        {
            if (!int.TryParse(
                    CustomHolesEntry.Text,
                    out numberOfHoles)
                || numberOfHoles <= 0)
            {
                await DisplayAlertAsync(
                    "Invalid Number",
                    "Please enter a valid number of holes.",
                    "OK");

                return;
            }
        }

        // Determine selected course type
        string courseType;

        if (TraditionalRadio.IsChecked)
        {
            courseType = "Traditional";
        }
        else if (PutterRadio.IsChecked)
        {
            courseType = "Putter";
        }
        else
        {
            courseType = "Executive";
        }

        // Create the normal round parameters
        var parameters = new ShellNavigationQueryParameters
        {
            { "CourseName", CourseNameEntry.Text },
            { "Date", RoundDatePicker.Date },
            { "CourseType", courseType },
            { "NumberOfHoles", numberOfHoles }
        };

        // Ask whether the player wants to enter par
        bool wantsPar = await DisplayAlertAsync(
            "Course Par",
            "Would you like to enter par for each hole?",
            "Yes",
            "No");

        if (!wantsPar)
        {
            // No par setup needed
            await Shell.Current.GoToAsync(
                "//Scorecard",
                parameters);

            return;
        }

        // Save the parameters until par setup is complete
        pendingRoundParameters = parameters;

        // Create one par value for each hole
        holePars = new int[numberOfHoles];

        // Start at Hole 1
        currentParHole = 0;

        // Switch from the form to par setup
        NewGameFormHeader.IsVisible = false;
        NewGameFormCard.IsVisible = false;
        ParSetupSection.IsVisible = true;

        UpdateParSetupDisplay();
    }

    // --------------------------------------------------
    // PAR VALUE SELECTION
    // --------------------------------------------------

    private void OnParThreeClicked(object? sender, EventArgs e)
    {
        SetCurrentPar(3);
    }

    private void OnParFourClicked(object? sender, EventArgs e)
    {
        SetCurrentPar(4);
    }

    private void OnParFiveClicked(object? sender, EventArgs e)
    {
        SetCurrentPar(5);
    }

    private void SetCurrentPar(int par)
    {
        if (holePars.Length == 0)
            return;

        // Save the par for the current hole
        holePars[currentParHole] = par;

        // Update the entry without triggering additional logic
        updatingParDisplay = true;
        ParValueEntry.Text = par.ToString();
        updatingParDisplay = false;

        UpdateParProgress();
        UpdateParButtonStyles();
    }

    // --------------------------------------------------
    // CUSTOM PAR ENTRY
    // --------------------------------------------------

    private void OnParValueChanged(
        object? sender,
        TextChangedEventArgs e)
    {
        if (updatingParDisplay || holePars.Length == 0)
            return;

        // Only accept positive whole numbers
        if (int.TryParse(e.NewTextValue, out int par)
            && par > 0)
        {
            holePars[currentParHole] = par;
        }
        else
        {
            // Zero represents an unanswered hole
            holePars[currentParHole] = 0;
        }

        UpdateParProgress();
        UpdateParButtonStyles();
    }

    // --------------------------------------------------
    // UPDATE PAR SETUP DISPLAY
    // --------------------------------------------------

    private void UpdateParSetupDisplay()
    {
        if (holePars.Length == 0)
            return;

        int totalHoles = holePars.Length;

        // Display the current hole number
        ParHoleLabel.Text =
            $"Hole {currentParHole + 1} of {totalHoles}";

        // Restore an existing par when revisiting a hole
        updatingParDisplay = true;

        int savedPar = holePars[currentParHole];

        ParValueEntry.Text =
            savedPar > 0 ? savedPar.ToString() : "";

        updatingParDisplay = false;

        // Previous is unavailable on Hole 1
        PreviousParButton.IsEnabled = currentParHole > 0;

        // Show Finish only on the last hole
        bool isLastHole = currentParHole == totalHoles - 1;

        NextParButton.IsVisible = !isLastHole;
        FinishParButton.IsVisible = isLastHole;

        UpdateParProgress();
        UpdateParButtonStyles();
    }

    // --------------------------------------------------
    // UPDATE PROGRESS AND BUTTON STATES
    // --------------------------------------------------

    private void UpdateParProgress()
    {
        if (holePars.Length == 0)
            return;

        // Count how many holes have valid par values
        int completedHoles = holePars.Count(par => par > 0);

        ParProgressLabel.Text =
            $"{completedHoles} of {holePars.Length} holes completed";

        // A valid par is required before moving forward
        bool currentHoleValid = holePars[currentParHole] > 0;

        NextParButton.IsEnabled = currentHoleValid;

        // All holes must have par before finishing
        FinishParButton.IsEnabled =
            holePars.All(par => par > 0);
    }

    // --------------------------------------------------
    // SELECTED PAR BUTTON STYLING
    // --------------------------------------------------

    private void UpdateParButtonStyles()
    {
        Color primaryGreen =
            (Color)Application.Current!.Resources["PrimaryGreen"];

        // Reset all preset buttons
        ParThreeButton.BackgroundColor = Colors.Transparent;
        ParFourButton.BackgroundColor = Colors.Transparent;
        ParFiveButton.BackgroundColor = Colors.Transparent;

        ParThreeButton.TextColor = primaryGreen;
        ParFourButton.TextColor = primaryGreen;
        ParFiveButton.TextColor = primaryGreen;

        if (holePars.Length == 0)
            return;

        int selectedPar = holePars[currentParHole];

        Button? selectedButton = selectedPar switch
        {
            3 => ParThreeButton,
            4 => ParFourButton,
            5 => ParFiveButton,
            _ => null
        };

        if (selectedButton != null)
        {
            selectedButton.BackgroundColor = primaryGreen;
            selectedButton.TextColor = Colors.White;
        }
    }

    // --------------------------------------------------
    // PREVIOUS HOLE
    // --------------------------------------------------

    private void OnPreviousParClicked(object? sender, EventArgs e)
    {
        if (currentParHole <= 0)
            return;

        currentParHole--;

        UpdateParSetupDisplay();
    }

    // --------------------------------------------------
    // NEXT HOLE
    // --------------------------------------------------

    private void OnNextParClicked(object? sender, EventArgs e)
    {
        if (holePars.Length == 0)
            return;

        // Do not continue without a valid par
        if (holePars[currentParHole] <= 0)
            return;

        if (currentParHole < holePars.Length - 1)
        {
            currentParHole++;
            UpdateParSetupDisplay();
        }
    }

    // --------------------------------------------------
    // FINISH PAR SETUP
    // --------------------------------------------------

    private async void OnFinishParSetupClicked(
        object? sender,
        EventArgs e)
    {
        if (pendingRoundParameters == null)
            return;

        // Make sure every hole has a valid par
        if (holePars.Any(par => par <= 0))
        {
            await DisplayAlertAsync(
                "Incomplete Par Setup",
                "Please enter par for every hole.",
                "OK");

            return;
        }

        // Add the par values to the navigation parameters
        pendingRoundParameters.Add(
            "HolePars",
            holePars.ToArray());

        // Navigate to Scorecard
        await Shell.Current.GoToAsync(
            "//Scorecard",
            pendingRoundParameters);

        // Reset the form for the next round
        ResetParSetup();
    }

    // --------------------------------------------------
    // CANCEL PAR SETUP
    // --------------------------------------------------

    private void OnCancelParSetupClicked(
        object? sender,
        EventArgs e)
    {
        ResetParSetup();
    }

    // --------------------------------------------------
    // RESET PAR SETUP
    // --------------------------------------------------

    private void ResetParSetup()
    {
        pendingRoundParameters = null;
        holePars = Array.Empty<int>();
        currentParHole = 0;

        updatingParDisplay = true;
        ParValueEntry.Text = "";
        updatingParDisplay = false;

        // Restore original form
        NewGameFormHeader.IsVisible = true;
        NewGameFormCard.IsVisible = true;
        ParSetupSection.IsVisible = false;
    }
}
