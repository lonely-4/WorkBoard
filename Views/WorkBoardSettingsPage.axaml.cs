using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Shared;
using FluentAvalonia.UI.Controls;
using ViewWork.Models;
using ViewWork.Services;

namespace ViewWork.Views;

[SettingsPageInfo("viewwork.workboard.homework", "今日作业", "\ue8f1", "\ue8f1")]
public partial class WorkBoardSettingsPage : SettingsPageBase
{
    public WorkBoardSettingsPage()
    {
        ViewModel = new WorkBoardSettingsViewModel(IAppHost.GetService<WorkBoardStore>());
        DataContext = ViewModel;
        InitializeComponent();
    }

    public WorkBoardSettingsViewModel ViewModel { get; }

    private async void Add_OnClick(object? sender, RoutedEventArgs e)
    {
        var result = await ShowHomeworkDialog("添加作业", "添加", null, "");
        if (result is { } value)
        {
            ViewModel.Add(value.Subject, value.Text);
        }
    }

    private async void Edit_OnClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.Selected is not { } row)
        {
            return;
        }

        var result = await ShowHomeworkDialog("编辑作业", "保存", row.Subject, row.Text);
        if (result is { } value)
        {
            ViewModel.Update(row, value.Subject, value.Text);
        }
    }

    private void Remove_OnClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.Selected is { } row)
        {
            ViewModel.Remove(row);
        }
    }

    private void Clear_OnClick(object? sender, RoutedEventArgs e) => ViewModel.ClearHomework();

    private async Task<(SubjectAssignment Subject, string Text)?> ShowHomeworkDialog(
        string title,
        string confirmText,
        SubjectAssignment? subject,
        string text)
    {
        var model = new HomeworkDialogModel(ViewModel.Store.Subjects, subject, text);
        var dialog = new ContentDialog
        {
            Title = title,
            PrimaryButtonText = confirmText,
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = model.CanConfirm,
            Content = new HomeworkDialogView { DataContext = model }
        };
        model.PropertyChanged += (_, _) => dialog.IsPrimaryButtonEnabled = model.CanConfirm;
        var result = await dialog.ShowAsync(TopLevel.GetTopLevel(this));
        if (result != ContentDialogResult.Primary || model.Subject == null)
        {
            return null;
        }

        return (model.Subject, model.Text.Trim());
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        LayoutUpdated += OnLayoutUpdated;
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        LayoutUpdated -= OnLayoutUpdated;
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        var host = this.FindAncestorOfType<ScrollViewer>();
        if (host == null || host.Bounds.Height < 1 || Header.Bounds.Height < 1)
        {
            return;
        }

        var origin = Editor.TranslatePoint(new Point(0, 0), host);
        if (origin == null)
        {
            return;
        }

        var available = host.Bounds.Height - origin.Value.Y - Root.Margin.Bottom - 8;
        var next = Math.Max(220, available);
        if (double.IsNaN(Editor.Height) || Math.Abs(Editor.Height - next) > 1)
        {
            Editor.Height = next;
        }
    }
}
