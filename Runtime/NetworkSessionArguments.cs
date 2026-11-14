

namespace Karapak.Konnect
{
    public interface IClientJoinArgs : IClientArgs
    {
        public string JoinCode { get; }
        public string PasswordAttempt { get; }
    }
    public interface IServerHostArgs
    {
        public int MaxPlayers { get; }
        public int AccessLevel { get; }
        public string ServerName { get; }
        public string Password { get; }
    }
    public interface IClientArgs
    {
        // maybe player index or character selection here? not actual join information though as hosting also impliments it
    }
    public interface IClientHostArgs : IServerHostArgs, IClientArgs
    {

    }
}
