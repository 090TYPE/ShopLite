using UserService.Models;

namespace UserService.Services;

public interface IJwtTokenGenerator
{
    string Generate(User user);
}
