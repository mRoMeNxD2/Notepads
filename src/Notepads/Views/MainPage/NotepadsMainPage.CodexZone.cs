// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2019-2024, Jiaqi (0x7c13) Liu. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Views.MainPage
{
    using Notepads.Controls.TextEditor;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Controls.Primitives;

    public sealed partial class NotepadsMainPage
    {
        private bool _isCodexZoneToggleUpdating;

        private void Sets_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateCodexZoneToggleState();
        }

        private void CodexZoneToggle_OnToggled(object sender, RoutedEventArgs e)
        {
            if (_isCodexZoneToggleUpdating)
            {
                return;
            }

            if (!(sender is ToggleButton toggleButton))
            {
                return;
            }

            if (NotepadsCore.GetSelectedTextEditor() is ITextEditor textEditor)
            {
                textEditor.IsCodexZoneEnabled = toggleButton.IsChecked == true;
            }
        }

        private void UpdateCodexZoneToggleState()
        {
            if (CodexZoneToggle == null)
            {
                return;
            }

            _isCodexZoneToggleUpdating = true;
            var textEditor = NotepadsCore.GetSelectedTextEditor();
            CodexZoneToggle.IsChecked = textEditor?.IsCodexZoneEnabled ?? false;
            _isCodexZoneToggleUpdating = false;
        }
    }
}
