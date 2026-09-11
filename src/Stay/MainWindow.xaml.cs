using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Stay.Core;

namespace Stay;

public partial class MainWindow : Window
{
    private readonly Shell _shell;

    public MainWindow(Engine engine)
    {
        _shell = new Shell(engine);
        InitializeComponent();
        DataContext = _shell;
        Loaded += (_, _) => { Theme.DecorateTitleBar(this); ShowPane(0); };
    }

    private void PaneChanged(object sender, SelectionChangedEventArgs e) => ShowPane(Panes.SelectedIndex);

    /// <summary>For the screenshot renderer, which walks every pane without anybody clicking.</summary>
    public void ShowPaneForShot(int which) { Panes.SelectedIndex = which; ShowPane(which); }

    private void ShowPane(int which)
    {
        if (StandPane is null) return;   // during InitializeComponent
        var panes = new[] { StandPane, PatchedPane, ElevenPane, ShutPane, SpeedPane, JunkPane, ReceiptPane };
        for (int i = 0; i < panes.Length; i++)
            panes[i].Visibility = i == which ? Visibility.Visible : Visibility.Collapsed;

        // The bar that changes things belongs only to the panes that change things.
        ActionBar.Visibility = which is 3 or 4 or 5 ? Visibility.Visible : Visibility.Collapsed;
        ApplyButton.Content = which switch { 4 => "Do the ticked ones", 5 => "Turn off the ticked ones", _ => "Shut the ticked ones" };
        Scroller.ScrollToTop();
        UpdateBar();
    }

    /// <summary>The plan the bar acts on depends on which pane is in front; there is one bar and three lists.</summary>
    private Plan Planned() => Panes.SelectedIndex switch
    {
        4 => _shell.PlanSpeed(),
        5 => _shell.PlanJunk(),
        _ => _shell.PlanSelected(),
    };

    private void UpdateBar()
    {
        var plan = Planned();
        int apps = Panes.SelectedIndex == 5 ? _shell.AppsToRemove().Count : 0;
        ApplyButton.IsEnabled = !plan.IsEmpty || apps > 0;

        if (plan.IsEmpty && apps == 0) { BarText.Text = "Nothing ticked that is not already done."; return; }

        var said = $"{plan.Guards.Count} ticked, {plan.Registry.Count} value{(plan.Registry.Count == 1 ? "" : "s")} to change.";
        if (apps > 0) said += $" {apps} app{(apps == 1 ? "" : "s")} to remove, which cannot be undone by a receipt.";
        if (plan.NeedsRestart) said += " Some of it takes effect after a restart.";
        BarText.Text = said;
    }

    /// <summary>Putting back what Windows undid, through the same path as any other change.</summary>
    private void FixDrift(object sender, RoutedEventArgs e)
    {
        var plan = _shell.PlanDrift();
        if (plan.IsEmpty) { Say("Nothing ticked."); return; }

        var receipt = _shell.Apply(plan, false);
        _shell.Rescan();
        Say($"Turned {receipt.Changed} of them off again. Receipt {receipt.Id}.");
    }

    private void OpenUpdate(object sender, RoutedEventArgs e) => Shell.Open("ms-settings:windowsupdate");

    /// <summary>Windows' own Disk Cleanup, because deleting files is the one thing a receipt cannot undo.</summary>
    private void OpenCleanup(object sender, RoutedEventArgs e) => Shell.Open("cleanmgr.exe");

    private void ShowChanges(object sender, RoutedEventArgs e)
    {
        var plan = Planned();
        if (plan.IsEmpty) { Say("Nothing ticked that is not already done."); return; }
        var lines = plan.Registry.Select(c => $"{c.Path}\n    {c.Before}  ->  {c.After}");
        Say(string.Join("\n", lines), "Every value that would change");
    }

    private void ApplyClicked(object sender, RoutedEventArgs e)
    {
        var plan = Planned();
        var removing = Panes.SelectedIndex == 5 ? _shell.AppsToRemove() : Array.Empty<AppState>();
        if (plan.IsEmpty && removing.Count == 0) return;

        var question = $"Change {plan.Registry.Count} value(s)?\n\n"
            + "A receipt is written first, holding what every value was before, so all of it can be put back.";
        if (removing.Count > 0)
            question = $"Change {plan.Registry.Count} value(s) and remove {removing.Count} app(s)?\n\n"
                + "The values go on a receipt and can all be put back. The apps cannot: removing one is the only "
                + "thing this app does that a receipt will not undo. The receipt keeps a Store link for each so "
                + "you can install it again.\n\n"
                + string.Join("\n", removing.Select(a => "  " + a.Title));

        var asked = MessageBox.Show(question, "Stay for Windows 10", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (asked != MessageBoxResult.OK) return;

        bool point = WantRestorePoint.IsChecked == true;
        bool made = false;
        if (point)
        {
            Cursor = System.Windows.Input.Cursors.Wait;
            var (ok, reason) = RestorePoint.Create();
            Cursor = null;
            made = ok;
            if (!ok) Say($"No restore point: {reason}\n\nThe receipt still records every change, so this is still undoable.");
        }

        Cursor = removing.Count > 0 ? System.Windows.Input.Cursors.Wait : Cursor;
        var receipt = _shell.Apply(plan, removing, made);
        Cursor = null;
        _shell.Rescan();
        ShowPane(Panes.SelectedIndex);

        var said = $"Changed {receipt.Changed} value(s).";
        if (receipt.Failed > 0)
            said += $"\n\n{receipt.Failed} failed. Those need an administrator — close the app and open it again with "
                  + "\"Run as administrator\".";
        if (plan.NeedsRestart) said += "\n\nSome of it takes effect after a restart.";
        if (plan.Guards.Any(g => g.NeedsSignOut)) said += "\n\nSome of it takes effect after you sign out and back in.";
        if (receipt.NeedsStore) said += "\n\nThe apps that were removed are listed on the receipt with a Store link each.";
        said += $"\n\nReceipt {receipt.Id}. It is under Receipts, with a button that puts it all back.";
        Say(said);
    }

    private void UndoClicked(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ReceiptRow row) return;

        var asked = MessageBox.Show(
            $"Put back every value this receipt changed?\n\n{row.Summary}, made {row.When}.",
            "Stay for Windows 10", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (asked != MessageBoxResult.OK) return;

        var result = _shell.Undo(row.Receipt);
        _shell.Rescan();
        ShowPane(Panes.SelectedIndex);
        Say(result.Errors.Count == 0
            ? $"Put back {result.Restored} value(s)."
            : $"Put back {result.Restored} value(s). {result.Errors.Count} could not be:\n\n"
              + string.Join("\n", result.Errors));
    }

    private static void Say(string what, string title = "Stay for Windows 10")
        => MessageBox.Show(what, title, MessageBoxButton.OK, MessageBoxImage.Information);
}

/// <summary>Turns a state word into the right pair of colours, so one template serves every kind of row.</summary>
public sealed class StateBrush : IValueConverter
{
    public string Mode { get; set; } = "Bg";

    public object Convert(object value, Type target, object parameter, CultureInfo culture)
    {
        var key = (value as string ?? "Loud") + Mode;
        return Application.Current.Resources[key] as Brush ?? Brushes.Transparent;
    }

    public object ConvertBack(object value, Type target, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Hides a row whose text is empty, so an absent remedy leaves no gap.</summary>
public sealed class NotEmptyToVisibility : IValueConverter
{
    public object Convert(object value, Type target, object parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type target, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Shows something only when a flag is false: the "nothing here yet" line under an empty list.</summary>
public sealed class FalseToVisibility : IValueConverter
{
    public object Convert(object value, Type target, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type target, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
