using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ViewWork.Models;
using ViewWork.Services;

namespace ViewWork.Views;

public sealed class WorkBoardSettingsViewModel : INotifyPropertyChanged
{
    private HomeworkRow? _selected;

    public WorkBoardSettingsViewModel(WorkBoardStore store)
    {
        Store = store;
        Store.Changed += (_, _) => Rebuild();
        Rebuild();
    }

    public WorkBoardStore Store { get; }

    public ObservableCollection<HomeworkRow> Rows { get; } = [];

    public bool HasSubjects => Store.Subjects.Count > 0;

    public bool HasSelection => Selected != null;

    public HomeworkRow? Selected
    {
        get => _selected;
        set
        {
            if (ReferenceEquals(_selected, value))
            {
                return;
            }

            _selected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelection));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Add(SubjectAssignment subject, string text)
    {
        var item = new HomeworkItem(text);
        subject.Items.Add(item);
        Selected = Rows.FirstOrDefault(row => ReferenceEquals(row.Item, item));
    }

    public void Update(HomeworkRow row, SubjectAssignment subject, string text)
    {
        row.Item.Text = text;
        if (!ReferenceEquals(row.Subject, subject))
        {
            row.Subject.Items.Remove(row.Item);
            subject.Items.Add(row.Item);
        }

        Selected = Rows.FirstOrDefault(item => ReferenceEquals(item.Item, row.Item));
    }

    public void Remove(HomeworkRow row)
    {
        row.Subject.Items.Remove(row.Item);
    }

    public void ClearHomework()
    {
        Selected = null;
        Store.ClearHomework();
    }

    private void Rebuild()
    {
        var selectedItem = Selected?.Item;
        Rows.Clear();
        foreach (var subject in Store.Subjects)
        {
            foreach (var item in subject.Items)
            {
                if (string.IsNullOrWhiteSpace(item.Text))
                {
                    continue;
                }

                Rows.Add(new HomeworkRow(subject, item));
            }
        }

        Selected = Rows.FirstOrDefault(row => ReferenceEquals(row.Item, selectedItem));
        OnPropertyChanged(nameof(HasSubjects));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
