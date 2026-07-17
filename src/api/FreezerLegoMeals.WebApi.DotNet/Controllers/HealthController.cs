using Microsoft.AspNetCore.Mvc;

namespace FreezerLegoMeals.WebApi.DotNet.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new
            {
                status = "healthy",
                service = "FreezerLegoMeals.WebApi.DotNet"
            });
        }
    }
}