
using Microsoft.EntityFrameworkCore;
using MyWebApi.Data;

namespace MyWebApi.Repositories
{
    public class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly BookStoreContext _context;
        private readonly ILogger<RefreshTokenRepository> _logger;

        public RefreshTokenRepository(BookStoreContext context, ILogger<RefreshTokenRepository> logger)
        {
            _context = context;
            _logger = logger;
        }
        public async Task AddAsync(RefreshToken token)
        {
            try
            {
                _logger.LogInformation("Thực hiện thêm refreshToken vào csdl");
                await _context.AddAsync(token);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Thực hiện thêm refreshToken vào csdl thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, "Xảy ra lỗi khi thêm refreshToken vào csdl");
                throw;
            }
        }

        public async Task<RefreshToken> GetTokenAsync(string refreshToken)
        {
            try
            {
                _logger.LogInformation("Truy vấn lấy refreshToken");
                var storedToken = await _context.RefreshTokens.FirstOrDefaultAsync(x => x.Token == refreshToken);
                if (storedToken == null)
                {
                    _logger.LogWarning("Không tìm thấy refreshToken");
                    return null;
                }
                else
                {
                    _logger.LogInformation("Lấy refreshToken thành công");
                    return storedToken;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Xảy ra lỗi khi lấy token csdl");
                throw;
            }
        }

        public async Task UpdateAsync(RefreshToken token, string refreshToken)
        {
            try
            {
                _logger.LogInformation("Truy vấn lấy refreshToken");
                var storedToken = await _context.RefreshTokens.FirstOrDefaultAsync(x => x.Token == refreshToken);

                _logger.LogInformation("Thực hiện cập nhật token");
                _context.Update(token);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Thực hiện cập nhật token thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, "Xảy ra lỗi khi cập nhật token ");
                throw;
            }
        }
    }
}
