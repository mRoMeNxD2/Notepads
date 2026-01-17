// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2019-2024, Jiaqi (0x7c13) Liu. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Controls.TextEditor
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Notepads.Services;
    using Windows.UI;
    using Windows.UI.Text;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Media;

    /// <summary>
    /// Partial class for TextEditorCore that implements CodexZone functionality
    /// (syntax highlighting and error indicators for code editing)
    /// </summary>
    public partial class TextEditorCore
    {
        private bool _isCodexZoneEnabled = false;
        private bool _isHighlightingInProgress = false;
        private CancellationTokenSource _highlightingCts;
        private readonly object _highlightingLock = new object();
        private int _lastHighlightedTextLength = 0;
        private string _lastHighlightedText = string.Empty;

        /// <summary>
        /// Event fired when CodexZone mode is toggled
        /// </summary>
        public event EventHandler<bool> CodexZoneModeChanged;

        /// <summary>
        /// Gets or sets whether CodexZone mode is enabled
        /// </summary>
        public bool IsCodexZoneEnabled
        {
            get => _isCodexZoneEnabled;
            set
            {
                if (_isCodexZoneEnabled != value)
                {
                    _isCodexZoneEnabled = value;
                    OnCodexZoneModeChanged(value);
                }
            }
        }

        /// <summary>
        /// Called when CodexZone mode changes
        /// </summary>
        private void OnCodexZoneModeChanged(bool enabled)
        {
            CodexZoneModeChanged?.Invoke(this, enabled);

            if (enabled)
            {
                // Apply syntax highlighting
                ApplyCodexZoneSyntaxHighlighting();
            }
            else
            {
                // Reset to default formatting
                ResetCodexZoneFormatting();
            }
        }

        /// <summary>
        /// Applies syntax highlighting to the current text
        /// </summary>
        public void ApplyCodexZoneSyntaxHighlighting()
        {
            if (!_isCodexZoneEnabled || !_loaded) return;

            lock (_highlightingLock)
            {
                if (_isHighlightingInProgress) return;
                _isHighlightingInProgress = true;
            }

            try
            {
                // Cancel any previous highlighting operation
                _highlightingCts?.Cancel();
                _highlightingCts = new CancellationTokenSource();

                var text = GetText();
                
                // Skip if text hasn't changed significantly
                if (text == _lastHighlightedText)
                {
                    return;
                }

                var isDarkTheme = IsDarkTheme();
                var tokens = CodexZoneSyntaxHighlighter.Tokenize(text);
                var errorPositions = CodexZoneErrorIndicator.GetErrorPositions(text);

                ApplyTokenColors(text, tokens, errorPositions, isDarkTheme);

                _lastHighlightedText = text;
                _lastHighlightedTextLength = text.Length;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[TextEditorCore.CodexZone] Error applying syntax highlighting: {ex.Message}");
            }
            finally
            {
                lock (_highlightingLock)
                {
                    _isHighlightingInProgress = false;
                }
            }
        }

        /// <summary>
        /// Applies token colors to the document
        /// </summary>
        private void ApplyTokenColors(string text, List<CodexZoneSyntaxHighlighter.Token> tokens, 
            List<(int Start, int Length)> errorPositions, bool isDarkTheme)
        {
            if (string.IsNullOrEmpty(text) || tokens == null) return;

            try
            {
                // Get default color
                var defaultColor = CodexZoneSyntaxHighlighter.GetColorForToken(
                    CodexZoneSyntaxHighlighter.TokenType.Default, isDarkTheme);
                var errorColor = CodexZoneSyntaxHighlighter.GetColorForToken(
                    CodexZoneSyntaxHighlighter.TokenType.Error, isDarkTheme);

                // Batch updates for better performance
                Document.BatchDisplayUpdates();

                try
                {
                    // Reset all text to default color
                    var fullRange = Document.GetRange(0, text.Length);
                    fullRange.CharacterFormat.ForegroundColor = defaultColor;
                    fullRange.CharacterFormat.Underline = UnderlineType.None;

                    // Apply syntax highlighting colors
                    foreach (var token in tokens)
                    {
                        if (token.Type != CodexZoneSyntaxHighlighter.TokenType.Default && 
                            token.Start >= 0 && 
                            token.Start + token.Length <= text.Length)
                        {
                            var range = Document.GetRange(token.Start, token.Start + token.Length);
                            range.CharacterFormat.ForegroundColor = CodexZoneSyntaxHighlighter.GetColorForToken(token.Type, isDarkTheme);
                        }
                    }

                    // Apply error underlines (wavy red underline for errors)
                    foreach (var error in errorPositions)
                    {
                        if (error.Start >= 0 && error.Start + error.Length <= text.Length)
                        {
                            var range = Document.GetRange(error.Start, error.Start + error.Length);
                            range.CharacterFormat.Underline = UnderlineType.Wave;
                            // Note: Wave underline color is typically controlled by the system
                            // But we set foreground to indicate the error location
                        }
                    }
                }
                finally
                {
                    Document.ApplyDisplayUpdates();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[TextEditorCore.CodexZone] Error applying token colors: {ex.Message}");
            }
        }

        /// <summary>
        /// Resets formatting to default (removes syntax highlighting)
        /// </summary>
        private void ResetCodexZoneFormatting()
        {
            try
            {
                _highlightingCts?.Cancel();
                _lastHighlightedText = string.Empty;
                _lastHighlightedTextLength = 0;

                var text = GetText();
                if (string.IsNullOrEmpty(text)) return;

                var isDarkTheme = IsDarkTheme();
                var defaultColor = isDarkTheme 
                    ? Color.FromArgb(255, 240, 240, 240)  // Light gray for dark theme
                    : Color.FromArgb(255, 0, 0, 0);       // Black for light theme

                Document.BatchDisplayUpdates();
                try
                {
                    var fullRange = Document.GetRange(0, text.Length);
                    fullRange.CharacterFormat.ForegroundColor = defaultColor;
                    fullRange.CharacterFormat.Underline = UnderlineType.None;
                }
                finally
                {
                    Document.ApplyDisplayUpdates();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[TextEditorCore.CodexZone] Error resetting formatting: {ex.Message}");
            }
        }

        /// <summary>
        /// Determines if the current theme is dark
        /// </summary>
        private bool IsDarkTheme()
        {
            try
            {
                var theme = ThemeSettingsService.ThemeMode;
                if (theme == ElementTheme.Dark)
                {
                    return true;
                }
                else if (theme == ElementTheme.Light)
                {
                    return false;
                }
                else // Default - check system theme
                {
                    var uiSettings = new Windows.UI.ViewManagement.UISettings();
                    var backgroundColor = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Background);
                    // If background is dark (low luminance), we're in dark mode
                    return backgroundColor.R < 128 && backgroundColor.G < 128 && backgroundColor.B < 128;
                }
            }
            catch
            {
                return true; // Default to dark theme
            }
        }

        /// <summary>
        /// Re-applies syntax highlighting after text changes (debounced)
        /// </summary>
        private async void ScheduleCodexZoneHighlighting()
        {
            if (!_isCodexZoneEnabled) return;

            try
            {
                _highlightingCts?.Cancel();
                _highlightingCts = new CancellationTokenSource();
                var token = _highlightingCts.Token;

                // Debounce: wait a bit before applying highlighting to avoid too many updates
                await Task.Delay(150, token);

                if (!token.IsCancellationRequested && _isCodexZoneEnabled)
                {
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Low, () =>
                    {
                        if (!token.IsCancellationRequested && _isCodexZoneEnabled)
                        {
                            ApplyCodexZoneSyntaxHighlighting();
                        }
                    });
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation occurs
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[TextEditorCore.CodexZone] Error scheduling highlighting: {ex.Message}");
            }
        }
    }
}
