using PrediCop.Mobile.ViewModels;

namespace PrediCop.Mobile.Pages;

public partial class PatrolActivationPage : ContentPage
{
    public PatrolActivationPage(PatrolActivationViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = ((PatrolActivationViewModel)BindingContext).LoadAsync();
    }
}
