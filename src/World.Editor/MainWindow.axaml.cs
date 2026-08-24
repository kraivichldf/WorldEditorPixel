using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Generation;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Serialization;
using Kingdom.World.Core.Validation;
using Kingdom.World.Editor.Controls;
using Kingdom.World.Editor.Dialogs;
using Kingdom.World.Editor.Services;
using Kingdom.World.Editor.ViewModels;

namespace Kingdom.World.Editor;

public sealed partial class MainWindow : Window
{
    private static readonly FilePickerFileType WorldManifestFileType = new("Kingdom world manifest")
    {
        Patterns = [CampaignWorldProjectSerializer.ManifestFileName, "*.json"],
        MimeTypes = ["application/json"],
    };

    private static readonly FilePickerFileType RuntimeWorldPackageFileType = new("Kingdom runtime world package")
    {
        Patterns = [$"*{CampaignWorldRuntimeExporter.PackageExtension}"],
        MimeTypes = ["application/vnd.kingdom.world+zip"],
    };

    private static readonly FilePickerFileType RuntimeWorldJsonFileType = new("World runtime JSON data")
    {
        Patterns = [$"*{CampaignWorldJsonExporter.SuggestedFileSuffix}", "*.json"],
        MimeTypes = ["application/json"],
    };

    private readonly EditorViewModel _viewModel = new();
    private readonly WorldCanvas _worldCanvas;
    private bool _closeApproved;
    private bool _closePromptActive;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        DataContext = _viewModel;
        _worldCanvas = this.FindControl<WorldCanvas>("WorldCanvas")
            ?? throw new InvalidOperationException("World canvas was not found.");
        _worldCanvas.TileHovered += (_, args) => _viewModel.UpdateHover(args.Info);
        _worldCanvas.TileSelected += (_, args) =>
        {
            if (args.Info is { } selection)
            {
                _viewModel.SelectCoordinate(selection.Coordinate);
            }
        };
        _worldCanvas.StrokeCompleted += (_, args) =>
            _viewModel.RecordTileStroke(args.Command, args.BlockedRiverTileCount);
        _worldCanvas.ResourceStrokeCompleted += (_, args) =>
            _viewModel.RecordResourceStroke(args.Command);
        _worldCanvas.SeasonStrokeCompleted += (_, args) =>
            _viewModel.RecordSeasonStroke(args.Command);
        _worldCanvas.ZoomChanged += (_, args) => _viewModel.SetZoom(args.PixelsPerTile);
        Deactivated += (_, _) => CancelActiveStroke("Editor focus changed; the active map stroke was cancelled.");
        Closing += MainWindow_OnClosing;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.F6 && e.KeyModifiers == KeyModifiers.None && _viewModel.HasWorld)
        {
            _worldCanvas.Focus();
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            switch (e.Key)
            {
                case Key.N:
                    _ = CreateNewWorldAsync();
                    e.Handled = true;
                    return;
                case Key.O:
                    _ = OpenWorldAsync();
                    e.Handled = true;
                    return;
                case Key.R when e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                    _ = RegenerateResourcesAsync();
                    e.Handled = true;
                    return;
                case Key.G when e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                    _ = GenerateSeasonsAsync();
                    e.Handled = true;
                    return;
                case Key.R:
                    _ = RegenerateWorldAsync();
                    e.Handled = true;
                    return;
                case Key.S when e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                    _ = SaveWorldAsync(forceChooseDirectory: true);
                    e.Handled = true;
                    return;
                case Key.S:
                    _ = SaveWorldAsync(forceChooseDirectory: false);
                    e.Handled = true;
                    return;
                case Key.E when e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                    _ = ExportJsonDataAsync();
                    e.Handled = true;
                    return;
                case Key.E:
                    _ = ExportRuntimeDataAsync();
                    e.Handled = true;
                    return;
                case Key.Z:
                    Undo();
                    e.Handled = true;
                    return;
                case Key.Y:
                    Redo();
                    e.Handled = true;
                    return;
            }
        }

        if (e.Key == Key.F && !e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _worldCanvas.ZoomToFit();
            e.Handled = true;
        }
    }

    private void NewWorld_OnClick(object? sender, RoutedEventArgs e) => _ = CreateNewWorldAsync();

    private void OpenWorld_OnClick(object? sender, RoutedEventArgs e) => _ = OpenWorldAsync();

    private void RegenerateWorld_OnClick(object? sender, RoutedEventArgs e) => _ = RegenerateWorldAsync();

    private void RegenerateResources_OnClick(object? sender, RoutedEventArgs e) => _ = RegenerateResourcesAsync();

    private void GenerateSeasons_OnClick(object? sender, RoutedEventArgs e) => _ = GenerateSeasonsAsync();

    private void SaveWorld_OnClick(object? sender, RoutedEventArgs e) => _ = SaveWorldAsync(forceChooseDirectory: false);

    private void SaveWorldAs_OnClick(object? sender, RoutedEventArgs e) => _ = SaveWorldAsync(forceChooseDirectory: true);

    private void ExportRuntimeData_OnClick(object? sender, RoutedEventArgs e) => _ = ExportRuntimeDataAsync();

    private void ExportJsonData_OnClick(object? sender, RoutedEventArgs e) => _ = ExportJsonDataAsync();

    private void Undo_OnClick(object? sender, RoutedEventArgs e) => Undo();

    private void Redo_OnClick(object? sender, RoutedEventArgs e) => Redo();

    private void ZoomToFit_OnClick(object? sender, RoutedEventArgs e) => _worldCanvas.ZoomToFit();

    private void FocusMap_OnClick(object? sender, RoutedEventArgs e) => _worldCanvas.Focus();

    private void TerrainWorkspace_OnClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.SwitchToTerrainWorkspace();
        _worldCanvas.NotifyWorldChanged();
    }

    private void ResourcesWorkspace_OnClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.SwitchToResourcesWorkspace();
        _worldCanvas.NotifyWorldChanged();
    }

    private void SeasonsWorkspace_OnClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.SwitchToSeasonsWorkspace();
        _worldCanvas.NotifyWorldChanged();
    }

    private void ResourceAddUpdateTool_OnClick(object? sender, RoutedEventArgs e) =>
        _viewModel.SelectResourceAddUpdateTool();

    private void ResourceEraseTool_OnClick(object? sender, RoutedEventArgs e) =>
        _viewModel.SelectResourceEraseTool();

    private void SeasonPaintTool_OnClick(object? sender, RoutedEventArgs e) =>
        _viewModel.SelectSeasonPaintTool();

    private void SeasonEraseTool_OnClick(object? sender, RoutedEventArgs e) =>
        _viewModel.SelectSeasonEraseTool();

    private void SeasonLockTool_OnClick(object? sender, RoutedEventArgs e) =>
        _viewModel.SelectSeasonLockTool();

    private void SeasonUnlockTool_OnClick(object? sender, RoutedEventArgs e) =>
        _viewModel.SelectSeasonUnlockTool();

    private void CustomTerrainTypes_OnClick(object? sender, RoutedEventArgs e) =>
        _ = ManageCustomTerrainTypesAsync();

    private void CustomResources_OnClick(object? sender, RoutedEventArgs e) =>
        _ = ManageCustomResourcesAsync();

    private void CustomSeasons_OnClick(object? sender, RoutedEventArgs e) =>
        _ = ManageCustomSeasonsAsync();

    private void CopyPinnedHeight_OnClick(object? sender, RoutedEventArgs e) =>
        _viewModel.UsePinnedCenterHeight();

    private void BlendPinnedHeight_OnClick(object? sender, RoutedEventArgs e) =>
        _viewModel.UsePinnedNearbyHeight();

    private void CreateRiverSplit_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.CreatePinnedRiverSplit())
        {
            _worldCanvas.NotifyWorldChanged();
        }
    }

    private void AdoptPinnedResource_OnClick(object? sender, RoutedEventArgs e) =>
        _viewModel.AdoptSelectedPinnedResource();

    private void LockPinnedResource_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.LockSelectedPinnedResource())
        {
            _worldCanvas.NotifyWorldChanged();
        }
    }

    private void UnlockPinnedResource_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.UnlockSelectedPinnedResource())
        {
            _worldCanvas.NotifyWorldChanged();
        }
    }

    private void ErasePinnedResource_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.EraseSelectedPinnedResource())
        {
            _worldCanvas.NotifyWorldChanged();
        }
    }

    private void LockPinnedSeason_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.LockPinnedSeason())
        {
            _worldCanvas.NotifyWorldChanged();
        }
    }

    private void UnlockPinnedSeason_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.UnlockPinnedSeason())
        {
            _worldCanvas.NotifyWorldChanged();
        }
    }

    private void Exit_OnClick(object? sender, RoutedEventArgs e) => Close();

    private async Task CreateNewWorldAsync()
    {
        if (_viewModel.IsBusy ||
            CancelActiveStroke("Active map stroke cancelled. Choose New again when ready.") ||
            !await ConfirmReplacementAsync())
        {
            return;
        }

        var result = await new NewWorldDialog().ShowDialog<NewWorldDialogResult?>(this);
        if (result is null)
        {
            return;
        }

        _viewModel.CreateWorld(
            result.World,
            result.GenerationResult,
            result.SeasonMap,
            result.SeasonEnabledIds,
            result.SeasonSavedGeneration,
            result.SeasonSupportFields);
        _worldCanvas.ZoomToFit();
    }

    private async Task RegenerateWorldAsync()
    {
        if (_viewModel.IsBusy ||
            CancelActiveStroke("Active map stroke cancelled. Choose Regenerate again when ready.") ||
            _viewModel.World is not { } currentWorld ||
            _viewModel.ResourceMap is not { } currentResources ||
            _viewModel.SeasonMap is not { } currentSeasons)
        {
            return;
        }

        var result = await new NewWorldDialog(
                currentWorld,
                currentResources,
                _viewModel.ResourceGenerationSettings,
                currentSeasons,
                _viewModel.SeasonEnabledIds,
                _viewModel.SeasonSavedGeneration,
                _viewModel.LastGenerationOptions)
            .ShowDialog<NewWorldDialogResult?>(this);
        if (result is null)
        {
            return;
        }

        try
        {
            _viewModel.RegenerateWorld(
                result.World,
                result.GenerationResult,
                result.ResourceRegenerationResult,
                result.SeasonRegenerationResult);
            _worldCanvas.NotifyWorldChanged();
            _worldCanvas.ZoomToFit();
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or WorldValidationException)
        {
            await ShowErrorAsync("World could not be regenerated", exception.Message);
        }
    }

    private async Task RegenerateResourcesAsync()
    {
        if (_viewModel.IsBusy ||
            CancelActiveStroke("Active map stroke cancelled. Choose Regenerate resources again when ready.") ||
            !_viewModel.CanRegenerateResources ||
            _viewModel.World is not { } world ||
            _viewModel.ResourceMap is not { } resources ||
            _viewModel.ResourceTerrainQuery is not { } terrainQuery)
        {
            return;
        }

        CampaignResourceGenerationSettings initialSettings;
        try
        {
            initialSettings = await PrepareGenerationDialogAsync(
                "Preparing resource generation…",
                _viewModel.ResolveInitialResourceGenerationSettings);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or WorldValidationException)
        {
            await ShowErrorAsync("Resource generation could not start", exception.Message);
            return;
        }

        ResourceGenerationDialogResult? result;
        try
        {
            result = await new ResourceGenerationDialog(
                    world,
                    resources,
                    terrainQuery,
                    initialSettings)
                .ShowDialog<ResourceGenerationDialogResult?>(this);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or WorldValidationException)
        {
            await ShowErrorAsync("Resource generation could not start", exception.Message);
            return;
        }

        if (result is null)
        {
            return;
        }

        try
        {
            _viewModel.AcceptResourceGeneration(result.GenerationResult);
            _worldCanvas.NotifyWorldChanged();
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or WorldValidationException)
        {
            await ShowErrorAsync("Resources could not be regenerated", exception.Message);
        }
    }

    private async Task GenerateSeasonsAsync()
    {
        if (_viewModel.IsBusy ||
            CancelActiveStroke("Active map stroke cancelled. Choose Generate seasons again when ready.") ||
            !_viewModel.CanGenerateSeasons ||
            _viewModel.World is not { } world ||
            _viewModel.SeasonMap is not { } seasons ||
            _viewModel.SeasonTerrainQuery is not { } terrainQuery)
        {
            return;
        }

        CampaignSeasonGenerationSettings initialSettings;
        try
        {
            initialSettings = await PrepareGenerationDialogAsync(
                "Preparing Season generation…",
                _viewModel.ResolveInitialSeasonGenerationSettings);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or WorldValidationException)
        {
            await ShowErrorAsync("Season generation could not start", exception.Message);
            return;
        }

        SeasonGenerationDialogResult? result;
        try
        {
            result = await new SeasonGenerationDialog(
                    world,
                    seasons,
                    terrainQuery,
                    initialSettings)
                .ShowDialog<SeasonGenerationDialogResult?>(this);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or WorldValidationException)
        {
            await ShowErrorAsync("Season generation could not start", exception.Message);
            return;
        }

        if (result is null)
        {
            return;
        }

        try
        {
            _viewModel.AcceptSeasonGeneration(result.GenerationResult);
            _worldCanvas.NotifyWorldChanged();
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or WorldValidationException)
        {
            await ShowErrorAsync("Seasons could not be generated", exception.Message);
        }
    }

    private async Task OpenWorldAsync()
    {
        if (_viewModel.IsBusy ||
            CancelActiveStroke("Active map stroke cancelled. Choose Open again when ready.") ||
            !await ConfirmReplacementAsync())
        {
            return;
        }

        if (!StorageProvider.CanOpen)
        {
            await ShowErrorAsync("Open is unavailable", "This desktop platform did not provide a file picker.");
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Kingdom world.json",
            AllowMultiple = false,
            FileTypeFilter = [WorldManifestFileType],
            SuggestedFileType = WorldManifestFileType,
        });
        var manifestPath = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            return;
        }

        try
        {
            SetBusy(true, "Opening world…");
            var result = await CampaignEditorProjectSerializer.LoadWithSeasonsAsync(manifestPath);
            _viewModel.OpenWorld(
                result.World,
                result.ResourceMap,
                result.ResourceGenerationSettings,
                result.SeasonMap,
                result.SeasonEnabledIds,
                result.SeasonSavedGeneration,
                result.WasConvertedFromLegacy ? null : result.SourceProjectDirectory,
                result.WasConvertedFromLegacy,
                result.SourceProjectDirectory,
                result.NormalizedLegacyCoastalTileCount,
                result.SeasonsWereImplicitCompatibility);
            if (result.SeasonSavedGeneration is not null)
            {
                try
                {
                    await _viewModel.RebuildSeasonDiagnosticsAsync();
                }
                catch (Exception exception) when (
                    exception is ArgumentException or InvalidOperationException or OverflowException)
                {
                    await ShowErrorAsync(
                        "Season diagnostics are unavailable",
                        exception.Message + " The authoritative Season Layer still opened unchanged.");
                }
            }

            _worldCanvas.ZoomToFit();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or WorldFormatException)
        {
            await ShowErrorAsync("World could not be opened", exception.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task ManageCustomTerrainTypesAsync()
    {
        if (_viewModel.IsBusy ||
            CancelActiveStroke("Active map stroke cancelled. Open Custom tile types again when ready.") ||
            _viewModel.World is not { } world)
        {
            return;
        }

        var usedIds = world.Tiles.CustomTerrainDefinitions
            .Where(definition => world.Tiles.GetCustomTerrainUsageCount(definition.Id) > 0)
            .Select(static definition => definition.Id)
            .ToHashSet(StringComparer.Ordinal);
        var updated = await new CustomTerrainTypesDialog(
                world.Tiles.CustomTerrainDefinitions,
                usedIds)
            .ShowDialog<IReadOnlyList<CampaignCustomTerrainDefinition>?>(this);
        if (updated is null)
        {
            return;
        }

        try
        {
            if (_viewModel.UpdateCustomTerrainTypes(updated))
            {
                _worldCanvas.NotifyWorldChanged();
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            await ShowErrorAsync("Custom tile types could not be updated", exception.Message);
        }
    }

    private async Task ManageCustomResourcesAsync()
    {
        if (_viewModel.IsBusy ||
            CancelActiveStroke("Active map stroke cancelled. Open Custom resources again when ready.") ||
            _viewModel.World is not { } world ||
            _viewModel.ResourceMap is not { } resources)
        {
            return;
        }

        var usageCounts = resources.GetUsageCounts(
            resources.Catalog.CustomDefinitions.Select(static definition => definition.Id));
        var result = await new CustomResourcesDialog(
                resources.Catalog.CustomDefinitions,
                usageCounts,
                world.Tiles.CustomTerrainDefinitions)
            .ShowDialog<CustomResourcesDialogResult?>(this);
        if (result is null)
        {
            return;
        }

        try
        {
            if (_viewModel.UpdateCustomResources(result.Definitions, result.SelectedResourceId))
            {
                _worldCanvas.NotifyWorldChanged();
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            await ShowErrorAsync("Custom resources could not be updated", exception.Message);
        }
    }

    private async Task ManageCustomSeasonsAsync()
    {
        if (_viewModel.IsBusy ||
            CancelActiveStroke("Active map stroke cancelled. Open Manage seasons again when ready.") ||
            _viewModel.SeasonMap is not { } seasons)
        {
            return;
        }

        var usageCounts = seasons.GetUsageCounts(
            seasons.Catalog.Definitions.Select(static definition => definition.Id));
        var result = await new CustomSeasonsDialog(
                seasons.Catalog,
                _viewModel.SeasonEnabledIds,
                usageCounts)
            .ShowDialog<CustomSeasonsDialogResult?>(this);
        if (result is null)
        {
            return;
        }

        try
        {
            if (_viewModel.UpdateSeasons(
                    result.BuiltInDefinitions,
                    result.CustomDefinitions,
                    result.EnabledSeasonIds,
                    result.DeletedSeasonReplacements,
                    result.SelectedSeasonId))
            {
                _worldCanvas.NotifyWorldChanged();
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            await ShowErrorAsync("Seasons could not be updated", exception.Message);
        }
    }

    private async Task<bool> SaveWorldAsync(bool forceChooseDirectory)
    {
        if (_viewModel.IsBusy ||
            CancelActiveStroke("Active map stroke cancelled. Choose Save again when ready.") ||
            _viewModel.World is null)
        {
            return false;
        }

        var projectDirectory = forceChooseDirectory ? null : _viewModel.ProjectDirectory;
        if (projectDirectory is null)
        {
            projectDirectory = await ChooseProjectDirectoryAsync();
            if (projectDirectory is null)
            {
                return false;
            }

            if (_viewModel.IsLegacyImportPending &&
                string.Equals(
                    Path.GetFullPath(projectDirectory),
                    Path.GetFullPath(_viewModel.ImportSourceDirectory!),
                    StringComparison.OrdinalIgnoreCase))
            {
                await ShowErrorAsync(
                    "Choose a new folder",
                    "Converted legacy worlds must be saved to a different folder so the version-1 source stays unchanged.");
                return false;
            }

            var existingManagedFiles = CampaignEditorProjectSerializer.ManagedFileNamesWithSeasons
                .Where(fileName => File.Exists(Path.Combine(projectDirectory, fileName)))
                .ToArray();
            if (existingManagedFiles.Length > 0 &&
                !string.Equals(projectDirectory, _viewModel.ProjectDirectory, StringComparison.OrdinalIgnoreCase))
            {
                var replace = await new ChoiceDialog(
                    "Replace world",
                    "Replace the world in this folder?",
                    $"This folder contains {existingManagedFiles.Length:N0} managed world/resource file(s): " +
                    $"{string.Join(", ", existingManagedFiles)}. The complete editor document will replace them.",
                    "Replace",
                    cancelText: "Cancel").ShowDialog<DialogChoice>(this);
                if (replace != DialogChoice.Primary)
                {
                    return false;
                }
            }
        }

        try
        {
            SetBusy(true, "Staging terrain, resource, and season data…");
            await CampaignEditorProjectSerializer.SaveWithSeasonsAsync(
                _viewModel.World,
                _viewModel.ResourceMap ?? throw new InvalidOperationException("The resource layer is unavailable."),
                _viewModel.ResourceGenerationSettings,
                _viewModel.SeasonMap ?? throw new InvalidOperationException("The season layer is unavailable."),
                _viewModel.SeasonEnabledIds,
                _viewModel.SeasonSavedGeneration,
                projectDirectory);
            _viewModel.MarkSaved(projectDirectory);
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or UnauthorizedAccessException or
            InvalidOperationException or WorldFormatException or WorldValidationException)
        {
            await ShowErrorAsync("World could not be saved", exception.Message);
            return false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task<string?> ChooseProjectDirectoryAsync()
    {
        if (!StorageProvider.CanPickFolder)
        {
            await ShowErrorAsync("Save As is unavailable", "This desktop platform did not provide a folder picker.");
            return null;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose or create a world project folder",
            AllowMultiple = false,
        });
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    private async Task ExportRuntimeDataAsync()
    {
        if (_viewModel.IsBusy ||
            CancelActiveStroke("Active map stroke cancelled. Choose Export again when ready.") ||
            _viewModel.World is not { } world)
        {
            return;
        }

        if (!StorageProvider.CanSave)
        {
            await ShowErrorAsync(
                "Export is unavailable",
                "This desktop platform did not provide a file picker for the runtime package.");
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export runtime world data",
            SuggestedFileName = GetSuggestedRuntimePackageName(),
            DefaultExtension = CampaignWorldRuntimeExporter.PackageExtension.TrimStart('.'),
            FileTypeChoices = [RuntimeWorldPackageFileType],
            SuggestedFileType = RuntimeWorldPackageFileType,
            ShowOverwritePrompt = true,
        });
        var packagePath = file?.TryGetLocalPath();
        if (file is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(packagePath))
        {
            await ShowErrorAsync(
                "Choose a local export file",
                "Runtime packages currently require a normal local .kworld file path.");
            return;
        }

        try
        {
            SetBusy(true, "Exporting terrain, resource, and season runtime data…");
            var resources = _viewModel.ResourceMap
                ?? throw new InvalidOperationException("The resource layer is unavailable.");
            var seasons = _viewModel.SeasonMap
                ?? throw new InvalidOperationException("The season layer is unavailable.");
            await CampaignEditorProjectSerializer.ExportWithSeasonsAsync(
                world,
                resources,
                seasons,
                packagePath);
            _viewModel.StatusMessage =
                $"Exported {Path.GetFileName(packagePath)} · {world.Definition.TileCount:N0} terrain records + " +
                $"{resources.OccurrenceCount:N0} resource occurrence(s) + " +
                $"{seasons.OccurrenceCount:N0} season occurrence record(s) in runtime package v3.";
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or UnauthorizedAccessException or
            InvalidOperationException or OverflowException)
        {
            await ShowErrorAsync("Runtime data could not be exported", exception.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task ExportJsonDataAsync()
    {
        if (_viewModel.IsBusy ||
            CancelActiveStroke("Active map stroke cancelled. Choose Export JSON again when ready.") ||
            _viewModel.World is not { } world)
        {
            return;
        }

        if (!StorageProvider.CanSave)
        {
            await ShowErrorAsync(
                "JSON export is unavailable",
                "This desktop platform did not provide a file picker for JSON runtime data.");
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export world as JSON data",
            SuggestedFileName = GetSuggestedRuntimeJsonName(),
            DefaultExtension = CampaignWorldJsonExporter.FileExtension.TrimStart('.'),
            FileTypeChoices = [RuntimeWorldJsonFileType],
            SuggestedFileType = RuntimeWorldJsonFileType,
            ShowOverwritePrompt = true,
        });
        var jsonPath = file?.TryGetLocalPath();
        if (file is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(jsonPath))
        {
            await ShowErrorAsync(
                "Choose a local JSON export file",
                "Runtime JSON export requires a normal local .json file path.");
            return;
        }

        try
        {
            SetBusy(true, "Exporting one self-contained JSON world file…");
            var resources = _viewModel.ResourceMap
                ?? throw new InvalidOperationException("The resource layer is unavailable.");
            var seasons = _viewModel.SeasonMap
                ?? throw new InvalidOperationException("The season layer is unavailable.");
            await CampaignEditorProjectSerializer.ExportJsonWithSeasonsAsync(
                world,
                resources,
                seasons,
                jsonPath);
            _viewModel.StatusMessage =
                $"Exported {Path.GetFileName(jsonPath)} · {world.Definition.TileCount:N0} complete tile record(s) + " +
                $"{resources.OccurrenceCount:N0} resource occurrence(s) + " +
                $"{seasons.OccurrenceCount:N0} season occurrence(s) in one JSON file.";
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or UnauthorizedAccessException or
            InvalidOperationException or OverflowException)
        {
            await ShowErrorAsync("JSON data could not be exported", exception.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private string GetSuggestedRuntimePackageName() =>
        GetSuggestedExportName(CampaignWorldRuntimeExporter.PackageExtension);

    private string GetSuggestedRuntimeJsonName() =>
        GetSuggestedExportName(CampaignWorldJsonExporter.SuggestedFileSuffix);

    private string GetSuggestedExportName(string suffix)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
        var safeName = new string(_viewModel.WorldName
            .Select(character => invalidCharacters.Contains(character) ? '_' : character)
            .ToArray())
            .Trim();
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "world";
        }

        return safeName.EndsWith(
            suffix,
            StringComparison.OrdinalIgnoreCase)
            ? safeName
            : safeName + suffix;
    }

    private async Task<bool> ConfirmReplacementAsync()
    {
        if (!_viewModel.IsDirty)
        {
            return true;
        }

        var choice = await new ChoiceDialog(
            "Unsaved world",
            "Save changes before continuing?",
            "The current campaign terrain, resources, or seasons are not written to a project folder.",
            "Save",
            "Discard",
            "Cancel").ShowDialog<DialogChoice>(this);
        return choice switch
        {
            DialogChoice.Primary => await SaveWorldAsync(forceChooseDirectory: false),
            DialogChoice.Secondary => true,
            _ => false,
        };
    }

    private void Undo()
    {
        if (CancelActiveStroke("Active map stroke cancelled. Choose Undo again when ready."))
        {
            return;
        }

        if (_viewModel.Undo())
        {
            _worldCanvas.NotifyWorldChanged();
        }
    }

    private void Redo()
    {
        if (CancelActiveStroke("Active map stroke cancelled. Choose Redo again when ready."))
        {
            return;
        }

        if (_viewModel.Redo())
        {
            _worldCanvas.NotifyWorldChanged();
        }
    }

    private async void MainWindow_OnClosing(object? sender, WindowClosingEventArgs e)
    {
        CancelActiveStroke("Active map stroke cancelled before closing.");
        if (_closeApproved || !_viewModel.IsDirty)
        {
            return;
        }

        e.Cancel = true;
        if (_closePromptActive)
        {
            return;
        }

        _closePromptActive = true;
        try
        {
            if (await ConfirmReplacementAsync())
            {
                _closeApproved = true;
                Close();
            }
        }
        finally
        {
            _closePromptActive = false;
        }
    }

    private bool CancelActiveStroke(string statusMessage)
    {
        if (!_worldCanvas.HasActiveStroke)
        {
            return false;
        }

        _worldCanvas.CancelActiveInteraction();
        _viewModel.StatusMessage = statusMessage;
        return true;
    }

    private async Task ShowErrorAsync(string heading, string message)
    {
        await new ChoiceDialog(
            "Kingdom World Editor",
            heading,
            message,
            "OK",
            cancelText: string.Empty).ShowDialog<DialogChoice>(this);
    }

    private async Task<T> PrepareGenerationDialogAsync<T>(string message, Func<T> prepare)
    {
        var previousStatus = _viewModel.StatusMessage;
        SetBusy(true, message);
        try
        {
            await Avalonia.Threading.Dispatcher.Yield(
                Avalonia.Threading.DispatcherPriority.Background);
            return prepare();
        }
        finally
        {
            SetBusy(false, previousStatus);
        }
    }

    private void SetBusy(bool busy, string? message = null)
    {
        _viewModel.IsBusy = busy;
        if (message is not null)
        {
            _viewModel.StatusMessage = message;
        }
    }
}
