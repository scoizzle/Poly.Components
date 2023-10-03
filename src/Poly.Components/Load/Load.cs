using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Stateless;

namespace Poly.Components;

public partial class Load<T> : ComponentBase, IAsyncDisposable
{
    [Parameter, EditorRequired]
    public Func<CancellationToken, Task<T?>>? From { get; set; }

    [Parameter]
    public RenderFragment<T?>? Loaded { get; set; }

    [Parameter] 
    public T? Data { get; set; }

    [Parameter] 
    public EventCallback<T?> DataChanged { get; set; }

    [Parameter]
    public TimeSpan? Every { get; set; }

    [Parameter] 
    public object? ReloadOnChange { get; set; }

    [Parameter]
    public bool StaleDataIsAnError { get; set; }

    [Parameter] 
    public RenderFragment Loading { get; set; } = builder => builder.AddMarkupContent(0, "Loading...");

    [Parameter] 
    public RenderFragment<Exception?> Error { get; set; } = _ => builder => builder.AddMarkupContent(0, "Error...");

    private PeriodicTimer? _timer;
    private RenderFragment? currentContent;
    private StateMachine<LoadState, LoadTrigger> stateMachine = new(LoadState.Init);
    private CancellationTokenSource cancellationTokenSource = new();

    public Load() => ConfigureStateMachine();

    private async void OnBeginLoading() {
        if (From is null)
            throw new ArgumentNullException(nameof(From));

        if (cancellationTokenSource.IsCancellationRequested)
            cancellationTokenSource = new();

        try {
            var result = await From(cancellationTokenSource.Token);

            stateMachine.Fire(EndLoadingTrigger, result);
        }
        catch (TaskCanceledException) {
            stateMachine.Fire(LoadTrigger.CancelLoading);
        }
        catch (Exception error) {
            stateMachine.Fire(LoadErrorTrigger, error);
        }
    }

    private async void OnEndLoading(T? data) {
        if (ReferenceEquals(Data, data))
            return;

        if (Equals(Data, data))
            return;

        if (Loaded is not null)
            currentContent = Loaded!(data);

        if (DataChanged.HasDelegate)
            await DataChanged.InvokeAsync(data);

        await InvokeAsync(StateHasChanged);
    }

    private async void OnLoadError(Exception error) {
        currentContent = Error?.Invoke(error);

        await InvokeAsync(StateHasChanged);
    }

    private void OnLoadStale(Exception error) {
        if (!StaleDataIsAnError)
            return;

        OnLoadError(error);
    }

    private async void InitializeTimerLoop(TimeSpan? period) {
        _timer?.Dispose();

        if (!period.HasValue || period.Value <= TimeSpan.Zero)
            return;

        _timer = new(period.Value);

        try {
            while (await _timer.WaitForNextTickAsync(cancellationTokenSource.Token)) {
                stateMachine.Fire(LoadTrigger.BeginLoading);
            }
        }
        catch (TaskCanceledException) { }
        catch (OperationCanceledException) { }
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
        => currentContent?.Invoke(builder);

    protected override void OnInitialized() 
        => stateMachine.Fire(LoadTrigger.BeginLoading);

    public override async Task SetParametersAsync(ParameterView parameters) {
        var previousPeriod = Every;
        var previousReloadSentinelValue = ReloadOnChange;

        await base.SetParametersAsync(parameters);

        if (!ReferenceEquals(ReloadOnChange, previousReloadSentinelValue) ||
            !Equals(ReloadOnChange, previousReloadSentinelValue))
        {
            await stateMachine.FireAsync(LoadTrigger.BeginLoading);
        }    

        if (Every != previousPeriod)
        {
            InitializeTimerLoop(Every);
        }
    }

    public async ValueTask DisposeAsync() {
        await stateMachine.FireAsync(LoadTrigger.Dispose);

        cancellationTokenSource?.Cancel();
    }
    
    public void Reload() {
        stateMachine.Fire(LoadTrigger.BeginLoading);
    }
}