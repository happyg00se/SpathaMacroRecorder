using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using SpathaMacroRecorder.Models;
using SpathaMacroRecorder.Services;
using SpathaMacroRecorder.Views;

namespace SpathaMacroRecorder.ViewModels;

/// <summary>Левая панель: список профилей + CRUD (создать/переименовать/дублировать/удалить/импорт/экспорт).</summary>
internal partial class ProfileListViewModel : ObservableObject
{
    private readonly ProfileManager _profileManager;
    private readonly ILogger<ProfileListViewModel> _logger;

    public ObservableCollection<MacroProfile> Profiles { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RenameProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(DuplicateProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportProfileCommand))]
    private MacroProfile? _selectedProfile;

    public ProfileListViewModel(ProfileManager profileManager, ILogger<ProfileListViewModel> logger)
    {
        _profileManager = profileManager;
        _logger = logger;
        _profileManager.ProfileLoadFailed += OnProfileLoadFailed;

        Reload();
    }

    private void Reload()
    {
        Profiles.Clear();
        foreach (var profile in _profileManager.LoadAll().OrderBy(p => p.ProfileName, StringComparer.CurrentCultureIgnoreCase))
        {
            Profiles.Add(profile);
        }

        SelectedProfile = Profiles.FirstOrDefault();
    }

    private void OnProfileLoadFailed(string path, Exception ex)
    {
        _logger.LogWarning(ex, "Failed to load profile {Path}", path);
        MessageBox.Show(
            $"{AppText.Instance["ProfileBroken"]}\n{path}\n\n{ex.Message}",
            AppText.Instance["ProfileLoadError"],
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    [RelayCommand]
    private void CreateProfile()
    {
        string? name = TextPromptDialog.Show(Application.Current.MainWindow, AppText.Instance["NewProfile"], AppText.Instance["ProfileName"], AppText.Instance["NewProfile"]);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            var profile = _profileManager.Create(name);
            Profiles.Add(profile);
            SelectedProfile = profile;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            ShowError(ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void RenameProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        string? name = TextPromptDialog.Show(Application.Current.MainWindow, AppText.Instance["RenameProfile"], AppText.Instance["NewName"], SelectedProfile.ProfileName);
        if (string.IsNullOrWhiteSpace(name) || name == SelectedProfile.ProfileName)
        {
            return;
        }

        try
        {
            var renamed = _profileManager.Rename(SelectedProfile, name);
            int index = Profiles.IndexOf(SelectedProfile);
            Profiles[index] = renamed;
            SelectedProfile = renamed;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            ShowError(ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DuplicateProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        string? name = TextPromptDialog.Show(
            Application.Current.MainWindow, AppText.Instance["DuplicateProfile"], AppText.Instance["CopyName"], $"{SelectedProfile.ProfileName} ({AppText.Instance["CopySuffix"]})");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            var duplicate = _profileManager.Duplicate(SelectedProfile, name);
            Profiles.Add(duplicate);
            SelectedProfile = duplicate;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            ShowError(ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DeleteProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var result = MessageBox.Show(
            string.Format(AppText.Instance["DeleteProfileQ"], SelectedProfile.ProfileName),
            AppText.Instance["DeleteProfileTitle"],
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _profileManager.Delete(SelectedProfile);
        Profiles.Remove(SelectedProfile);
        SelectedProfile = Profiles.FirstOrDefault();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ExportProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            FileName = SelectedProfile.ProfileName,
            DefaultExt = ".json",
            Filter = AppText.Instance["ProfileFilter"],
        };

        if (dialog.ShowDialog() == true)
        {
            _profileManager.Export(SelectedProfile, dialog.FileName);
        }
    }

    [RelayCommand]
    private void ImportProfile()
    {
        var dialog = new OpenFileDialog
        {
            DefaultExt = ".json",
            Filter = AppText.Instance["ProfileFilter"],
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var imported = _profileManager.Import(dialog.FileName);
            Profiles.Add(imported);
            SelectedProfile = imported;
        }
        catch (Exception ex)
        {
            ShowError($"{AppText.Instance["ImportFailed"]}\n{ex.Message}");
        }
    }

    private bool HasSelection => SelectedProfile is not null;

    private static void ShowError(string message) =>
        MessageBox.Show(message, AppText.Instance["ErrorTitle"], MessageBoxButton.OK, MessageBoxImage.Error);
}
