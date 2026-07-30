namespace Tourist_Project_MVC.View_Model
{
    public class DocViewModel
    {
        public Tourist_Project_MVC.Services.DocArticle Article { get; set; } = null!;
        public Tourist_Project_MVC.Services.DocArticle? PrevArticle { get; set; }
        public Tourist_Project_MVC.Services.DocArticle? NextArticle { get; set; }
        public List<Tourist_Project_MVC.Services.DocSection> Sections { get; set; } = new();
    }
}
