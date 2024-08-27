using MyWebApi.Data;

namespace MyWebApi.Repositories
{
    public interface IRefreshTokenRepository
    {
        Task AddAsync(RefreshToken token);
        Task<RefreshToken> GetTokenAsync(string refreshToken);
        Task UpdateAsync(RefreshToken token, string refreshToken);
    }
}
