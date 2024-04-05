using Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Interaces
{
    public interface INewsLetterRepository
    {
        List<Subscription> GetAll();

        Subscription GetById(int id);

        
        void Delete(int id);
    }
}
