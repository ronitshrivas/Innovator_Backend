using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResearchService.DTOs;
using ResearchService.Services;

namespace ResearchService.Controllers;

[ApiController]
[AllowAnonymous]
public class ResearchController : ControllerBase
{
    private readonly IResearchPaperService _service;

    public ResearchController(IResearchPaperService service) => _service = service;

    [HttpGet("research")]
    public async Task<IActionResult> GetPapers(
        [FromQuery] string? search,
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        var result = await _service.GetPapersAsync(search, type, status, page, limit);
        return Ok(result);
    }

    [HttpGet("research/{id:int}")]
    public async Task<IActionResult> GetPaper(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return result is null
            ? NotFound(new { message = "Research paper not found." })
            : Ok(result);
    }

    [HttpPost("research/upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload([FromForm] UploadResearchForm form)
    {
        if (form.PaperFile is null || form.PaperFile.Length == 0)
            return BadRequest(new { message = "paper_file is required." });

        var request = new UploadResearchRequest(
            form.Email, form.Title, form.Description, form.Type, form.Price, form.ResearcherNames);

        var created = await _service.UploadAsync(request, form.PaperFile, form.ResearcherFile);
        return StatusCode(201, created);
    }

    [HttpPost("payment/initiate/{paperId:int}")]
    public async Task<IActionResult> InitiatePayment(int paperId)
    {
        var result = await _service.InitiatePaymentAsync(paperId);
        return result is null
            ? NotFound(new { message = "Research paper not found." })
            : Ok(result);
    }
}
