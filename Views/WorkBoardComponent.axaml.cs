using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Shared;
using ViewWork.Services;

namespace ViewWork.Views;

[ComponentInfo("B7E4A1C2-9F30-4D58-A6C1-3E8B5D2F7049", "作业看板", "\uE8F1", "按学科轮播今日作业。")]
public partial class WorkBoardComponent : ComponentBase
{
    private const string EmptySubject = "无学科";
    private const string EmptyHomework = "今日暂无作业";

    private readonly WorkBoardStore _store;
    private readonly DispatcherTimer _reloadTimer;
    private readonly List<Slide> _slides = [];
    private CancellationTokenSource _playback = new();
    private int _index;

    public WorkBoardComponent()
    {
        _store = IAppHost.GetService<WorkBoardStore>();
        _reloadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _reloadTimer.Tick += (_, _) =>
        {
            _reloadTimer.Stop();
            RestartPlayback();
        };
        InitializeComponent();
        EnsureTransform(SubjectCurrent);
        EnsureTransform(SubjectIncoming);
        EnsureTransform(HomeworkCurrent);
        EnsureTransform(HomeworkIncoming);
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _store.Changed += OnStoreChanged;
        RestartPlayback();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _store.Changed -= OnStoreChanged;
        _reloadTimer.Stop();
        CancelPlayback();
    }

    private void OnStoreChanged(object? sender, EventArgs e)
    {
        _reloadTimer.Stop();
        _reloadTimer.Start();
    }

    private void RestartPlayback()
    {
        CancelPlayback();
        RebuildSlides();
        var token = _playback.Token;
        if (_slides.Count == 0)
        {
            ShowInstant(EmptySubject, EmptyHomework);
            return;
        }

        _index = 0;
        ShowInstant(_slides[0].Subject, _slides[0].Homework);
        if (_slides.Count == 1)
        {
            return;
        }

        _ = PlayAsync(token);
    }

    private async Task PlayAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var seconds = (double)Math.Clamp(_store.IntervalSeconds, 1, 60);
                await Task.Delay(TimeSpan.FromSeconds(seconds), token);
                if (token.IsCancellationRequested || _slides.Count <= 1)
                {
                    return;
                }

                var nextIndex = (_index + 1) % _slides.Count;
                var current = _slides[_index];
                var next = _slides[nextIndex];
                var rollSubject = next.Subject != current.Subject;
                _index = nextIndex;
                await ShowAsync(next, rollSubject, token);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void RebuildSlides()
    {
        _slides.Clear();
        foreach (var subject in _store.Subjects)
        {
            var name = subject.Name.Trim();
            if (name.Length == 0)
            {
                continue;
            }

            foreach (var item in subject.Items)
            {
                var homework = item.Text.Trim();
                if (homework.Length == 0)
                {
                    continue;
                }

                _slides.Add(new Slide(name, homework));
            }
        }
    }

    private void ShowInstant(string subject, string homework)
    {
        SubjectIncoming.IsVisible = false;
        HomeworkIncoming.IsVisible = false;
        SubjectCurrent.Text = subject;
        HomeworkCurrent.Text = homework;
        ResetTransform(SubjectCurrent);
        ResetTransform(SubjectIncoming);
        ResetTransform(HomeworkCurrent);
        ResetTransform(HomeworkIncoming);
    }

    private async Task ShowAsync(Slide slide, bool rollSubject, CancellationToken token)
    {
        var rolls = new List<Task>();
        if (rollSubject)
        {
            rolls.Add(RollAsync(SubjectWindow, SubjectCurrent, SubjectIncoming, slide.Subject, token));
        }

        rolls.Add(RollAsync(HomeworkWindow, HomeworkCurrent, HomeworkIncoming, slide.Homework, token));
        await Task.WhenAll(rolls);
    }

    private async Task RollAsync(Border window, TextBlock current, TextBlock incoming, string text, CancellationToken token)
    {
        incoming.Text = text;
        incoming.IsVisible = true;
        current.Measure(Size.Infinity);
        incoming.Measure(Size.Infinity);
        var distance = Math.Max(current.DesiredSize.Height, incoming.DesiredSize.Height);
        if (distance < 1)
        {
            distance = 28;
        }

        window.Height = distance;
        var currentTransform = EnsureTransform(current);
        var incomingTransform = EnsureTransform(incoming);
        currentTransform.Y = 0;
        incomingTransform.Y = distance;

        await Task.WhenAll(
            AnimateYAsync(currentTransform, 0, -distance, token),
            AnimateYAsync(incomingTransform, distance, 0, token));

        if (token.IsCancellationRequested)
        {
            return;
        }

        current.Text = text;
        currentTransform.Y = 0;
        incoming.IsVisible = false;
        incomingTransform.Y = 0;
        window.Height = double.NaN;
    }

    private static async Task AnimateYAsync(TranslateTransform transform, double from, double to, CancellationToken token)
    {
        var duration = TimeSpan.FromMilliseconds(460);
        var started = DateTime.UtcNow;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var progress = (DateTime.UtcNow - started).TotalMilliseconds / duration.TotalMilliseconds;
            if (progress >= 1)
            {
                transform.Y = to;
                return;
            }

            var eased = progress < 0.5
                ? 4 * progress * progress * progress
                : 1 - Math.Pow(-2 * progress + 2, 3) / 2;
            transform.Y = from + (to - from) * eased;
            await Task.Delay(16, token);
        }
    }

    private static TranslateTransform EnsureTransform(TextBlock block)
    {
        if (block.RenderTransform is TranslateTransform transform)
        {
            return transform;
        }

        transform = new TranslateTransform();
        block.RenderTransform = transform;
        return transform;
    }

    private static void ResetTransform(TextBlock block)
    {
        EnsureTransform(block).Y = 0;
    }

    private void CancelPlayback()
    {
        _playback.Cancel();
        _playback.Dispose();
        _playback = new CancellationTokenSource();
    }

    private readonly record struct Slide(string Subject, string Homework);
}
