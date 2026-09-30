using System.Threading.Tasks;

namespace Eqfal.API.Services
{
    public interface IKeywordSeederService
    {
        Task SeedDefaultKeywordsAsync(int userId);
    }
}
