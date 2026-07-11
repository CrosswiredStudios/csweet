namespace CSweet.Domain.Core;

public enum WorkerExecutionMode
{
    InProcess = 0,
    LocalWorkerHost = 1,
    HttpRemote = 2,
    McpRemote = 3,
    MarketplaceManaged = 4,
    HumanFulfillment = 5
}
