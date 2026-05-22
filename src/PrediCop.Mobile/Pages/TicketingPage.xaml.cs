using PrediCop.Mobile.ViewModels;

namespace PrediCop.Mobile.Pages;

public partial class TicketingPage : ContentPage
{
    private readonly TicketingViewModel _vm;

    public TicketingPage(TicketingViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadRecentCommand.Execute(null);
    }
}
