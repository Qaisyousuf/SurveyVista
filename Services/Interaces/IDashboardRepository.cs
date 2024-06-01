using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Interaces
{
    public interface IDashboardRepository
    {
        Task<Dictionary<string, int>> GetModelCountsAsync();
        Task<Dictionary<string, int>> GetCurrentBannerSelectionsAsync();
        Task<Dictionary<string, int>> GetCurrentFooterSelectionsAsync();
    }
}
