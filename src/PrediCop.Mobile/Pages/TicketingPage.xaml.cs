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
        EntryPlate.Keyboard = Keyboard.Create(KeyboardFlags.CapitalizeCharacter | KeyboardFlags.Suggestions);

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TicketingViewModel.ShowForm) && !_vm.ShowForm)
                DismissKeyboard();
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadRecentCommand.Execute(null);
    }

    private void OnPlateTextChanged(object sender, TextChangedEventArgs e)
    {
        if (e.NewTextValue is null) return;
        var upper = e.NewTextValue.ToUpperInvariant();
        if (upper == e.NewTextValue) return;
        var entry = (Entry)sender;
        int cursor = entry.CursorPosition;
        entry.Text = upper;
        entry.CursorPosition = Math.Min(cursor, upper.Length);
    }

    private static void DismissKeyboard()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
#if ANDROID
            if (Platform.CurrentActivity?.CurrentFocus is Android.Views.View v)
            {
                var imm = (Android.Views.InputMethods.InputMethodManager?)
                    Platform.CurrentActivity.GetSystemService(Android.Content.Context.InputMethodService);
                imm?.HideSoftInputFromWindow(v.WindowToken, 0);
                v.ClearFocus();
            }
#elif IOS || MACCATALYST
            UIKit.UIApplication.SharedApplication.SendAction(
                new ObjCRuntime.Selector("resignFirstResponder"), null, null, null);
#endif
        });
    }

    private async void OnTicketTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not TicketSummary summary) return;
        await Navigation.PushAsync(new TicketDetailPage(summary.Id, _vm.ApiServiceRef, summary.IsToday));
    }
}
