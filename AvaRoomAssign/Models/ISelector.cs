using System.Threading.Tasks;

namespace AvaRoomAssign.Models;

public interface ISelector
{
    public Task RunAsync();

    public void Stop();
} 