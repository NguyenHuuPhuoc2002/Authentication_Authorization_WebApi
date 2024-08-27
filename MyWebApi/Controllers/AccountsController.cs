using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages;
using MyWebApi.Data;
using MyWebApi.Models;
using MyWebApi.Repositories;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace MyWebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountsController : ControllerBase
    {
        private readonly IAccountRepository _repo;
        private readonly IConfiguration _configuration;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IRefreshTokenRepository _refreshToken;
        private readonly AppSetting _appSettings;
        private SecurityToken validatedToken;
        public AccountsController(IAccountRepository repo, UserManager<ApplicationUser> userManager,
                                IConfiguration configuration, IRefreshTokenRepository refreshToken,
                                   IOptionsMonitor<AppSetting> optionsMonitor)
        {
            _repo = repo;
            _configuration = configuration;
            _userManager = userManager;
            _refreshToken = refreshToken;
            _appSettings = optionsMonitor.CurrentValue;
        }

        [HttpPost("SignUp")]
        public async Task<IActionResult> SignUp(SignUpModel model)
        {
            var result = await _repo.SignUpAsync(model);
            if (result.Succeeded)
            {
                return Ok(result.Succeeded);
            }
            return StatusCode(500);
        }

        [HttpPost("SignIn")]
        public async Task<IActionResult> SignIn(SigninModel model)
        {
            var user = await _repo.SignInAsync(model);
            if (user == null)
            {
                return Unauthorized();
            }
            var token = await GenerateToken(user);
            return Ok(token);

        }
        private string GenerateRefreshToken()
        {
            var random = new byte[32];
            //sinh số ngẫu nhiên  
            using (var rng = RandomNumberGenerator.Create())
            {
                //lưu vào mảng ramdom
                rng.GetBytes(random);
                //chuyển mảng byte thành chuỗi Base64
                return Convert.ToBase64String(random);
            }
        }
        private async Task<TokenModel> GenerateToken(IdentityUser user)
        {
            //thanh cong thi tao ra cac quyen
            var authClaim = new List<Claim>
            {
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),

            };

            var appUser = new ApplicationUser
            {
                Email = user.Email,
                PasswordHash = user.PasswordHash
            };
            var _user = await _userManager.FindByEmailAsync(user.Email);
            //lay usseRole
            var useRole = await _userManager.GetRolesAsync(_user);
            foreach (var role in useRole)
            {
                authClaim.Add(new Claim(ClaimTypes.Role, role.ToString()));
            }
            
            var authenKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_appSettings.Secret));

            //tao moi token
            var token = new JwtSecurityToken(
                issuer: _appSettings.ValidIssuer,
                audience: _appSettings.ValidAudience,
                expires: DateTime.Now.AddMinutes(20),
                claims: authClaim,
                signingCredentials: new SigningCredentials(authenKey, SecurityAlgorithms.HmacSha512Signature)

                );
            var jwtTokenHandler = new JwtSecurityTokenHandler();
            var accessToken = jwtTokenHandler.WriteToken(token);
            var refreshToken = GenerateRefreshToken();
            var refreshTokenEntity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                JwtId = token.Id,
                UserId = user.Id,
                Token = refreshToken,
                IsUsed = false,
                IsRevoked = false,
                IssuedAt = DateTime.UtcNow,
                ExpiredAt = DateTime.UtcNow.AddHours(1),
            };

            await _refreshToken.AddAsync(refreshTokenEntity);
            return new TokenModel
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
            };
        }
        [HttpPost("renewToken")]
        public async Task<IActionResult> RenewToken(TokenModel tokenModel)
        {
            //check xem token gửi lên nó còn hợp lệ không trước khi cấp phát một access token mới.
            var jwtTokenHandler = new JwtSecurityTokenHandler(); //sử dụng để tạo và viết token JWT.
            var secretKeyBytes = Encoding.UTF8.GetBytes(_appSettings.Secret); //Chuyển đổi khóa bí mật thành mảng byte.

            //Cấu hình
            var tokenValidateParam = new TokenValidationParameters
            {
                //tự cấp token
                ValidateIssuer = false,
                ValidateAudience = false,
                //ký vào token
                IssuerSigningKey = new SymmetricSecurityKey(secretKeyBytes),
                ValidateIssuerSigningKey = true,

                ClockSkew = TimeSpan.Zero,

                ValidateLifetime = false// ko kiem tra token het hang  
            };
            try
            {
                //check 1: AccessToken valid format
                var tokenInverification = jwtTokenHandler.ValidateToken(tokenModel.AccessToken, tokenValidateParam, out validatedToken);
                //check 2: check alg
                if (validatedToken is JwtSecurityToken jwtSecurityToken)
                {
                    var result = jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha512Signature, StringComparison.InvariantCultureIgnoreCase);
                    if (!result)
                    {
                        return Ok(new ApiResponse
                        {
                            Success = false,
                            Message = "Invalid token"
                        });
                    }
                }

                //check 3: Check accessToken expire?
                var utcExpireDate = long.Parse(tokenInverification.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Exp).Value);

                var expireDate = ConvertUnixTimeToDaateTime(utcExpireDate);

                if (expireDate > DateTime.UtcNow)
                {
                    return Ok(new ApiResponse
                    {
                        Success = false,
                        Message = "Access token has not yet expired"
                    });
                }

                //check 4: check refreshtoken exist in DB
                var storedToken = await _refreshToken.GetTokenAsync(tokenModel.RefreshToken);
                if (storedToken is null)
                {
                    return Ok(new ApiResponse
                    {
                        Success = false,
                        Message = "Refresh token doesn't exist"
                    });
                }

                //check 5: check refresh is used/ revoked ?
                if (storedToken.IsUsed)
                {
                    return Ok(new ApiResponse
                    {
                        Success = false,
                        Message = "Refresh token has been exist"
                    });
                }
                if (storedToken.IsRevoked)
                {
                    return Ok(new ApiResponse
                    {
                        Success = false,
                        Message = "Refresh token has been Revoked"
                    });
                }

                //check 6: AccessToken ID = JwID in RefreshToken // dịch ngược lại để lấy JwtId từ chuỗi token
                var jti = tokenInverification.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Jti).Value;
                if (storedToken.JwtId != jti)
                {
                    return Ok(new ApiResponse
                    {
                        Success = false,
                        Message = "Token doesn't match"
                    });
                }
                //check 7: update token is used
                storedToken.IsRevoked = true;
                storedToken.IsUsed = true;
                await _refreshToken.UpdateAsync(storedToken, tokenModel.RefreshToken);

                //create new token
                var user = await _userManager.FindByIdAsync(storedToken.UserId);
                var token = await GenerateToken(user);
                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "Renew Token Success",
                    Data = token
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Message = "Something went wrong"
                });
            }

        }
        private DateTime ConvertUnixTimeToDaateTime(long utcExpireDate)
        {
            var dateTimeInterval = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dateTimeInterval.AddSeconds(utcExpireDate).ToUniversalTime();
            return dateTimeInterval;
        }
    }
}
