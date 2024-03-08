using Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Interaces
{
    public interface IFooterRepository
    {
        List<Footer> GetFooter();
        List<Footer> GetFooterWithFooterSocial();

        Footer GetFooterById(int id);

        Footer GetFooterByIdWithSocialMedia(int id);

        Task Add(Footer footer);

        void Delete(int id);

        void Update(Footer footer);

        Task commitAsync();

    }
}
