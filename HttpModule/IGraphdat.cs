namespace Alphashack.Graphdat.Agent
{
    public interface IGraphdat
    {
        void Begin(string name);
        void End(string name = null);
    }
}
