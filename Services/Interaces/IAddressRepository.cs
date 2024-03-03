using Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Interaces
{
    public interface IAddressRepository
    {

        List<Address> GetAddresses();

        Address GetAddressById(int id);

        Task Add(Address address);

        void Delete(int id);

        void Update(Address address);

        Task commitAsync();
    }
}
