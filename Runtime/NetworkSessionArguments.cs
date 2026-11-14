

namespace Karapak.Konnect
{
    public interface IStartClientArgs
    {
        public string Handler { get; }
        public string JoinCode { get; }
        public string PasswordAttempt { get; }
    }
    public interface IStartServerArgs
    {
        public string Handler { get; }
        public int MaxPlayers { get; }
        public int AccessLevel { get; }
        public string ServerName { get; }
        public string Password { get; }
    }
    public interface IStartHostArgs : IStartClientArgs, IStartServerArgs
    {
        string IStartClientArgs.JoinCode => "localhost";
        string IStartClientArgs.PasswordAttempt => "null";
    }
}
