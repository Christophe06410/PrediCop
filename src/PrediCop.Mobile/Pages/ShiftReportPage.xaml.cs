using PrediCop.Mobile.ViewModels;

namespace PrediCop.Mobile.Pages;

public partial class ShiftReportPage : ContentPage
{
    private readonly ShiftReportViewModel _vm;

    public ShiftReportPage(ShiftReportViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }
}
