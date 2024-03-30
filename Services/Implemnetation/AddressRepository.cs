using Data;
using Microsoft.EntityFrameworkCore;
using Model;
using Services.Interaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Implemnetation
{
    public class AddressRepository : IAddressRepository
    {
        private readonly SurveyContext _context;

        public AddressRepository(SurveyContext context)
        {
            _context = context;
        }

        public async Task Add(Address address)
        {
             await _context.Addresss.AddAsync(address);

           
        }

        public async Task commitAsync()
        {
            await _context.SaveChangesAsync();
        }

        public void Delete(int? id)
        {
            var addresId = GetAddressById(id);

            _context.Addresss.Remove(addresId);
        }

        public Address GetAddressById(int? id)
        {
            return _context.Addresss.AsNoTracking().Where(x => x.Id == id).FirstOrDefault();
        }

        public List<Address> GetAddresses()
        {
           return _context.Addresss.ToList();
        }

        public async Task<List<Address>> GetAddressesAsync()
        {
           return await _context.Addresss.AsNoTracking().ToListAsync();
        }

        public void Update(Address address)
        {
            _context.Addresss.Update(address);
        }
    }
}
