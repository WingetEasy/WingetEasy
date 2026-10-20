using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using WingetEasy.Core.Interfaces;
using WingetEasy.Core.Models;

namespace WingetEasy.App.Views;

public sealed partial class OnboardingDialog : ContentDialog
{
    private const string OnboardingCompletedKey = "OnboardingCompleted";
    private const string ScheduleConfigKey = "ScheduleConfig";
    private const string StartWithWindowsRunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string StartWithWindowsValueName = "WingetEasy";

    private readonly ISettingsRepository _settingsRepository;
    private readonly ISchedulerService _schedulerService;
    private bool _completed;

    public OnboardingDialog()
    {
        InitializeComponent();
        _settingsRepository = App.Services.GetRequiredService<ISettingsRepository>();
        _schedulerService = App.Services.GetRequiredService<ISchedulerService>();
        UpdateStepIndicators();
    }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (StepsFlipView.SelectedIndex < StepsFlipView.Items.Count - 1)
        {
            args.Cancel = true;
            StepsFlipView.SelectedIndex++;
            return;
        }

        var deferral = args.GetDeferral();
        try
        {
            var config = await SavePreferencesAsync().ConfigureAwait(true);
            _completed = true;
            deferral.Complete();
            _ = UpdateScheduleAfterCloseAsync(config);
        }
        catch (Exception ex)
        {
            args.Cancel = true;
            Serilog.Log.Error(ex, "Não foi possível concluir o onboarding do WingetEasy.");
            if (!_completed)
            {
                deferral.Complete();
            }
        }
    }

    private async Task UpdateScheduleAfterCloseAsync(ScheduleConfig config)
    {
        try
        {
            await _schedulerService.UpdateScheduleAsync(config).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Não foi possível atualizar o agendamento após concluir o onboarding.");
        }
    }

    private void OnSecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (StepsFlipView.SelectedIndex > 0)
        {
            args.Cancel = true;
            StepsFlipView.SelectedIndex--;
        }
    }

    private void StepsFlipView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateStepIndicators();
    }

    private void UpdateStepIndicators()
    {
        if (StepsFlipView is null || StepOne is null || StepTwo is null || StepThree is null || StepFour is null)
        {
            return;
        }

        SecondaryButtonText = StepsFlipView.SelectedIndex > 0 ? "Voltar" : string.Empty;
        PrimaryButtonText = StepsFlipView.SelectedIndex == StepsFlipView.Items.Count - 1
            ? "Concluir"
            : "Próximo";

        var indicators = new[] { StepOne, StepTwo, StepThree, StepFour };
        for (var index = 0; index < indicators.Length; index++)
        {
            indicators[index].Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                index == StepsFlipView.SelectedIndex
                    ? Microsoft.UI.Colors.DodgerBlue
                    : Microsoft.UI.Colors.LightGray);
        }
    }

    private async Task<ScheduleConfig> SavePreferencesAsync()
    {
        var selectedItem = FrequencyComboBox.SelectedItem as ComboBoxItem;
        var startWithWindows = StartWithWindowsToggle.IsOn;
        var frequency = Enum.TryParse<ScheduleFrequency>(selectedItem?.Tag?.ToString(), out var parsedFrequency)
            ? parsedFrequency
            : ScheduleFrequency.OnceDaily;
        var config = new ScheduleConfig(frequency, TimeSpan.Zero);

        await _settingsRepository.SetAsync(ScheduleConfigKey, config).ConfigureAwait(true);
        SetStartWithWindows(startWithWindows);
        await _settingsRepository.SetAsync(OnboardingCompletedKey, "true").ConfigureAwait(true);
        return config;
    }

    private static void SetStartWithWindows(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartWithWindowsRunKey, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(StartWithWindowsRunKey);
        if (key is null)
        {
            throw new InvalidOperationException("Não foi possível acessar as configurações de inicialização do Windows.");
        }

        if (enabled)
        {
            var executablePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("Não foi possível determinar o executável do aplicativo.");
            key.SetValue(StartWithWindowsValueName, $"\"{executablePath}\" --minimized");
        }
        else
        {
            key.DeleteValue(StartWithWindowsValueName, throwOnMissingValue: false);
        }
    }

    private void Dialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
    {
        if (!_completed && args.Result == ContentDialogResult.None)
        {
            // Fechar pelo X não grava preferências; o diálogo será mostrado novamente.
        }
    }
}
