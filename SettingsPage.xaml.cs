using System.Windows.Controls;

namespace MarvinsAIRARefactoredSimHub
{
    public partial class PluginControl : UserControl
    {
        private readonly PluginSettings _settings;

        public PluginControl(PluginSettings settings)
        {
            InitializeComponent();
            var _settings = settings;

            overlaysShowInPracticeCheckbox.IsChecked = _settings.OverlaysShowInPractice;
            overlaysShowInQualifyingCheckbox.IsChecked = _settings.OverlaysShowInQualifying;
            overlaysShowInRaceCheckbox.IsChecked = _settings.OverlaysShowInRace;
            overlaysShowInTestDriveCheckbox.IsChecked = _settings.OverlaysShowInTestDrive;

            overlaysShowInPracticeCheckbox.Checked += (s, e) => { _settings.OverlaysShowInPractice = true; };
            overlaysShowInPracticeCheckbox.Unchecked += (s, e) => { _settings.OverlaysShowInPractice = false; };

            overlaysShowInQualifyingCheckbox.Checked += (s, e) => { _settings.OverlaysShowInQualifying = true; };
            overlaysShowInQualifyingCheckbox.Unchecked += (s, e) => { _settings.OverlaysShowInQualifying = false; };

            overlaysShowInRaceCheckbox.Checked += (s, e) => { _settings.OverlaysShowInRace = true; };
            overlaysShowInRaceCheckbox.Unchecked += (s, e) => { _settings.OverlaysShowInRace = false; };

            overlaysShowInTestDriveCheckbox.Checked += (s, e) => { _settings.OverlaysShowInTestDrive = true; };
            overlaysShowInTestDriveCheckbox.Unchecked += (s, e) => { _settings.OverlaysShowInTestDrive = false; };
        }
    }
}
