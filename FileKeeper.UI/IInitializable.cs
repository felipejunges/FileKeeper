using System.Threading.Tasks;

namespace FileKeeper.UI;

public interface IInitializable
{
    Task InitializeAsync();
}