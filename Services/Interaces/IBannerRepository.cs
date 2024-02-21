using Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Interaces
{
    public interface IBannerRepository
    {
        Task<IEnumerable<Banner>> GetBanners();
        List<Banner> GetAllBanners();

        Banner GetBannerById(int id);

        Task Add(Banner banner);

        void Delete(int id);

        void Update(Banner banner);

        Task commitAsync();
    }
}
