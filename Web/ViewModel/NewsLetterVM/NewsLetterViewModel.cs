namespace Web.ViewModel.NewsLetterVM
{
    public class NewsLetterViewModel
    {
        public int Id { get; set; }
       
        public string? Name { get; set; }
        public string? Email { get; set; }
        public bool IsSubscribed { get; set; }
    }
}
