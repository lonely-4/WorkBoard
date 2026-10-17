using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ViewWork.Models;

public sealed class HomeworkItem : INotifyPropertyChanged
{
    private string _text = "";

    public HomeworkItem()
    {
    }

    public HomeworkItem(string text)
    {
        _text = text;
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
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
