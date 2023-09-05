using Microsoft.AspNetCore.Components;
using Stateless;

namespace Poly.Components;

public partial class Load<T> : ComponentBase, IAsyncDisposable {
    static readonly StateMachine<LoadState, LoadTrigger>.TriggerWithParameters<T?> EndLoadingTrigger = new(LoadTrigger.EndLoading);
    static readonly StateMachine<LoadState, LoadTrigger>.TriggerWithParameters<Exception> LoadErrorTrigger = new(LoadTrigger.LoadError);

    internal enum LoadState {
        Init,
        Loading,
        Refreshing,
        Loaded,
        Stale,
        Error,
        Disposed
    }

    internal enum LoadTrigger {
        BeginLoading,
        CancelLoading,
        LoadError,
        EndLoading,
        Dispose
    }

    private void ConfigureStateMachine() {
        stateMachine.Configure(LoadState.Init)
            .OnExit(() => currentContent = Loading)
            .Permit(LoadTrigger.BeginLoading, LoadState.Loading)
            .Permit(LoadTrigger.Dispose, LoadState.Disposed);

        stateMachine.Configure(LoadState.Loading)
            .OnEntry(OnBeginLoading)
            .PermitDynamic(EndLoadingTrigger, _ => LoadState.Loaded)
            .PermitDynamic(LoadErrorTrigger, _ => LoadState.Error)
            .Permit(LoadTrigger.CancelLoading, LoadState.Error)
            .Permit(LoadTrigger.Dispose, LoadState.Disposed)
            .Ignore(LoadTrigger.BeginLoading);

        stateMachine.Configure(LoadState.Loaded)
            .OnEntryFrom(EndLoadingTrigger, OnEndLoading)
            .Permit(LoadTrigger.BeginLoading, LoadState.Refreshing)
            .Permit(LoadTrigger.Dispose, LoadState.Disposed)
            .Ignore(LoadTrigger.CancelLoading);

        stateMachine.Configure(LoadState.Refreshing)
            .OnEntry(OnBeginLoading)
            .PermitDynamic(EndLoadingTrigger, _ => LoadState.Loaded)
            .PermitDynamic(LoadErrorTrigger, _ => LoadState.Stale)
            .Permit(LoadTrigger.CancelLoading, LoadState.Stale)
            .Permit(LoadTrigger.Dispose, LoadState.Disposed)
            .Ignore(LoadTrigger.BeginLoading);

        stateMachine.Configure(LoadState.Stale)
            .OnEntryFrom(LoadErrorTrigger, OnLoadStale)
            .Permit(LoadTrigger.BeginLoading, LoadState.Refreshing)
            .Permit(LoadTrigger.EndLoading, LoadState.Loaded)
            .Permit(LoadTrigger.Dispose, LoadState.Disposed)
            .Ignore(LoadTrigger.CancelLoading);

        stateMachine.Configure(LoadState.Error)
            .OnEntryFrom(LoadErrorTrigger, OnLoadError)
            .Permit(LoadTrigger.BeginLoading, LoadState.Loading)
            .Permit(LoadTrigger.Dispose, LoadState.Disposed);

        stateMachine.Configure(LoadState.Disposed)
            .PermitReentry(LoadTrigger.Dispose)
            .Ignore(LoadTrigger.CancelLoading);
    }
}