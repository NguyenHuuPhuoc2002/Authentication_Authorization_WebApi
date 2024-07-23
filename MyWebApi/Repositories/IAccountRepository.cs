using Microsoft.AspNetCore.Identity;
using MyWebApi.Models;

namespace MyWebApi.Repositories
{
    public interface IAccountRepository
    {
        public Task<IdentityResult> SignUpAsync(SignUpModel model);
        public Task<string> SignInAsync(SigninModel model);
    }
}
