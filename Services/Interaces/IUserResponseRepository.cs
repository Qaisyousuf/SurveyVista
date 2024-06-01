using Model;




namespace Services.Interaces
{
    public interface IUserResponseRepository
    {
        Task<IEnumerable<Response>> GetResponsesByUserAsync(string userName);
       
    }
}
