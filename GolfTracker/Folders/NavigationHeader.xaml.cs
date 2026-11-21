
namespace GolfTracker.Controls;

public partial class NavigationHeader : ContentView
{
    private const double DesktopBreakpoint = 800;

    private static readonly Color HoverColor =
        Color.FromArgb("#86EFAC");

    public NavigationHeader()
    {
        InitializeComponent();

        Loaded += OnHeaderLoaded;
        Unloaded += OnHeaderUnloaded;
    }

    // Set up active navigation when the header appears
    private void OnHeaderLoaded(object? sender, EventArgs e)
    {
        if (Shell.Current is not null)
        {
            Shell.Current.Navigated -= OnShellNavigated;
            Shell.Current.Navigated += OnShellNavigated;
        }

        UpdateActiveNavigation();
    }

    // Clean up when the header is removed
    private void OnHeaderUnloaded(object? sender, EventArgs e)
    {
        if (Shell.Current is not null)
        {
            Shell.Current.Navigated -= OnShellNavigated;
        }
    }

    // Update whenever Shell finishes navigating
    private void OnShellNavigated(
        object? sender,
        ShellNavigatedEventArgs e)
    {
        UpdateActiveNavigation();
    }

    // Highlight the current page
    
private void UpdateActiveNavigation()
{
    if (Shell.Current?.CurrentState?.Location is null)
        return;

    string currentRoute = Shell.Current.CurrentState.Location
        .ToString()
        .Split('?', '#')[0]
        .TrimEnd('/')
        .Split('/')
        .LastOrDefault() ?? "";

    // Desktop: green underline on the active page
    WelcomeUnderline.IsVisible =
        currentRoute == "WelcomePage";

    SignInUnderline.IsVisible =
        currentRoute == "SignIn";

    NewGameUnderline.IsVisible =
        currentRoute == "NewGame";

    HistoryUnderline.IsVisible =
        currentRoute == "History";

    // Mobile: green text on the active page
    WelcomeMobile.TextColor =
        currentRoute == "WelcomePage"
            ? HoverColor : Colors.White;

    SignInMobile.TextColor =
        currentRoute == "SignIn"
            ? HoverColor : Colors.White;

    NewGameMobile.TextColor =
        currentRoute == "NewGame"
            ? HoverColor : Colors.White;

    HistoryMobile.TextColor =
        currentRoute == "History"
            ? HoverColor : Colors.White;
}


    // Responsive desktop/mobile navigation
    private void Header_SizeChanged(object? sender, EventArgs e)
    {
        bool isDesktop = Width >= DesktopBreakpoint;

        DesktopNavigation.IsVisible = isDesktop;
        MobileMenuButton.IsVisible = !isDesktop;

        if (isDesktop)
        {
            MobileMenu.IsVisible = false;
        }
    }

    // Toggle mobile menu
    private void OnMenuClicked(object? sender, EventArgs e)
    {
        MobileMenu.IsVisible = !MobileMenu.IsVisible;
    }

    // Navigate when a link is tapped
    private async void OnNavigateTapped(
        object? sender,
        TappedEventArgs e)
    {
        if (e.Parameter is not string route)
            return;

        MobileMenu.IsVisible = false;

        await Shell.Current.GoToAsync($"//{route}");
    }

    // Welcome hover
    private void OnWelcomeHover(object? sender, PointerEventArgs e)
    {
        WelcomeDesktop.TextColor = HoverColor;
    }

    private void OnWelcomeExit(object? sender, PointerEventArgs e)
    {
        WelcomeDesktop.TextColor = Colors.White;
    }

    // Sign In hover
    private void OnSignInHover(object? sender, PointerEventArgs e)
    {
        SignInDesktop.TextColor = HoverColor;
    }

    private void OnSignInExit(object? sender, PointerEventArgs e)
    {
        SignInDesktop.TextColor = Colors.White;
    }

    // New Game hover
    private void OnNewGameHover(object? sender, PointerEventArgs e)
    {
        NewGameDesktop.TextColor = HoverColor;
    }

    private void OnNewGameExit(object? sender, PointerEventArgs e)
    {
        NewGameDesktop.TextColor = Colors.White;
    }

    // History hover
    private void OnHistoryHover(object? sender, PointerEventArgs e)
    {
        HistoryDesktop.TextColor = HoverColor;
    }

    private void OnHistoryExit(object? sender, PointerEventArgs e)
    {
        HistoryDesktop.TextColor = Colors.White;
    }
}
