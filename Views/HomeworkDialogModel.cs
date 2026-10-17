using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ViewWork.Models;

namespace ViewWork.Views;

public sealed class HomeworkDialogModel : INotifyPropertyChanged
{
    private SubjectAssignment? _subject;
    private string _text;

    public HomeworkDialogModel(ObservableCollection<SubjectAssignment> subjects, SubjectAssignment? subject, string text)
    {
        Subjects = subjects;
        _subject = subject ?? subjects.FirstOrDefault();
        _text = text;
    }

    public ObservableCollection<SubjectAssignment> Subjects { get; }

    public SubjectAssignment? Subject
    {
        get => _subject;
        set
        {
            if (ReferenceEquals(_subject, value))
            {
                return;
            }

            _subject = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanConfirm));
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
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanConfirm));
        }
    }

    public bool CanConfirm => Subject != null && !string.IsNullOrWhiteSpace(Text);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
