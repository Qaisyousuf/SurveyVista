using Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Interaces
{
    public interface IPageRepository
    {
        Task<List<Page>> GetPages();

        Page GetPageById(int id);

        void Add(Page page);

        void Delete(int id);

        void Update(Page page);

        List<Page> GetPageWithBanner();

        Page GetPageSlug(string slug);

        bool SlugExists(string slug,int? pageIdExclude=null);



        Task commitAsync();



    }
}
