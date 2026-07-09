using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.View_Model
{
    public class SupportTicketVM
    {
        public List<SupportTicket> Tickets { get; set; } = new();

        [Required(ErrorMessage = "Subject is required.")]
        public string? Subject { get; set; }

        [Required(ErrorMessage = "Description is required.")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Please choose a category.")]
        public string? Category { get; set; }

        // Optional photo/video attachment.
        public IFormFile? Attachment { get; set; }

        public static readonly List<string> Categories = new()
        {
            "Incorrect location",
            "Wrong historical information",
            "Bug in application",
            "App crashed",
            "Reward issue",
            "Another issue"
        };
    }
}
