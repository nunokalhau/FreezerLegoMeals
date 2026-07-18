using Microsoft.AspNetCore.Mvc;

namespace WebApi.DotNet.Controllers;

/// <summary>
/// Controller for meal planning operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MealPlanningController : ControllerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MealPlanningController"/> class.
    /// </summary>
    public MealPlanningController()
    {
    }

    // Currently empty as expected, can be extended with meal planning features as needed
}