namespace AICompanion.Settings
{
    public partial class SettingsPanel
    {
        void BuildPortraitToggle()
        {
            AddToggle("Show expression portrait", _settings.ShowExpressionPortrait, value => _settings.ShowExpressionPortrait = value);
            Note("Images load from %APPDATA%\\ProjectLeia\\portraits. Default is off.");
        }
    }
}
