using ViewWork.Models;

namespace ViewWork.Views;

public sealed class HomeworkRow
{
    public HomeworkRow(SubjectAssignment subject, HomeworkItem item)
    {
        Subject = subject;
        Item = item;
    }

    public SubjectAssignment Subject { get; }

    public HomeworkItem Item { get; }

    public string SubjectName => Subject.Name;

    public string Text => Item.Text;
}
