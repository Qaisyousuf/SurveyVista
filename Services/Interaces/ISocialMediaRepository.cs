using Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



namespace Services.Interaces
{
    public interface ISocialMediaRepository
    {
        List<SocialMedia> GetSocialMedia();

        List<SocialMedia> GetListofSocialMediaById(List<int> ids);

        SocialMedia GetSocialById(int id);

        Task Add(SocialMedia socialMedia);

        void Delete(int id);

        void Update(SocialMedia socialMedia);

        Task commitAsync();
    }
}
