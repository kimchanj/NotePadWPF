using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace NotePadWPF
{
    public partial class MainWindow : Window
    {
        private readonly UTF8Encoding _utf8NoBom = new UTF8Encoding(false);
        private int _untitledIndex = 1;

        public MainWindow()
        {
            InitializeComponent();
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            AddNewTab();
            UpdateStatusPosition();
        }

        private sealed class DocumentModel : INotifyPropertyChanged
        {
            private string? _filePath;
            private string _displayName = string.Empty;
            private bool _isDirty;
            private string _text = string.Empty;

            public Guid Id { get; } = Guid.NewGuid();

            public string? FilePath
            {
                get => _filePath;
                set
                {
                    if (_filePath == value)
                    {
                        return;
                    }

                    _filePath = value;
                    OnPropertyChanged(nameof(FilePath));
                }
            }

            public string DisplayName
            {
                get => _displayName;
                set
                {
                    if (_displayName == value)
                    {
                        return;
                    }

                    _displayName = value;
                    OnPropertyChanged(nameof(DisplayName));
                    OnPropertyChanged(nameof(DisplayHeader));
                }
            }

            public bool IsDirty
            {
                get => _isDirty;
                set
                {
                    if (_isDirty == value)
                    {
                        return;
                    }

                    _isDirty = value;
                    OnPropertyChanged(nameof(IsDirty));
                    OnPropertyChanged(nameof(DisplayHeader));
                }
            }

            public string Text
            {
                get => _text;
                set
                {
                    if (_text == value)
                    {
                        return;
                    }

                    _text = value;
                    OnPropertyChanged(nameof(Text));
                }
            }

            public string DisplayHeader => IsDirty ? $"{DisplayName} ●" : DisplayName;

            public event PropertyChangedEventHandler? PropertyChanged;

            private void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private void AddNewTab()
        {
            var document = new DocumentModel
            {
                DisplayName = $"제목 없음 {_untitledIndex++}",
                FilePath = null,
                IsDirty = false,
                Text = string.Empty
            };

            AddDocumentTab(document, selectTab: true);
        }

        private void AddDocumentTab(DocumentModel document, bool selectTab)
        {
            var editor = CreateEditor(document);
            SetEditorText(editor, document.Text);
            editor.TextWrapping = WordWrapMenuItem.IsChecked ? TextWrapping.Wrap : TextWrapping.NoWrap;

            var tab = new TabItem
            {
                Header = document,
                DataContext = document,
                Content = editor
            };

            EditorTabControl.Items.Add(tab);
            if (selectTab)
            {
                EditorTabControl.SelectedItem = tab;
                editor.Focus();
            }

            UpdateStatusPosition();
        }

        private TextBox CreateEditor(DocumentModel document)
        {
            var editor = new TextBox
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 14,
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            editor.TextChanged += (_, _) =>
            {
                if (editor.Tag is true)
                {
                    UpdateStatusPosition(editor);
                    return;
                }

                document.Text = editor.Text;
                if (!document.IsDirty)
                {
                    document.IsDirty = true;
                }

                UpdateStatusPosition(editor);
            };

            editor.SelectionChanged += (_, _) => UpdateStatusPosition(editor);

            return editor;
        }

        private static void SetEditorText(TextBox editor, string text)
        {
            editor.Tag = true;
            editor.Text = text;
            editor.Tag = null;
        }

        private void SetWordWrapForAllEditors(bool enabled)
        {
            var wrapMode = enabled ? TextWrapping.Wrap : TextWrapping.NoWrap;

            foreach (var tab in EditorTabControl.Items.OfType<TabItem>())
            {
                if (tab.Content is TextBox editor)
                {
                    editor.TextWrapping = wrapMode;
                }
            }
        }

        private TabItem? GetSelectedTab()
        {
            return EditorTabControl.SelectedItem as TabItem;
        }

        private static DocumentModel? GetDocumentFromTab(TabItem? tab)
        {
            return tab?.DataContext as DocumentModel;
        }

        private static TextBox? GetEditorFromTab(TabItem? tab)
        {
            return tab?.Content as TextBox;
        }

        private static string NormalizeToCrLf(string text)
        {
            return text.Replace("\r\n", "\n", StringComparison.Ordinal)
                       .Replace("\r", "\n", StringComparison.Ordinal)
                       .Replace("\n", "\r\n", StringComparison.Ordinal);
        }

        private bool SaveDocument(TabItem tab, bool forceSaveAs)
        {
            var document = GetDocumentFromTab(tab);
            var editor = GetEditorFromTab(tab);
            if (document is null || editor is null)
            {
                return false;
            }

            var targetPath = document.FilePath;
            if (forceSaveAs || string.IsNullOrWhiteSpace(targetPath))
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                    FileName = document.DisplayName,
                    AddExtension = true,
                    DefaultExt = ".txt"
                };

                if (dialog.ShowDialog(this) != true)
                {
                    return false;
                }

                targetPath = dialog.FileName;
            }

            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return false;
            }

            try
            {
                var content = NormalizeToCrLf(editor.Text);
                File.WriteAllText(targetPath, content, _utf8NoBom);

                document.FilePath = targetPath;
                document.DisplayName = Path.GetFileName(targetPath);
                document.Text = editor.Text;
                document.IsDirty = false;
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"저장 중 오류가 발생했습니다.\n{ex.Message}", "WinMemo", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private static string ReadTextFileWithFallback(string path)
        {
            try
            {
                return File.ReadAllText(path, new UTF8Encoding(false, true));
            }
            catch (DecoderFallbackException)
            {
                return File.ReadAllText(path, Encoding.Default);
            }
        }

        private TabItem? FindTabByFilePath(string filePath)
        {
            var normalized = Path.GetFullPath(filePath);
            foreach (var tab in EditorTabControl.Items.OfType<TabItem>())
            {
                var document = GetDocumentFromTab(tab);
                if (document?.FilePath is null)
                {
                    continue;
                }

                var openedPath = Path.GetFullPath(document.FilePath);
                if (string.Equals(openedPath, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return tab;
                }
            }

            return null;
        }

        private bool ConfirmSaveIfDirty(TabItem tab)
        {
            var document = GetDocumentFromTab(tab);
            if (document is null || !document.IsDirty)
            {
                return true;
            }

            var result = MessageBox.Show(
                $"'{document.DisplayName}' 문서의 변경 내용을 저장하시겠습니까?",
                "WinMemo",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question,
                MessageBoxResult.Yes);

            if (result == MessageBoxResult.Yes)
            {
                return SaveDocument(tab, forceSaveAs: false);
            }

            return result == MessageBoxResult.No;
        }

        private bool CloseTabWithPrompt(TabItem tab)
        {
            if (!ConfirmSaveIfDirty(tab))
            {
                return false;
            }

            EditorTabControl.Items.Remove(tab);
            return true;
        }

        private bool ConfirmAllDirtyDocumentsBeforeExit()
        {
            var tabs = EditorTabControl.Items.OfType<TabItem>().ToList();
            foreach (var tab in tabs)
            {
                if (!ConfirmSaveIfDirty(tab))
                {
                    return false;
                }
            }

            return true;
        }

        private TextBox? GetActiveEditor()
        {
            return GetEditorFromTab(GetSelectedTab());
        }

        private static StringComparison GetStringComparison(bool matchCase)
        {
            return matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        }

        private void ShowFindReplacePanel(bool showReplace)
        {
            FindReplacePanel.Visibility = Visibility.Visible;
            ReplaceRowPanel.Visibility = showReplace ? Visibility.Visible : Visibility.Collapsed;
            FindTextBox.Focus();
            FindTextBox.SelectAll();
        }

        private void CloseFindReplacePanel()
        {
            FindReplacePanel.Visibility = Visibility.Collapsed;
            GetActiveEditor()?.Focus();
        }

        private bool FindNextInActiveEditor(bool showNotFoundMessage)
        {
            var editor = GetActiveEditor();
            if (editor is null)
            {
                return false;
            }

            var query = FindTextBox.Text;
            if (string.IsNullOrEmpty(query))
            {
                return false;
            }

            var comparison = GetStringComparison(MatchCaseCheckBox.IsChecked == true);
            var startIndex = editor.SelectionStart + editor.SelectionLength;
            if (startIndex < 0 || startIndex > editor.Text.Length)
            {
                startIndex = editor.CaretIndex;
            }

            var foundIndex = editor.Text.IndexOf(query, startIndex, comparison);
            if (foundIndex < 0 && startIndex > 0)
            {
                foundIndex = editor.Text.IndexOf(query, 0, comparison);
            }

            if (foundIndex < 0)
            {
                if (showNotFoundMessage)
                {
                    MessageBox.Show("더 이상 찾을 수 없습니다.", "WinMemo", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                return false;
            }

            editor.Focus();
            editor.Select(foundIndex, query.Length);
            editor.ScrollToLine(editor.GetLineIndexFromCharacterIndex(foundIndex));
            UpdateStatusPosition(editor);
            return true;
        }

        private void ReplaceOneInActiveEditor()
        {
            var editor = GetActiveEditor();
            if (editor is null)
            {
                return;
            }

            var findText = FindTextBox.Text;
            if (string.IsNullOrEmpty(findText))
            {
                return;
            }

            var replaceText = ReplaceTextBox.Text ?? string.Empty;
            var comparison = GetStringComparison(MatchCaseCheckBox.IsChecked == true);
            var selectedText = editor.SelectedText;

            if (string.Equals(selectedText, findText, comparison))
            {
                var selectionStart = editor.SelectionStart;
                editor.SelectedText = replaceText;
                editor.Select(selectionStart, replaceText.Length);
                FindNextInActiveEditor(showNotFoundMessage: true);
            }
            else if (!FindNextInActiveEditor(showNotFoundMessage: true))
            {
                return;
            }
        }

        private int ReplaceAllText(string source, string findText, string replaceText, StringComparison comparison, out string result)
        {
            if (string.IsNullOrEmpty(findText))
            {
                result = source;
                return 0;
            }

            var count = 0;
            var currentIndex = 0;
            var builder = new StringBuilder();

            while (true)
            {
                var foundIndex = source.IndexOf(findText, currentIndex, comparison);
                if (foundIndex < 0)
                {
                    builder.Append(source, currentIndex, source.Length - currentIndex);
                    break;
                }

                builder.Append(source, currentIndex, foundIndex - currentIndex);
                builder.Append(replaceText);
                count++;
                currentIndex = foundIndex + findText.Length;
            }

            result = builder.ToString();
            return count;
        }

        private void ReplaceAllInActiveEditor()
        {
            var editor = GetActiveEditor();
            if (editor is null)
            {
                return;
            }

            var findText = FindTextBox.Text;
            if (string.IsNullOrEmpty(findText))
            {
                return;
            }

            var replaceText = ReplaceTextBox.Text ?? string.Empty;
            var comparison = GetStringComparison(MatchCaseCheckBox.IsChecked == true);
            var replaceCount = ReplaceAllText(editor.Text, findText, replaceText, comparison, out var replaced);

            if (replaceCount > 0)
            {
                editor.Text = replaced;
            }

            MessageBox.Show($"{replaceCount}개 바꿈", "WinMemo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UpdateStatusPosition()
        {
            UpdateStatusPosition(GetActiveEditor());
        }

        private void UpdateStatusPosition(TextBox? editor)
        {
            var caretIndex = editor?.CaretIndex ?? 0;
            var line = 1;
            var column = 1;

            if (editor is not null)
            {
                line = editor.GetLineIndexFromCharacterIndex(caretIndex) + 1;
                var lineStart = editor.GetCharacterIndexFromLineIndex(line - 1);
                column = caretIndex - lineStart + 1;
            }

            StatusPositionTextBlock.Text = $"Ln {line}, Col {column} | UTF-8 | CRLF | 100%";
        }

        private static void ShowTodo(string action)
        {
            MessageBox.Show($"TODO: {action}", "WinMemo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F3)
            {
                FindNextInActiveEditor(showNotFoundMessage: true);
                e.Handled = true;
                return;
            }

            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
            {
                return;
            }

            if (e.Key == Key.N)
            {
                AddNewTab();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.O)
            {
                OpenMenuItem_Click(sender, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                SaveAsMenuItem_Click(sender, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.S)
            {
                SaveMenuItem_Click(sender, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F)
            {
                ShowFindReplacePanel(showReplace: false);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.H)
            {
                ShowFindReplacePanel(showReplace: true);
                e.Handled = true;
            }
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (!ConfirmAllDirtyDocumentsBeforeExit())
            {
                e.Cancel = true;
            }
        }

        private void NewTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            AddNewTab();
        }

        private void TabCloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: TabItem tab })
            {
                CloseTabWithPrompt(tab);
            }
        }

        private void WordWrapMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SetWordWrapForAllEditors(WordWrapMenuItem.IsChecked);
        }

        private void StatusBarMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MainStatusBar.Visibility = StatusBarMenuItem.IsChecked ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OpenMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            var existingTab = FindTabByFilePath(dialog.FileName);
            if (existingTab is not null)
            {
                EditorTabControl.SelectedItem = existingTab;
                GetEditorFromTab(existingTab)?.Focus();
                return;
            }

            try
            {
                var text = ReadTextFileWithFallback(dialog.FileName);
                var document = new DocumentModel
                {
                    FilePath = dialog.FileName,
                    DisplayName = Path.GetFileName(dialog.FileName),
                    IsDirty = false,
                    Text = text
                };

                AddDocumentTab(document, selectTab: true);
                document.IsDirty = false;
                UpdateStatusPosition();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"파일을 여는 중 오류가 발생했습니다.\n{ex.Message}", "WinMemo", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var tab = GetSelectedTab();
            if (tab is not null)
            {
                SaveDocument(tab, forceSaveAs: false);
            }
        }

        private void SaveAsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var tab = GetSelectedTab();
            if (tab is not null)
            {
                SaveDocument(tab, forceSaveAs: true);
            }
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void UndoMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var editor = GetActiveEditor();
            if (editor?.CanUndo == true)
            {
                editor.Undo();
            }
        }

        private void RedoMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var editor = GetActiveEditor();
            if (editor?.CanRedo == true)
            {
                editor.Redo();
            }
        }

        private void CutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            GetActiveEditor()?.Cut();
        }

        private void CopyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            GetActiveEditor()?.Copy();
        }

        private void PasteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            GetActiveEditor()?.Paste();
        }

        private void SelectAllMenuItem_Click(object sender, RoutedEventArgs e)
        {
            GetActiveEditor()?.SelectAll();
        }

        private void FindMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ShowFindReplacePanel(showReplace: false);
        }

        private void ReplaceMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ShowFindReplacePanel(showReplace: true);
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e) => ShowTodo("정보");

        private void FindNextButton_Click(object sender, RoutedEventArgs e)
        {
            FindNextInActiveEditor(showNotFoundMessage: true);
        }

        private void ReplaceButton_Click(object sender, RoutedEventArgs e)
        {
            ReplaceOneInActiveEditor();
        }

        private void ReplaceAllButton_Click(object sender, RoutedEventArgs e)
        {
            ReplaceAllInActiveEditor();
        }

        private void CloseFindReplacePanelButton_Click(object sender, RoutedEventArgs e)
        {
            CloseFindReplacePanel();
        }

        private void FindTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            FindNextInActiveEditor(showNotFoundMessage: true);
            e.Handled = true;
        }

        private void EditorTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(sender, EditorTabControl))
            {
                return;
            }

            UpdateStatusPosition();
        }
    }
}
