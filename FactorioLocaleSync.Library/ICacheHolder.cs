#nullable enable
using System.Threading.Tasks;

namespace FactorioLocaleSync.Library;

public interface ICacheHolder {
    Task ClearCacheAsync();
}