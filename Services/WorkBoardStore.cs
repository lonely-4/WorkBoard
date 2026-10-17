using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Shared.Models.Profile;
using ViewWork.Models;

namespace ViewWork.Services;

public sealed class WorkBoardStore : INotifyPropertyChanged
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly Dictionary<Guid, List<string>> _homeworkById = [];
    private readonly List<LegacyHomework> _legacyHomework = [];
    private decimal _intervalSeconds = 5;
    private bool _suppress;
    private IProfileService? _profiles;
    private Profile? _profile;

    public WorkBoardStore(string configFolder)
    {
        Directory.CreateDirectory(configFolder);
        _filePath = Path.Combine(configFolder, "workboard.json");
        Subjects.CollectionChanged += OnSubjectsChanged;
    }

    public ObservableCollection<SubjectAssignment> Subjects { get; } = [];

    public decimal IntervalSeconds
    {
        get => _intervalSeconds;
        set
        {
            var clamped = value < 1 ? 1 : value > 60 ? 60 : value;
            if (_intervalSeconds == clamped)
            {
                return;
            }

            _intervalSeconds = clamped;
            OnPropertyChanged(nameof(IntervalSeconds));
            Commit();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? Changed;

    public void Load()
    {
        if (!File.Exists(_filePath))
        {
            return;
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize<WorkBoardSnapshot>(File.ReadAllText(_filePath), JsonOptions);
            if (snapshot == null)
            {
                return;
            }

            _homeworkById.Clear();
            _legacyHomework.Clear();
            foreach (var subject in snapshot.Subjects)
            {
                if (subject.Id != Guid.Empty)
                {
                    _homeworkById[subject.Id] = subject.Items;
                }
                else if (!string.IsNullOrWhiteSpace(subject.Name))
                {
                    _legacyHomework.Add(new LegacyHomework(subject.Name.Trim(), subject.Items));
                }
            }

            _intervalSeconds = snapshot.IntervalSeconds < 1 ? 5 : snapshot.IntervalSeconds;
            OnPropertyChanged(nameof(IntervalSeconds));
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _homeworkById.Clear();
            _legacyHomework.Clear();
        }
    }

    public void Attach(IProfileService profiles)
    {
        if (_profiles is INotifyPropertyChanged previous)
        {
            previous.PropertyChanged -= OnProfileServiceChanged;
        }

        _profiles = profiles;
        if (profiles is INotifyPropertyChanged current)
        {
            current.PropertyChanged += OnProfileServiceChanged;
        }

        BindProfile(profiles.Profile);
    }

    public void ClearHomework()
    {
        foreach (var subject in Subjects)
        {
            subject.Items.Clear();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnProfileServiceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_profiles != null && (e.PropertyName is null || e.PropertyName == nameof(IProfileService.Profile)))
        {
            BindProfile(_profiles.Profile);
        }
    }

    private void BindProfile(Profile profile)
    {
        if (_profile != null)
        {
            _profile.PropertyChanged -= OnProfilePropertyChanged;
            _profile.Subjects.CollectionChanged -= OnProfileSubjectsChanged;
            foreach (var subject in _profile.Subjects.Values)
            {
                subject.PropertyChanged -= OnProfileSubjectPropertyChanged;
            }
        }

        _profile = profile;
        profile.PropertyChanged += OnProfilePropertyChanged;
        profile.Subjects.CollectionChanged += OnProfileSubjectsChanged;
        foreach (var subject in profile.Subjects.Values)
        {
            subject.PropertyChanged += OnProfileSubjectPropertyChanged;
        }

        SyncSubjects();
    }

    private void OnProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_profile != null && e.PropertyName == nameof(Profile.Subjects))
        {
            _profile.Subjects.CollectionChanged -= OnProfileSubjectsChanged;
            _profile.Subjects.CollectionChanged += OnProfileSubjectsChanged;
            SyncSubjects();
        }
    }

    private void OnProfileSubjectsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_profile == null)
        {
            return;
        }

        SyncSubjects();
    }

    private void OnProfileSubjectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_profile == null || sender is not Subject subject)
        {
            return;
        }

        if (e.PropertyName is not null && e.PropertyName != nameof(Subject.Name))
        {
            return;
        }

        var id = _profile.Subjects.FirstOrDefault(pair => ReferenceEquals(pair.Value, subject)).Key;
        var row = Subjects.FirstOrDefault(item => item.Id == id);
        row?.ApplyName(subject.Name);
    }

    private void SyncSubjects()
    {
        if (_profile == null)
        {
            return;
        }

        var desired = _profile.Subjects
            .Select(pair => (pair.Key, Name: pair.Value.Name ?? ""))
            .ToList();

        _suppress = true;
        try
        {
            for (var index = 0; index < desired.Count; index++)
            {
                var (id, name) = desired[index];
                var existingIndex = IndexOf(id);
                SubjectAssignment row;
                if (existingIndex >= 0)
                {
                    row = Subjects[existingIndex];
                    if (existingIndex != index)
                    {
                        Subjects.Move(existingIndex, index);
                    }
                }
                else
                {
                    row = new SubjectAssignment(id, name, TakeHomework(id, name));
                    Subjects.Insert(index, row);
                }

                row.ApplyName(name);
            }

            while (Subjects.Count > desired.Count)
            {
                Subjects.RemoveAt(Subjects.Count - 1);
            }

            foreach (var subject in _profile.Subjects.Values)
            {
                subject.PropertyChanged -= OnProfileSubjectPropertyChanged;
                subject.PropertyChanged += OnProfileSubjectPropertyChanged;
            }
        }
        finally
        {
            _suppress = false;
        }

        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private int IndexOf(Guid id)
    {
        for (var index = 0; index < Subjects.Count; index++)
        {
            if (Subjects[index].Id == id)
            {
                return index;
            }
        }

        return -1;
    }

    private IEnumerable<string> TakeHomework(Guid id, string name)
    {
        if (_homeworkById.Remove(id, out var saved))
        {
            return saved;
        }

        var legacy = _legacyHomework.FindIndex(item => item.Name == name.Trim());
        if (legacy < 0)
        {
            return [];
        }

        var items = _legacyHomework[legacy].Items;
        _legacyHomework.RemoveAt(legacy);
        return items;
    }

    private void OnSubjectsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (SubjectAssignment subject in e.OldItems)
            {
                Unhook(subject);
            }
        }

        if (e.NewItems != null)
        {
            foreach (SubjectAssignment subject in e.NewItems)
            {
                Hook(subject);
            }
        }

        Commit();
    }

    private void Hook(SubjectAssignment subject)
    {
        subject.PropertyChanged += OnSubjectPropertyChanged;
        subject.Items.CollectionChanged += OnHomeworkChanged;
        foreach (var item in subject.Items)
        {
            item.PropertyChanged += OnHomeworkPropertyChanged;
        }
    }

    private void Unhook(SubjectAssignment subject)
    {
        subject.PropertyChanged -= OnSubjectPropertyChanged;
        subject.Items.CollectionChanged -= OnHomeworkChanged;
        foreach (var item in subject.Items)
        {
            item.PropertyChanged -= OnHomeworkPropertyChanged;
        }
    }

    private void OnSubjectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Commit();
    }

    private void OnHomeworkChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (HomeworkItem item in e.OldItems)
            {
                item.PropertyChanged -= OnHomeworkPropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (HomeworkItem item in e.NewItems)
            {
                item.PropertyChanged += OnHomeworkPropertyChanged;
            }
        }

        Commit();
    }

    private void OnHomeworkPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Commit();
    }

    private void Commit()
    {
        if (_suppress)
        {
            return;
        }

        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Save()
    {
        var snapshot = new WorkBoardSnapshot
        {
            IntervalSeconds = IntervalSeconds,
            Subjects = Subjects.Select(subject => new SubjectSnapshot
            {
                Id = subject.Id,
                Items = subject.Items.Select(item => item.Text).ToList()
            }).ToList()
        };

        foreach (var pair in _homeworkById)
        {
            if (snapshot.Subjects.All(subject => subject.Id != pair.Key))
            {
                snapshot.Subjects.Add(new SubjectSnapshot { Id = pair.Key, Items = pair.Value });
            }
        }

        File.WriteAllText(_filePath, JsonSerializer.Serialize(snapshot, JsonOptions));
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed class WorkBoardSnapshot
    {
        public decimal IntervalSeconds { get; set; } = 5;

        public List<SubjectSnapshot> Subjects { get; set; } = [];
    }

    private sealed class SubjectSnapshot
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = "";

        public List<string> Items { get; set; } = [];
    }

    private readonly record struct LegacyHomework(string Name, List<string> Items);
}
