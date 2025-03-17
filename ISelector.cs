namespace RoomAssign;

public interface ISelector
{
    public Task RunAsync();

    public void Stop();
}