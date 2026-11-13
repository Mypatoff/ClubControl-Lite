using System;
using System.Threading.Tasks;
using System.Windows;
using ClubControl.Server.Data;
using ClubControl.Server.Data.Models;
using ClubControl.Server.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubControl.Server.Windows;

/// <summary>
/// Manually adds an offline PC: one with no LAN connection, so no client ever connects to it. It is billed
/// by hand from the admin panel like any other PC, it just has no lock screen, no online tracking and no power commands.
/// </summary>
public partial class AddOfflinePcWindow : Window
{
    private readonly IServiceScope _scope;
    private readonly ClubDbContext _db;

    public AddOfflinePcWindow(IServiceProvider services)
    {
        InitializeComponent();
        _scope = services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<ClubDbContext>();

        Title = Strings.Get("AddComputerTitle");
        HeaderText.Text = Title;
        NumberLabel.Text = Strings.Get("LabelNumber");
        TariffLabel.Text = Strings.Get("LabelTariff");
        EnabledCheckBox.Content = Strings.Get("ColumnEnabled");
        CancelButton.Content = Strings.Get("Cancel");
        AddButton.Content = Strings.Get("ButtonAdd");

        Loaded += async (_, _) => await LoadTariffsAsync();
        Closed += (_, _) => _scope.Dispose();
    }

    private async Task LoadTariffsAsync()
    {
        var tariffs = await TariffsHelper.GetAllAsync(_db);
        TariffCombo.ItemsSource = tariffs;

        if (tariffs.Count == 0)
            ErrorText.Text = Strings.Get("NoTariffsForKind");
        else
            TariffCombo.SelectedIndex = 0;
    }

    private async void OnOk(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(NumberBox.Text, out var number))
        {
            ErrorText.Text = Strings.Get("EnterAValidNumber");
            return;
        }

        if (await _db.Pcs.AnyAsync(p => p.Number == number))
        {
            ErrorText.Text = Strings.Get("PcNumbersMustBeUnique");
            return;
        }

        if (TariffCombo.SelectedItem is not Tariff tariff)
        {
            ErrorText.Text = Strings.Get("ChooseATariff");
            return;
        }

        _db.Pcs.Add(new Pc
        {
            MachineGuid = Guid.NewGuid(),
            IsManual = true,
            Number = number,
            TariffId = tariff.Id,
            Enabled = EnabledCheckBox.IsChecked == true
        });
        await _db.SaveChangesAsync();

        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
