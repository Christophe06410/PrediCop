namespace PrediCop.Mobile.Pages;

public partial class MainPage : TabbedPage
{
    public MainPage(MissionPage missions, PatrolPage patrol, MapPage map, ProfilePage profile)
    {
        InitializeComponent();
        Children.Clear();
        Children.Add(missions);
        Children.Add(patrol);
        Children.Add(map);
        Children.Add(profile);
    }
}
