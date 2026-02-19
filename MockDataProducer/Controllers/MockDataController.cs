using Microsoft.AspNetCore.Mvc;

namespace MockDataProducer.Controllers;

[ApiController]
[Route("/")]
[Produces("application/json")]
public class MockDataController : ControllerBase
{
    private readonly ILogger<MockDataController> _logger;
    private readonly Worker _worker;

    public MockDataController(ILogger<MockDataController> logger, Worker worker)
    {
        _logger = logger;       
        _worker = worker;
    }

    [HttpPost("toggle")]
    public IActionResult ToggleDataGen([FromQuery] bool state)
    {
        _worker.SetState(state);
        
        var message = state ? "Data generation started" : "Data generation stopped";
        _logger.LogInformation(message);
        
        return Ok(new { isRunning = state, message });
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new { isRunning = _worker.IsRunning });
    }
}
