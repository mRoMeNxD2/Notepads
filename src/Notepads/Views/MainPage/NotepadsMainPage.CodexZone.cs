// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2019-2024, Jiaqi (0x7c13) Liu. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Views.MainPage
{
    using Notepads.Services;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls.Primitives;

    /// <summary>
    /// Partial class for NotepadsMainPage that handles CodexZone functionality
    /// </summary>
    public sealed partial class NotepadsMainPage
    {
        private bool _isCodexZoneEnabled = false;

        /// <summary>
        /// Handles the CodexZone toggle button click event
        /// </summary>
        private void CodexZoneToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggleButton)
            {
                _isCodexZoneEnabled = toggleButton.IsChecked == true;
                
                // Apply CodexZone mode to all open editors
                var allEditors = NotepadsCore.GetAllTextEditors();
                foreach (var editor in allEditors)
                {
                    editor.IsCodexZoneEnabled = _isCodexZoneEnabled;
                }

                // Show notification
                if (_isCodexZoneEnabled)
                {
                    NotificationCenter.Instance.PostNotification(
                        _resourceLoader.GetString("CodexZone_Enabled") ?? "Codex Zone enabled - Code highlighting active", 
                        1500);
                    AnalyticsService.TrackEvent("CodexZone_Enabled");
                }
                else
                {
                    NotificationCenter.Instance.PostNotification(
                        _resourceLoader.GetString("CodexZone_Disabled") ?? "Codex Zone disabled", 
                        1500);
                    AnalyticsService.TrackEvent("CodexZone_Disabled");
                }

                LoggingService.LogInfo($"[NotepadsMainPage] CodexZone mode {(_isCodexZoneEnabled ? "enabled" : "disabled")}", consoleOnly: true);
            }
        }

        /// <summary>
        /// Applies CodexZone mode to a newly created or loaded editor
        /// </summary>
        private void ApplyCodexZoneModeToEditor(Controls.TextEditor.ITextEditor textEditor)
        {
            if (textEditor != null)
            {
                textEditor.IsCodexZoneEnabled = _isCodexZoneEnabled;
            }
        }
    }
}
