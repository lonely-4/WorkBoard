using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ViewWork.Services;
using ViewWork.Views;

namespace ViewWork;

[PluginEntrance]
public class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        var store = new WorkBoardStore(PluginConfigFolder);
        store.Load();
        services.AddSingleton(store);
        services.AddComponent<WorkBoardComponent>();
        services.AddSettingsPage<WorkBoardSettingsPage>();
        AppBase.Current.AppStarted += (_, _) => store.Attach(IAppHost.GetService<IProfileService>());
    }
}
