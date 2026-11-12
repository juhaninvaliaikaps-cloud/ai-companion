namespace AICompanion.Settings
{
    public partial class SettingsPanel
    {
        void BuildPortraitToggle()
        {
            AddToggle("Show expression portrait", _settings.ShowExpressionPortrait, value => _settings.ShowExpressionPortrait = value);
            Note("Images load from the persona folder, or %APPDATA%\\ProjectLeia\\portraits. Default is off.");
        AddToggle("Intimate expressions (18+)", _settings.IntimateExpressions, value =>
        {
            _settings.IntimateExpressions = value;
            ApplyLive();
            _rebuild = true;
        });
        Note("Adult drawn expressions load only from %APPDATA%\\ProjectLeia\\portraits-adult while this is on. Off by default; they are not shown or offered to the model.");
        }
    }
}
