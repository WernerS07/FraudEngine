using Confluent.Kafka;
using FraudEngineService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Models;
using FraudEngineService.Models;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Shared.Services;


namespace FraudEngineService.Controllers;

[ApiController]
[Route("[controller]")]
public class RecordController : ControllerBase
{
    private readonly ILogger<RecordController> _logger;
    private readonly TransactionDBContext _context;

    private readonly RecordService _recordService;

    private readonly RedisCacheService _cache;
    public RecordController(ILogger<RecordController> logger,TransactionDBContext context,RecordService recordService, RedisCacheService cache)
    {
        _logger = logger;
        _context = context;
        _recordService = recordService;
        _cache = cache;
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<Record>>> Get(    
        [FromQuery] int limit = 10,
        [FromQuery] int page = 1)
    {   
        int offset = limit * (page-1);
        if(limit <= 0 || limit > 100) 
            return BadRequest("Limit must be between 1 and 100");
        
        if(page < 1) 
            return BadRequest("Page must be greater than or equal to 1");

        var response = await _recordService.GetRecords(offset,limit);

        if(response == null)
        {
            return NotFound("No results");
        }

        return Ok(response);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<List<Record>>> Get(int id)
    {
        var record = await _recordService.GetRecordById(id);
        
        if (record.Count == 0 || record == null)
        {
            return NotFound();
        }
        
        return record;
    }

    [HttpGet("FraudCheck")]
    public async Task<ActionResult<PaginatedResponse<Record>>> GetFraudulentRecords([FromQuery] int limit = 10,
        [FromQuery] int page = 1)
    {   
        int offset = limit * (page-1);
        if(limit <= 0 || limit > 100) 
            return BadRequest("Limit must be between 1 and 100");
        
        if(page < 1) 
            return BadRequest("Page must be greater than or equal to 1");

        var response = await _recordService.GetFraudRecords(offset,limit);

        if(response == null)
        {
            return NotFound("No results");
        }

        return Ok(response);
    }

    [HttpPut("ApproveRecord/{id}")]
    public async Task<IActionResult> ApproveRecord(int id)
    {
        if(id <= 0)
        {
            return BadRequest("Invalid record ID.");
        }
        Record? record = await _context.Records.FindAsync(id);
        if (record == null)
        {
            return NotFound($"Record with ID {id} not found.");
        }
        record.Isfraud = false;
        await _context.SaveChangesAsync();
        // Invalidate caches 
        await _cache.ClearAllInstanceCachesAsync();
        return Ok("Record Updated Successfully" + record);
    }

    // TODO!!
    // Get by Account ID 
    [HttpGet("Account/{id}")]
    public async Task<ActionResult<List<Record>>> GetByAccount(
        int id,    
        [FromQuery] int limit = 10,
        [FromQuery] int page = 1)
    {   
        int offset = (page - 1) * limit;
        if(limit <= 0 || limit > 100) {

                return BadRequest("Limit must be between 0 and 100");
        }
        if(offset < 0) {

                return BadRequest("Offset must be greater than or equal 0");
        }
        int _limit = (limit <= 100) && (limit > 0) ? limit : 10;
        int _offset = offset >= 0 ? offset : 10;
        var record = await _recordService.GetRecordByAccountId(id,_limit,_offset);
        
        if (record.TotalCount == 0)
        {
            return NotFound();
        }
        
        return Ok(record);
    }

    // Get by Recipient ID 
    [HttpGet("Recipient/{id}")]
    public async Task<ActionResult<List<Record>>> GetByRecipient(
        int id,    
        [FromQuery] int limit = 10,
        [FromQuery] int page = 1)
    {   
        int offset = (page - 1) * limit;
        if(limit <= 0 || limit > 100) {

                return BadRequest("Limit must be between 0 and 100");
        }
        if(offset < 0) {

                return BadRequest("Offset must be greater than or equal 0");
        }
        int _limit = (limit <= 100) && (limit > 0) ? limit : 10;
        int _offset = offset >= 0 ? offset : 10;
        var record = await _recordService.GetRecordByAccountRecipientID(id,_limit,_offset);
        
        if (record.TotalCount == 0)
        {
            return NotFound();
        }
        
        return Ok(record);
    }

    // Get by Location 
    [HttpGet("Location/{location}")]
    public async Task<ActionResult<List<Record>>> GetByLocation(
        string location,    
        [FromQuery] int limit = 10,
        [FromQuery] int page = 1)
    {   
        int offset = (page - 1) * limit;
        if(limit <= 0 || limit > 100) {

                return BadRequest("Limit must be between 0 and 100");
        }
        if(offset < 0) {

                return BadRequest("Offset must be greater than or equal 0");
        }
        int _limit = (limit <= 100) && (limit > 0) ? limit : 10;
        int _offset = offset >= 0 ? offset : 10;
        var record = await _recordService.GetRecordByLocation(location,_limit,_offset);
        
        if (record.TotalCount == 0)
        {
            return NotFound();
        }
        
        return Ok(record);
    }

    // Get by Location 
    [HttpGet("Category/{category}")]
    public async Task<ActionResult<List<Record>>> GetByCategory(
        string category,    
        [FromQuery] int limit = 10,
        [FromQuery] int page = 1)
    {   
        int offset = (page - 1) * limit;
        if(limit <= 0 || limit > 100) {

                return BadRequest("Limit must be between 0 and 100");
        }
        if(offset < 0) {

                return BadRequest("Offset must be greater than or equal 0");
        }
        if(category == null || category == "")
        {
            return BadRequest("Category must be provided");
        }
        int _limit = (limit <= 100) && (limit > 0) ? limit : 10;
        int _offset = offset >= 0 ? offset : 10;
        var record = await _recordService.GetRecordByCategory(category,_limit,_offset);
        
        if (record.TotalCount == 0)
        {
            return NotFound();
        }
        
        return Ok(record);
    }
}
