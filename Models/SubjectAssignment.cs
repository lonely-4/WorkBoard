using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ViewWork.Models;

public sealed class SubjectAssignment : INotifyPropertyChanged
{
    private string _name = "";

    public SubjectAssignment(Guid id, string name, IEnumerable<string>? items = null)
    {
        Id = id;
        _name = name;
        Items = new ObservableCollection<HomeworkItem>((items ?? []).Select(text => new HomeworkItem(text)));
    }

    public Guid Id { get; set; }

    public string Name
    {
        get => _name;
        private set
        {
            if (_name == value)
            {
                return;
            }

            _name = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<HomeworkItem> Items { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ApplyName(string name)
    {
        Name = name;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
