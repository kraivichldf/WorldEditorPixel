using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Kingdom.World.Core.Campaign.Seasons;

namespace Kingdom.World.Editor.Dialogs;

public sealed partial class SeasonLockResolutionDialog : Window
{
    private readonly List<ConflictEditor> _conflictEditors = [];
    private readonly StackPanel _conflictRows;
    private readonly TextBlock _conflictHelp;
    private readonly Border _dropPanel;
    private readonly TextBlock _dropSummary;
    private readonly CheckBox _permitDrops;
    private readonly Border _validationPanel;
    private readonly TextBlock _validationText;

    public SeasonLockResolutionDialog()
    {
        AvaloniaXamlLoader.Load(this);
        _conflictRows = FindRequired<StackPanel>("ConflictRows");
        _conflictHelp = FindRequired<TextBlock>("ConflictHelpText");
        _dropPanel = FindRequired<Border>("DropPanel");
        _dropSummary = FindRequired<TextBlock>("DropSummaryText");
        _permitDrops = FindRequired<CheckBox>("PermitDropsInput");
        _validationPanel = FindRequired<Border>("ValidationPanel");
        _validationText = FindRequired<TextBlock>("ValidationText");
    }

    public SeasonLockResolutionDialog(
        CampaignSeasonWorldRegenerationReport report,
        CampaignSeasonCatalog catalog,
        IEnumerable<CampaignSeasonLockResolution> currentResolutions,
        bool permitLockedDrops)
        : this()
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(currentResolutions);
        var resolutions = currentResolutions.ToDictionary(
            static value => (value.TargetX, value.TargetY),
            static value => value.SeasonId);
        _conflictHelp.Text = report.Conflicts.Count == 0
            ? "This candidate has no equal-overlap conflict."
            : "Two or more different locked Season IDs claim the same target with equal greatest overlap. " +
              "Choose the authoritative winner for every target.";
        _conflictRows.IsVisible = report.Conflicts.Count > 0;
        foreach (var conflict in report.Conflicts)
        {
            var options = conflict.Claims
                .Select(static value => value.SeasonId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => catalog.GetIndex(value))
                .Select(value => new SeasonDecisionOption(
                    value,
                    $"{catalog.Get(value).Name} ({value})"))
                .ToArray();
            var selectedId = resolutions.GetValueOrDefault((conflict.TargetX, conflict.TargetY)) ??
                conflict.ResolvedSeasonId;
            var comboBox = new ComboBox
            {
                ItemsSource = options,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                SelectedIndex = Array.FindIndex(
                    options,
                    option => string.Equals(option.Id, selectedId, StringComparison.Ordinal)),
            };
            comboBox.SetValue(
                Avalonia.Automation.AutomationProperties.NameProperty,
                $"Winner for target tile {conflict.TargetX}, {conflict.TargetY}");
            var claims = string.Join(
                " · ",
                conflict.Claims.Select(claim =>
                    $"{catalog.Get(claim.SeasonId).Name} from ({claim.SourceX}, {claim.SourceY}) " +
                    $"{claim.OverlapPercent:0.#}% overlap"));
            var content = new StackPanel { Spacing = 5 };
            content.Children.Add(new TextBlock
            {
                Text = $"Target tile ({conflict.TargetX}, {conflict.TargetY})",
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
            });
            content.Children.Add(new TextBlock
            {
                Text = claims,
                FontSize = 12,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            });
            content.Children.Add(comboBox);
            _conflictRows.Children.Add(new Border
            {
                Classes = { "win98-sunken" },
                Padding = new Avalonia.Thickness(9),
                Child = content,
            });
            _conflictEditors.Add(new ConflictEditor(conflict.TargetX, conflict.TargetY, comboBox));
        }

        _dropPanel.IsVisible = report.LockedDrops.Count > 0;
        _permitDrops.IsChecked = permitLockedDrops;
        if (report.LockedDrops.Count > 0)
        {
            var named = string.Join(
                ", ",
                report.LockedDrops.Take(10).Select(drop =>
                    $"{catalog.Get(drop.SeasonId).Name} ({drop.SourceX}, {drop.SourceY})"));
            var remaining = report.LockedDrops.Count - Math.Min(10, report.LockedDrops.Count);
            _dropSummary.Text =
                $"{report.LockedDrops.Count:N0} locked source tile(s) have no physical overlap with the replacement world: " +
                named + (remaining > 0 ? $", plus {remaining:N0} more" : string.Empty) +
                ". Acceptance remains blocked unless you explicitly permit these drops.";
        }
    }

    private void UseDecisions_OnClick(object? sender, RoutedEventArgs e)
    {
        var resolutions = new List<CampaignSeasonLockResolution>(_conflictEditors.Count);
        foreach (var editor in _conflictEditors)
        {
            if (editor.Input.SelectedItem is not SeasonDecisionOption option)
            {
                ShowValidation(
                    $"Choose a winning Season for target tile ({editor.TargetX}, {editor.TargetY}).");
                return;
            }

            resolutions.Add(new CampaignSeasonLockResolution(
                editor.TargetX,
                editor.TargetY,
                option.Id));
        }

        Close(new SeasonLockResolutionDialogResult(
            Array.AsReadOnly(resolutions.ToArray()),
            _permitDrops.IsChecked == true));
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(null);

    private void ShowValidation(string message)
    {
        _validationText.Text = message;
        _validationPanel.IsVisible = true;
    }

    private T FindRequired<T>(string name) where T : Control =>
        this.FindControl<T>(name) ??
        throw new InvalidOperationException($"Required control '{name}' was not found.");

    private sealed record ConflictEditor(int TargetX, int TargetY, ComboBox Input);

    private sealed record SeasonDecisionOption(string Id, string Label)
    {
        public override string ToString() => Label;
    }
}

public sealed record SeasonLockResolutionDialogResult(
    IReadOnlyList<CampaignSeasonLockResolution> Resolutions,
    bool PermitLockedDrops);
