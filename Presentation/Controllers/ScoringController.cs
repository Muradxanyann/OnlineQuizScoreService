using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DefaultNamespace;

[ApiController]
[Route("api/[controller]")]
public class ScoringController : ControllerBase
{
    private readonly IScoringService _scoringService;
    private readonly ILogger<ScoringController> _logger;

    public ScoringController(IScoringService scoringService, ILogger<ScoringController> logger)
    {
        _scoringService = scoringService;
        _logger = logger;
    }

    [HttpPost("process-submission")]
    public async Task<IActionResult> ProcessSubmission([FromBody] QuizSubmittedEvent submission)
    {
        try
        {
            await _scoringService.ProcessSubmissionAsync(submission);
            return Ok(new { message = "Submission processed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing submission");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "Healthy", service = "Scoring Service" });
    }

    [HttpGet("test-connection/{quizId}")]
    public async Task<IActionResult> TestConnection(int quizId)
    {
        try
        {
            var quizManagerClient = HttpContext.RequestServices.GetRequiredService<IQuizManagerClient>();
            var quizDetails = await quizManagerClient.GetQuizDetailsAsync(quizId);
                
            return Ok(new { 
                quizDetails = quizDetails != null ? "Connected successfully" : "Quiz not found",
                quizId = quizId
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}