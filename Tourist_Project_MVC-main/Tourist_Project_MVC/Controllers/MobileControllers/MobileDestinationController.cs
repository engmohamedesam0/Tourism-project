using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Tourist_Project_MVC.DTOs;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;

namespace Tourist_Project_MVC.Controllers.MobileControllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]

    public class MobileDestinationController : ControllerBase
    {
        private readonly IDestinationRepository _destinationRepo;
        private readonly UserManager<ApplicationUser> _userManager;
        public MobileDestinationController(
            IDestinationRepository destinationRepository,
            UserManager<ApplicationUser> userManager
            )
        {
            _destinationRepo = destinationRepository;
            _userManager = userManager;
        }

        [HttpGet("AllDest")]
        public IActionResult GetAll()
        {
            var destinations = _destinationRepo.GetAll();

            if(destinations == null)
            {
                return NotFound(new { message = "Unable to retrieve destinations." });
            }
            var destinationDto = destinations.Select(d => new DestinationDto
            {
                Id = d.Id,
                Name = d.Name,
                Category = d.Category,
                Latitude = d.Location.Y,
                Longitude = d.Location.X,
                Description = d.Description,
                Rating = d.Rating,
                Status = d.Status
            }).ToList();
            return Ok(destinationDto);
        }
        
        [HttpPost("GetDestinationById")]
        public async Task <IActionResult> GetDestinationById([FromBody] DestinationIdDto dto) 
        {
            var applicationUser = await _userManager.GetUserAsync(User);

            if(applicationUser == null)
            {
                return Unauthorized();
            }

            var destination = await _destinationRepo.GetByIdAsync(dto.DestinationId);

            if(destination == null)
            {
                return NotFound(new { message = "Destination not found." });
            }
            try
            {

            
            var destinationDto = new DestinationDetailsDto
            {
                Id = destination.Id,
                Name = destination.Name,
                City = destination.City,
                OpenHour = destination.OpeningHours,
                OpenAt = destination.OpenAt,
                CloseAt = destination.CloseAt,
                TicketPrice = destination.TicketPrice,
                ForeignPrice = destination.ForeignPrice,
                BookingUrl = destination.Booking,
                Images = destination.PhotoUrlList,
                Description = destination.Description,
                Latitude = destination.Location.Y,
                Longitude = destination.Location.X,
                Rating = destination.Rating,
                Visitors = destination.Visits,
                Status = destination.Status
            };
                return Ok(destinationDto);
            }
            catch(Exception e)
            {
                return StatusCode(500, new { message = e.Message});
            }
            
        }
    }
}
