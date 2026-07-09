using System.ComponentModel;

namespace Tourist_Project_MVC.View_Model
{
    public class SponsorApprovalListItem
    {
        public int Id { get; set; }
        public string ApplicantName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Nationality { get; set; } = string.Empty;
        [DisplayName("Requested Date")]
        public DateTime RequestedDate { get; set; }
        public string Status { get; set; } = "Pending";
    }
}
